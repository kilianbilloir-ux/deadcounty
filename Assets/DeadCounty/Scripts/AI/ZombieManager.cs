using System;
using System.Collections.Generic;
using DeadCounty.Player;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.AI;

namespace DeadCounty.AI
{
    // Scheduler of every zombie in the scene: every half second it sorts them into distance tiers around the
    // target and keeps the nearest activeBudget awake; each frame it runs the due senses, staggered and capped,
    // then Think and Move for the awake ones. Records stay apart from the views so any zombie can become data.
    [DefaultExecutionOrder(-50)]
    public class ZombieManager : MonoBehaviour
    {
        const float GoldenRatio = 0.618034f;
        const int StateCount = (int)ZombieState.Feed + 1;
        const float SenseTimeSmoothing = 0.1f;

        static readonly ProfilerMarker ManagerMarker = new("DC.AI.Manager");
        static readonly ProfilerMarker SenseMarker = new("DC.AI.Sense");
        static readonly ProfilerMarker BrainMarker = new("DC.AI.Brain");
        static readonly ProfilerMarker MoveMarker = new("DC.AI.Move");

        [Header("Budget")]
        [Tooltip("Most zombies awake at once; the farthest beyond it go dormant.")]
        [SerializeField] int activeBudget = 30;
        [SerializeField] int maxSensesPerFrame = 8;

        [Header("Level of detail")]
        [SerializeField] float lodInterval = 0.5f;
        [SerializeField] float nearDistance = 25f;
        [SerializeField] float midDistance = 50f;
        [Tooltip("Beyond this zombies go dormant.")]
        [SerializeField] float farDistance = 100f;
        [Tooltip("Tier boundaries move this fraction outward for zombies inside and inward for zombies outside, so none flickers.")]
        [Range(0f, 0.5f)]
        [SerializeField] float hysteresis = 0.1f;

        [Header("Sense rates (Hz)")]
        [SerializeField] float nearSenseRate = 10f;
        [SerializeField] float midSenseRate = 4f;
        [Tooltip("Far zombies only listen.")]
        [SerializeField] float farSenseRate = 1f;

        [Header("Navigation")]
        [SerializeField] float avoidancePredictionTime = 0.5f;

        readonly List<ZombieAgent> agents = new();
        readonly List<ZombieRecord> records = new();
        readonly int[] stateCounts = new int[StateCount];
        float[] lodDistances = new float[32];
        int[] lodOrder = new int[32];
        ZombieLod[] lodTiers = new ZombieLod[32];
        float lodTimer;
        bool lodDirty;
        int senseCursor;
        int registrations;

        public static ZombieManager Instance { get; private set; }

        // Set by tests to perceive a stand-in instead of the player.
        public IPerceivable TargetOverride { get; set; }

        public IPerceivable Target
        {
            get
            {
                if (TargetOverride != null) return TargetOverride;
                var player = PerceptionTarget.Player;
                return player != null ? player : null;
            }
        }

        public IReadOnlyList<ZombieAgent> Agents => agents;
        public IReadOnlyList<ZombieRecord> Records => records;
        public int ActiveBudget => activeBudget;
        public int MaxSensesPerFrame => maxSensesPerFrame;
        public int ActiveCount { get; private set; }
        public int SensesThisFrame { get; private set; }
        public long TotalSenses { get; private set; }
        public int RaysThisFrame { get; private set; }
        // Smoothed cost of one Sense, in milliseconds.
        public float AverageSenseMs { get; private set; }

        public int CountIn(ZombieState state) => stateCounts[(int)state];

        public ZombieRecord GetRecord(int index) => records[index];

        void Awake()
        {
            agents.Clear();
            records.Clear();
        }

        void OnEnable()
        {
            if (Instance != null && Instance != this)
                throw new InvalidOperationException($"Second ZombieManager on '{name}', '{Instance.name}' is already running");
            Instance = this;
            NavMesh.avoidancePredictionTime = avoidancePredictionTime;
            PlayerDeathFlow.PlayerRespawned += ResetAll;
            lodDirty = true;
        }

