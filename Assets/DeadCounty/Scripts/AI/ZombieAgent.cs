using System;
using DeadCounty.Animation;
using DeadCounty.Perception;
using UnityEngine;
using UnityEngine.AI;

namespace DeadCounty.AI
{
    // One zombie in the scene: turns what it senses into a PerceptionSnapshot for its ZombieBrain, executes the
    // brain's commands through a ZombieMotor and exposes the resulting motion to its animator. It has no Update:
    // the ZombieManager calls Sense on its time slice, then Think and Move every frame unless it is dormant.
    [RequireComponent(typeof(NavMeshAgent), typeof(ZombieHealth))]
    public partial class ZombieAgent : MonoBehaviour, IHumanoidMotion, IZombieWorld
    {
        const float RandomPointSampleDistance = 2f;
        const float MinAimDistance = 1e-3f;
        const int FarAnimationInterval = 2;
        const int DormantAnimationInterval = 8;

        [Header("Setup")]
        [SerializeField] ZombieProfile profile;
        [SerializeField] int seed;
        [Tooltip("Empty only on bodies built without any animator (PlayMode tests); LOD and snapping are then skipped.")]
        [SerializeField] ProceduralHumanoidAnimator animator;
        [Tooltip("Test zombies: no per-zombie speed jitter.")]
        [SerializeField] bool deterministic;

        NavMeshAgent navAgent;
        ZombieHealth health;
        ZombieManager manager;
        ZombieTuning tuning;
        ZombieBrain brain;
        ZombieMotor motor;
        IPerceivable target;
        PerceptionSnapshot snapshot;
        BrainCommands commands = new() { Pose = PoseModifiers.Neutral };
        Vector3 spawnPosition;
        Quaternion spawnRotation;
        int ownId;
        bool dead;

        public ZombieProfile Profile => profile;
        public int Seed => seed;
        public bool Deterministic => deterministic;
        // Source id of this zombie's noises and AIEvents.
        public int AgentId => ownId;
        public ZombieBrain Brain => brain;
        public ZombieState State => brain.State;
        public ZombieHealth Health => health;
        public NavMeshAgent NavAgent => navAgent;
        public Vector3 Position => motor.Position;
        public Vector3 Heading => motor.Heading;
        public bool IsDead => dead;
        public ZombieLod Lod { get; private set; }
        // Last snapshot handed to the brain; visibility fields date from the last Sense.
        public PerceptionSnapshot Perception => snapshot;
        public BrainCommands Commands => commands;

        public float CurrentSpeed { get; private set; }
        public bool IsGrounded => true;
        public bool IsCrouching => false;
        public float AimYaw { get; private set; }
        public float AimPitch { get; private set; }
        public PoseModifiers Pose => commands.Pose;
        public int MotionResetId { get; private set; }

        internal int RecordIndex { get; set; } = -1;
        internal int ManagerIndex { get; set; } = -1;
        internal float NextSenseTime { get; set; }
        internal Vector3 SpawnPosition => spawnPosition;
        internal Quaternion SpawnRotation => spawnRotation;

        // Zombies built in code: call on the inactive GameObject, before its first activation runs Awake.
        public void Configure(ZombieProfile zombieProfile, int zombieSeed, bool isDeterministic)
        {
            if (brain != null) throw new InvalidOperationException($"{name}: Configure must run before the zombie first wakes up");
            profile = zombieProfile;
            seed = zombieSeed;
            deterministic = isDeterministic;
        }

        void Awake()
        {
            if (profile == null) throw new InvalidOperationException($"{name}: ZombieAgent needs a ZombieProfile");
            if (animator == null && GetComponentInChildren<ProceduralHumanoidAnimator>(true) != null)
                throw new InvalidOperationException($"{name}: its ProceduralHumanoidAnimator is not assigned to the ZombieAgent");
            navAgent = GetComponent<NavMeshAgent>();
            health = GetComponent<ZombieHealth>();
            // GetInstanceID and the EntityId to int cast are compile errors in Unity 6.6; NoiseEvent keeps an int id.
            ownId = gameObject.GetEntityId().GetHashCode();
            tuning = profile.Tuning;
            if (deterministic)
            {
                tuning.WanderSpeedJitter = 0f;
                tuning.SpeedJitter = 0f;
            }
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
            brain = new ZombieBrain(tuning, seed, spawnPosition);
            motor = new ZombieMotor(navAgent, transform, profile.Locomotion, tuning.Attack, seed);
            lastNoiseId = NoiseSystem.Buffer.LatestId;
            health.Died += OnDied;
            UpdateAim();
        }

        void OnEnable()
        {
            manager = ZombieManager.Instance;
            if (manager == null) throw new InvalidOperationException($"{name}: no enabled ZombieManager in the scene");
            manager.Register(this);
        }

        void OnDisable()
        {
            if (manager != null) manager.Unregister(this);
        }

        void Start()
        {
            if (navAgent.isOnNavMesh) return;
            enabled = false;
            throw new InvalidOperationException($"{name}: not on a NavMesh at {transform.position}");
        }

        void OnDestroy()
        {
            if (health != null) health.Died -= OnDied;
        }

