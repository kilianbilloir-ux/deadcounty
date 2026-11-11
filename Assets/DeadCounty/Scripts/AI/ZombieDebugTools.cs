using System.Collections.Generic;
using System.Text;
using DeadCounty.Core;
using DeadCounty.Perception;
using DeadCounty.Player;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace DeadCounty.AI
{
    // Development tools for the zombies, next to the ZombieManager. Direct device reads, not gameplay actions:
    // F2 AI panel, event log and gizmos (Gizmos must be on in the Game view) · F3 clock between noon and 23:00 ·
    // F5 ghost (zombies neither see nor hear the player) · F6 freeze every zombie · F7 spawn a zombie where the
    // camera points, or a few metres ahead · F8 reset every zombie to its spawn · F9 god mode · F10 slow motion ·
    // N + 1..4 test noise of 4, 6, 12 or 20 m at the player.
    [RequireComponent(typeof(ZombieManager))]
    public class ZombieDebugTools : MonoBehaviour
    {
        const int LogSize = 8;
        const int DebugNoiseSource = -1;
        const float SlowMotionScale = 0.25f;
        const float SpawnRayLength = 80f;
        const float SpawnAheadDistance = 8f;
        const float SpawnSampleDistance = 3f;
        const float NoiseShowTime = 2f;
        static readonly float[] TestNoiseRadii = { 4f, 6f, 12f, 20f };

        [SerializeField] GameObject zombiePrefab;
        [SerializeField] bool visible;

        ZombieManager manager;
        DayNightDirector director;
        readonly Queue<string> log = new();
        readonly StringBuilder text = new();
        readonly NoiseEvent[] noises = new NoiseEvent[64];
        GUIStyle style;
        int spawnSeed = 10000;
        bool slowMotion;

        void Awake()
        {
            manager = GetComponent<ZombieManager>();
            director = FindAnyObjectByType<DayNightDirector>();
        }

        void OnEnable()
        {
            AIEvents.Transition += OnTransition;
            AIEvents.Strike += OnStrike;
        }

        void OnDisable()
        {
            AIEvents.Transition -= OnTransition;
            AIEvents.Strike -= OnStrike;
            if (slowMotion) Time.timeScale = 1f;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.f2Key.wasPressedThisFrame) visible = !visible;
            if (keyboard.f3Key.wasPressedThisFrame) ToggleNight();
            if (keyboard.f5Key.wasPressedThisFrame) ToggleGhost();
            if (keyboard.f6Key.wasPressedThisFrame)
            {
                manager.Frozen = !manager.Frozen;
                Log(manager.Frozen ? "zombies gelés" : "zombies dégelés");
            }
            if (keyboard.f7Key.wasPressedThisFrame) SpawnAtAim();
            if (keyboard.f8Key.wasPressedThisFrame)
            {
                manager.ResetAll();
                Log("zombies remis à leur spawn");
            }
            if (keyboard.f9Key.wasPressedThisFrame) ToggleGod();
            if (keyboard.f10Key.wasPressedThisFrame)
            {
                slowMotion = !slowMotion;
                Time.timeScale = slowMotion ? SlowMotionScale : 1f;
            }
            if (!keyboard.nKey.isPressed) return;
            for (var i = 0; i < TestNoiseRadii.Length; i++)
                if (keyboard[Key.Digit1 + i].wasPressedThisFrame) EmitTestNoise(TestNoiseRadii[i]);
        }

        void ToggleNight()
        {
            if (director == null)
            {
                Log("pas de cycle jour/nuit dans la scène");
                return;
            }
            director.SetHour(WorldLighting.IsNight ? 12f : 23f);
            Log(WorldLighting.IsNight ? "nuit (23:00)" : "jour (12:00)");
        }

        void ToggleGhost()
        {
            var player = PerceptionTarget.Player;
            if (player == null) return;
            player.Visible = !player.Visible;
            Log(player.Visible ? "fantôme désactivé" : "fantôme : les zombies ne perçoivent plus le joueur");
        }

        void ToggleGod()
        {
            var player = PerceptionTarget.Player;
            if (player == null) return;
            var health = player.GetComponent<PlayerHealth>();
            health.Invulnerable = !health.Invulnerable;
            Log(health.Invulnerable ? "invincible" : "vulnérable");
        }

        void EmitTestNoise(float radius)
        {
            var player = PerceptionTarget.Player;
            if (player == null) return;
            NoiseSystem.Emit(player.Position, radius, NoiseKind.Debug, DebugNoiseSource);
            Log($"bruit de test {radius:0} m");
        }

        void SpawnAtAim()
        {
            var view = Camera.main;
            if (view == null || zombiePrefab == null) return;
            // The over-the-shoulder camera often looks level, over the ground: then spawn a few metres ahead.
            var ray = view.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            var flatForward = Vector3.ProjectOnPlane(ray.direction, Vector3.up).normalized;
            var aim = Physics.Raycast(ray, out var hit, SpawnRayLength, GameLayers.EnvironmentMask, QueryTriggerInteraction.Ignore)
                ? hit.point
                : ray.origin + flatForward * SpawnAheadDistance;
            if (!NavMesh.SamplePosition(aim, out var ground, SpawnSampleDistance, NavMesh.AllAreas))
            {
                Log("pas de sol navigable visé");
                return;
            }
            // Built under an inactive holder so Configure runs before the agent wakes up.
            var holder = new GameObject("SpawnHolder");
            holder.SetActive(false);
            var facing = view.transform.position - ground.position;
            facing.y = 0f;
            var zombie = Instantiate(zombiePrefab, ground.position, facing.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(facing) : Quaternion.identity, holder.transform);
            zombie.name = $"Zombie_Debug_{spawnSeed}";
            var agent = zombie.GetComponent<ZombieAgent>();
            agent.Configure(agent.Profile, spawnSeed++, false);
            zombie.transform.SetParent(null, true);
            Destroy(holder);
            Log($"{zombie.name} apparu");
        }

        void OnTransition(ZombieTransitionEvent e)
        {
            if (visible) Log($"{NameOf(e.AgentId)} : {e.From} → {e.To} ({e.Reason})");
        }

        void OnStrike(ZombieStrikeEvent e)
        {
            if (visible) Log($"{NameOf(e.AgentId)} frappe : {(e.Hit ? "touché" : "raté")}");
        }

        string NameOf(int agentId)
        {
            var agents = manager.Agents;
            for (var i = 0; i < agents.Count; i++)
                if (agents[i].AgentId == agentId) return agents[i].name;
            return "zombie";
        }

        void Log(string line)
        {
            Debug.Log($"[AI] {line}");
            log.Enqueue($"{Time.time:0.0}  {line}");
            while (log.Count > LogSize) log.Dequeue();
        }

        void OnGUI()
        {
            if (!visible) return;
            style ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 13, richText = true, wordWrap = true, padding = new RectOffset(10, 10, 8, 8) };
            // IMGUI does not follow the screen resolution: scale the panel like a 1080p layout.
            var scale = Mathf.Max(1f, Screen.height / 1080f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            text.Clear();
            text.Append("<b>IA</b>  (F2)\n");
            text.Append($"zombies {manager.Agents.Count} · actifs {manager.ActiveCount}/{manager.ActiveBudget} · perception {manager.AverageSenseMs:0.000} ms · rayons {manager.RaysThisFrame}\n");
            for (var s = ZombieState.Idle; s <= ZombieState.Feed; s++)
                text.Append(s).Append(' ').Append(manager.CountIn(s)).Append(s == ZombieState.Search ? "\n" : "   ");
            text.Append('\n');
            AppendNearest();
            var player = PerceptionTarget.Player;
            var health = player != null ? player.GetComponent<PlayerHealth>() : null;
            text.Append($"F3 {(WorldLighting.IsNight ? "nuit" : "jour")} · F5 fantôme {OnOff(player != null && !player.Visible)} · F6 gel {OnOff(manager.Frozen)} · ");
            text.Append($"F9 invincible {OnOff(health != null && health.Invulnerable)} · F10 ralenti {OnOff(slowMotion)}\n");
            text.Append("F7 zombie visé ou devant · F8 reset · N+1..4 bruit 4/6/12/20 m\n");
            foreach (var line in log) text.Append(line).Append('\n');
            GUI.Box(new Rect(Screen.width / scale - 570f, 10f, 560f, 360f), text.ToString(), style);
            GUI.matrix = Matrix4x4.identity;
        }

        static string OnOff(bool on) => on ? "oui" : "non";

        void AppendNearest()
        {
            var player = PerceptionTarget.Player;
            if (player == null) return;
            ZombieAgent nearest = null;
            var best = float.PositiveInfinity;
            foreach (var agent in manager.Agents)
            {
                var offset = agent.Position - player.Position;
                offset.y = 0f;
                if (offset.sqrMagnitude >= best) continue;
                best = offset.sqrMagnitude;
                nearest = agent;
            }
            if (nearest == null) return;
            text.Append($"plus proche : {nearest.name} à {Mathf.Sqrt(best):0.0} m · {nearest.State} · suspicion {nearest.Brain.Suspicion:0.00} · ");
            text.Append($"vu {OnOff(nearest.Perception.TargetVisible)} · murs au dernier bruit {nearest.LastNoiseWalls}\n");
        }

        // Sight cones coloured by state, the last known position of hunted targets, anchors, recent noises.
        void OnDrawGizmos()
        {
            if (!visible || manager == null || !Application.isPlaying) return;
            foreach (var agent in manager.Agents)
            {
                var tuning = agent.Profile.Tuning;
                var eye = agent.Position + Vector3.up * tuning.EyeHeight;
                Gizmos.color = StateColor(agent.State);
                var range = agent.Perception.SightRange;
                foreach (var side in new[] { -0.5f, 0.5f })
                    Gizmos.DrawLine(eye, eye + Quaternion.Euler(0f, side * tuning.FieldOfView, 0f) * agent.Heading * range);
                Gizmos.DrawLine(eye, eye + agent.Heading * range);
                if (agent.State is ZombieState.Chase or ZombieState.Attack or ZombieState.Search or ZombieState.Investigate)
                {
                    Gizmos.DrawLine(eye, agent.Brain.LastKnownPosition + Vector3.up * 0.1f);
                    Gizmos.DrawWireSphere(agent.Brain.LastKnownPosition, 0.3f);
                }
                else
                {
                    Gizmos.DrawWireCube(agent.Brain.Anchor + Vector3.up * 0.05f, new Vector3(0.4f, 0.1f, 0.4f));
                }
            }
            var count = NoiseSystem.Buffer.CopySince(NoiseSystem.Buffer.LatestId - noises.Length, noises);
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.8f);
            for (var i = 0; i < count; i++)
                if (Time.time - noises[i].Time < NoiseShowTime) Gizmos.DrawWireSphere(noises[i].Position, noises[i].Radius);
        }

        static Color StateColor(ZombieState state) => state switch
        {
            ZombieState.Alert => Color.yellow,
            ZombieState.Investigate or ZombieState.Search => new Color(1f, 0.55f, 0.1f),
            ZombieState.Chase or ZombieState.Attack => Color.red,
            ZombieState.Feed => new Color(0.5f, 0f, 0f),
            _ => new Color(0.6f, 0.75f, 0.6f),
        };
    }
}