        void OnDisable()
        {
            PlayerDeathFlow.PlayerRespawned -= ResetAll;
            if (Instance == this) Instance = null;
        }

        internal void Register(ZombieAgent agent)
        {
            if (agent.RecordIndex < 0)
            {
                agent.RecordIndex = records.Count;
                records.Add(new ZombieRecord
                {
                    SpawnPosition = agent.SpawnPosition,
                    SpawnRotation = agent.SpawnRotation,
                    Seed = agent.Seed,
                    Profile = agent.Profile,
                    View = agent,
                });
            }
            else
            {
                var record = records[agent.RecordIndex];
                record.View = agent;
                record.Dead = agent.IsDead;
                records[agent.RecordIndex] = record;
            }
            // A dead view keeps its record linked so a reset can revive it, but takes no turn.
            if (agent.IsDead || agent.ManagerIndex >= 0) return;
            agent.ManagerIndex = agents.Count;
            agents.Add(agent);
            agent.NextSenseTime = Time.time + Mathf.Repeat(registrations++ * GoldenRatio, 1f) / nearSenseRate;
            EnsureLodCapacity(agents.Count);
            lodDirty = true;
        }

        // The view went away (disabled or destroyed); its record stays.
        internal void Unregister(ZombieAgent agent)
        {
            RemoveActive(agent);
            if (agent.RecordIndex < 0) return;
            var record = records[agent.RecordIndex];
            record.View = null;
            records[agent.RecordIndex] = record;
        }

        internal void ReportDeath(ZombieAgent agent)
        {
            RemoveActive(agent);
            var record = records[agent.RecordIndex];
            record.Dead = true;
            records[agent.RecordIndex] = record;
        }

        void RemoveActive(ZombieAgent agent)
        {
            var index = agent.ManagerIndex;
            if (index < 0) return;
            var last = agents.Count - 1;
            var moved = agents[last];
            agents[index] = moved;
            moved.ManagerIndex = index;
            agents.RemoveAt(last);
            agent.ManagerIndex = -1;
        }

        // Prototype rule: when the player respawns, every zombie, dead or alive, starts over at its spawn.
        void ResetAll()
        {
            for (var i = 0; i < records.Count; i++)
            {
                var view = records[i].View;
                if (view != null) view.ResetToSpawn();
            }
            lodDirty = true;
        }

        void Update()
        {
            var dt = Time.deltaTime;
            if (dt <= 0f) return;
            using (ManagerMarker.Auto())
            {
                var now = Time.time;
                var target = Target;
                lodTimer -= dt;
                if (lodDirty || lodTimer <= 0f)
                {
                    lodTimer = lodInterval;
                    lodDirty = false;
                    UpdateLod(target, now);
                }
                SenseDue(target, now);

                // Backwards so an agent leaving the list mid-loop does not make another one skip its turn.
                using (BrainMarker.Auto())
                {
                    for (var i = agents.Count - 1; i >= 0; i--)
                        if (agents[i].Lod != ZombieLod.Dormant) agents[i].Think(dt);
                }
                using (MoveMarker.Auto())
                {
                    for (var i = agents.Count - 1; i >= 0; i--)
                        if (agents[i].Lod != ZombieLod.Dormant) agents[i].Move(dt);
                }

                CountStates();
            }
        }

        void SenseDue(IPerceivable target, float now)
        {
            SensesThisFrame = 0;
            RaysThisFrame = 0;
            var count = agents.Count;
            if (count == 0) return;
            var start = System.Diagnostics.Stopwatch.GetTimestamp();
            using (SenseMarker.Auto())
            {
                // Rotating start: when the cap cuts a frame short, the skipped agents go first next frame.
                var k = 0;
                for (; k < count; k++)
                {
                    var agent = agents[(senseCursor + k) % count];
                    if (agent.Lod == ZombieLod.Dormant || now < agent.NextSenseTime) continue;
                    if (SensesThisFrame == maxSensesPerFrame) break;
                    agent.Sense(target, now);
                    // Keeps the long-run rate exact; an agent left far behind is due again at once, without a burst.
                    agent.NextSenseTime = Mathf.Max(agent.NextSenseTime + SenseInterval(agent.Lod), now);
                    SensesThisFrame++;
                    RaysThisFrame += agent.RaysLastSense;
                }
                senseCursor = (senseCursor + k) % count;
            }
            TotalSenses += SensesThisFrame;
            if (SensesThisFrame == 0) return;
            var ms = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency / SensesThisFrame);
            AverageSenseMs = AverageSenseMs > 0f ? Mathf.Lerp(AverageSenseMs, ms, SenseTimeSmoothing) : ms;
        }