        public void Think(float dt)
        {
            RefreshSnapshot();
            brain.Tick(dt, snapshot, this, ref commands);
            noisePending = false;
            if (brain.TransitionedThisTick) AIEvents.RaiseTransition(ownId, brain.LastTransition, motor.Position);
            if (commands.EmitCall) NoiseSystem.Emit(motor.Position, tuning.CallRadius, NoiseKind.ZombieCall, ownId);
        }

        public void Move(float dt)
        {
            var chasing = brain.State is ZombieState.Chase or ZombieState.Attack;
            var hasTarget = target != null;
            if (motor.Move(commands, chasing, hasTarget ? target.Position : default, hasTarget, dt)) MotionResetId++;
            CurrentSpeed = motor.MeasuredSpeed;
            UpdateAim();
            // The brain expects the strike resolved after the last lunge step has moved the body.
            if (commands.StrikeNow) Strike();
        }

        // Back to the recorded spawn pose, calm and at full health; revives a dead zombie.
        public void ResetToSpawn()
        {
            if (RecordIndex < 0) throw new InvalidOperationException($"{name}: ResetToSpawn before registering with the ZombieManager");
            var record = manager.GetRecord(RecordIndex);
            dead = false;
            navAgent.enabled = true;
            motor.Reset(record.SpawnPosition, record.SpawnRotation);
            motor.SetDormant(Lod == ZombieLod.Dormant);
            brain.Reset(record.SpawnPosition);
            AIEvents.RaiseTransition(ownId, brain.LastTransition, motor.Position);
            health.Restore();
            commands = new BrainCommands { Pose = PoseModifiers.Neutral };
            ClearPerception();
            CurrentSpeed = 0f;
            UpdateAim();
            MotionResetId++;
            if (isActiveAndEnabled) manager.Register(this);
        }

        internal void SetLod(ZombieLod lod)
        {
            if (lod == Lod) return;
            var wasDormant = Lod == ZombieLod.Dormant;
            Lod = lod;
            if (lod == ZombieLod.Dormant)
            {
                motor.SetDormant(true);
                CurrentSpeed = 0f;
            }
            else if (wasDormant) motor.SetDormant(false);
            if (animator == null) return;
            animator.GroundRays = lod == ZombieLod.Near;
            animator.UpdateEvery = lod switch
            {
                ZombieLod.Far => FarAnimationInterval,
                ZombieLod.Dormant => DormantAnimationInterval,
                _ => 1,
            };
        }

        void OnDied(ZombieHealth _)
        {
            dead = true;
            motor.Halt();
            CurrentSpeed = 0f;
            navAgent.enabled = false;
            manager.ReportDeath(this);
        }

        void RefreshSnapshot()
        {
            var position = motor.Position;
            snapshot.Time = Time.time;
            snapshot.Position = position;
            snapshot.Forward = motor.Heading;
            snapshot.ArrivedAtDestination = motor.Arrived;
            snapshot.PathFailed = motor.PathFailed;
            if (target != null)
            {
                var targetPosition = target.Position;
                var offset = targetPosition - position;
                offset.y = 0f;
                snapshot.TargetAlive = target.IsAlive;
                snapshot.TargetPosition = targetPosition;
                snapshot.TargetVelocity = target.Velocity;
                snapshot.TargetSpeed = target.Speed;
                snapshot.TargetDistance = offset.magnitude;
            }
            else
            {
                snapshot.TargetAlive = false;
                snapshot.TargetVisible = false;
                snapshot.InProximity = false;
                // Out of feeding range, so an aware zombie that loses its target goes back to its anchor.
                snapshot.TargetDistance = float.PositiveInfinity;
            }
            snapshot.HeardNoise = noisePending;
            snapshot.NoisePosition = noisePosition;
            snapshot.NoiseKind = noiseKind;
        }

        void Strike()
        {
            var origin = motor.Position;
            var hit = target != null && target.IsAlive && AttackResolver.Hits(tuning.Attack, origin, commands.LungeDirection, target.Position);
            if (hit) target.ApplyHit(tuning.Attack.Damage, origin);
            brain.ReportStrike(hit);
            AIEvents.RaiseStrike(ownId, Time.time, origin, commands.LungeDirection, hit);
        }

        // Head aim from the eyes toward the commanded look point, in world degrees; positive pitch looks down.
        void UpdateAim()
        {
            var look = commands.HasLookPoint ? commands.LookPoint - (motor.Position + Vector3.up * tuning.EyeHeight) : motor.Heading;
            var planar = new Vector2(look.x, look.z).magnitude;
            if (planar < MinAimDistance)
            {
                look = motor.Heading;
                planar = 1f;
            }
            AimYaw = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg;
            AimPitch = -Mathf.Atan2(look.y, planar) * Mathf.Rad2Deg;
        }

        bool IZombieWorld.TryRandomPoint(Vector3 center, float radius, double random01a, double random01b, out Vector3 point)
        {
            var angle = (float)(random01a * 2.0 * Math.PI);
            var distance = radius * Mathf.Sqrt((float)random01b);
            var candidate = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
            if (NavMesh.SamplePosition(candidate, out var hit, RandomPointSampleDistance, NavMesh.AllAreas))
            {
                point = hit.position;
                return true;
            }
            point = center;
            return false;
        }
    }
}