        float SenseInterval(ZombieLod lod) => 1f / lod switch
        {
            ZombieLod.Near => nearSenseRate,
            ZombieLod.Mid => midSenseRate,
            _ => farSenseRate,
        };

        void UpdateLod(IPerceivable target, float now)
        {
            var count = agents.Count;
            var hasTarget = target != null;
            var targetPosition = hasTarget ? target.Position : Vector3.zero;
            var candidates = 0;
            for (var i = 0; i < count; i++)
            {
                var agent = agents[i];
                var offset = agent.Position - targetPosition;
                offset.y = 0f;
                // Without a target nothing is near or far: everyone competes for the budget equally.
                var distance = hasTarget ? offset.magnitude : 0f;
                lodDistances[i] = distance;
                lodTiers[i] = DistanceTier(distance, agent.Lod);
                if (lodTiers[i] != ZombieLod.Dormant) lodOrder[candidates++] = i;
            }
            if (candidates > activeBudget)
            {
                SortByDistance(candidates);
                for (var k = activeBudget; k < candidates; k++) lodTiers[lodOrder[k]] = ZombieLod.Dormant;
            }
            for (var i = 0; i < count; i++)
            {
                var agent = agents[i];
                var tier = lodTiers[i];
                // A zombie moving to a faster tier must not wait out its old, longer interval.
                if (tier != ZombieLod.Dormant && tier < agent.Lod)
                    agent.NextSenseTime = Mathf.Min(agent.NextSenseTime, now + SenseInterval(tier));
                agent.SetLod(tier);
            }
        }

        ZombieLod DistanceTier(float distance, ZombieLod current)
        {
            var tier = ZombieLod.Near;
            if (distance > Boundary(nearDistance, current, ZombieLod.Near)) tier = ZombieLod.Mid;
            if (distance > Boundary(midDistance, current, ZombieLod.Mid)) tier = ZombieLod.Far;
            if (distance > Boundary(farDistance, current, ZombieLod.Far)) tier = ZombieLod.Dormant;
            return tier;
        }

        float Boundary(float distance, ZombieLod current, ZombieLod inner) => distance * (current > inner ? 1f - hysteresis : 1f + hysteresis);

        // Insertion sort of the first count candidate indices by distance: a few dozen entries twice a second.
        void SortByDistance(int count)
        {
            for (var i = 1; i < count; i++)
            {
                var index = lodOrder[i];
                var distance = lodDistances[index];
                var j = i - 1;
                for (; j >= 0 && lodDistances[lodOrder[j]] > distance; j--) lodOrder[j + 1] = lodOrder[j];
                lodOrder[j + 1] = index;
            }
        }

        void EnsureLodCapacity(int count)
        {
            if (lodDistances.Length >= count) return;
            var capacity = Mathf.Max(count, lodDistances.Length * 2);
            Array.Resize(ref lodDistances, capacity);
            Array.Resize(ref lodOrder, capacity);
            Array.Resize(ref lodTiers, capacity);
        }

        void CountStates()
        {
            Array.Clear(stateCounts, 0, StateCount);
            var active = 0;
            for (var i = 0; i < agents.Count; i++)
            {
                var agent = agents[i];
                stateCounts[(int)agent.State]++;
                if (agent.Lod != ZombieLod.Dormant) active++;
            }
            ActiveCount = active;
        }

        // Domain reload is disabled on entering Play mode, so the previous session's manager must be dropped by hand.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;
    }
}
