using DeadCounty.Animation;
using DeadCounty.Perception;
using UnityEngine;

namespace DeadCounty.AI
{
    // Pure state machine of one zombie: calm wandering around an anchor, a readable alert before any chase,
    // investigation of noises, chase with tracking and prediction, search, the attack sub-state and feeding.
    // The agent fills a PerceptionSnapshot every frame and executes the BrainCommands. All randomness comes
    // from the seed, so equal seeds and inputs replay identically. At most one transition per Tick.
    public partial class ZombieBrain
    {
        const float Epsilon = 1e-4f;
        // An alert that heard nothing gives up once suspicion has decayed below this.
        const float CalmSuspicion = 0.1f;
        const float NoiseCapMargin = 0.05f;
        const float CalmArms = 0.15f;
        const float AlertArms = 0.5f;
        const float ChaseArms = 0.8f;
        const float GrabArms = 1f;
        const float FeedArms = 0.3f;
        const float LookDistance = 5f;
        const float LookAroundAngle = 70f;
        const float LookAroundPeriod = 3f;

        readonly ZombieTuning tuning;
        readonly System.Random random;
        readonly float hunch;
        readonly float headRoll;

        float now;
        float sinceStimulus;
        float sinceSeen;
        float sinceKnown;
        float sinceCall;
        float attackCooldown;
        bool callThisTick;
        bool strikeThisTick;
        Vector3 lookBase;
        float lookTimer;

        public ZombieBrain(in ZombieTuning tuning, int seed, Vector3 anchor)
        {
            this.tuning = tuning;
            random = new System.Random(seed);
            WanderSpeedFactor = 1f + tuning.WanderSpeedJitter * Signed();
            SpeedFactor = 1f + tuning.SpeedJitter * Signed();
            hunch = Range(tuning.HunchMin, tuning.HunchMax);
            headRoll = tuning.HeadRollMax * Signed();
            Restart(anchor);
            LastTransition = new ZombieTransition { From = State, To = State, Reason = TransitionReason.Reset };
        }

        public ZombieState State { get; private set; }
        public float Suspicion { get; private set; }
        public Vector3 LastKnownPosition { get; private set; }
        public Vector3 LastKnownVelocity { get; private set; }
        public float StateTime { get; private set; }
        public AttackPhase AttackPhase { get; private set; }
        public Vector3 Anchor { get; private set; }
        public float WanderSpeedFactor { get; }
        // Investigate, search and chase speed factor.
        public float SpeedFactor { get; }
        public ZombieTransition LastTransition { get; private set; }
        // Set by the Tick or Reset that changed State, cleared by the next Tick.
        public bool TransitionedThisTick { get; private set; }

        // State part of the hearing radius multiplier; the agent applies it to every noise.
        public float HearingMultiplier => tuning.HearingMultiplier * State switch
        {
            ZombieState.Alert or ZombieState.Investigate => tuning.HearingAlert,
            ZombieState.Search or ZombieState.Chase or ZombieState.Attack => tuning.HearingHunting,
            _ => tuning.HearingUnaware,
        };

        float ChaseSpeed => tuning.ChaseSpeed * SpeedFactor;

        public void Tick(float dt, in PerceptionSnapshot p, IZombieWorld world, ref BrainCommands c)
        {
            TransitionedThisTick = false;
            callThisTick = false;
            strikeThisTick = false;
            now = p.Time;
            StateTime += dt;
            sinceCall += dt;
            attackCooldown = Mathf.Max(0f, attackCooldown - dt);
            UpdateSuspicion(dt, p);
            UpdateKnowledge(dt, p);

            if (!p.TargetAlive && IsAware(State))
                ForgetDeadTarget(p);
            else
            {
                switch (State)
                {
                    case ZombieState.Idle:
                    case ZombieState.Wander:
                        UpdateCalm(dt, p, world);
                        break;
                    case ZombieState.Alert:
                        UpdateAlert(p);
                        break;
                    case ZombieState.Investigate:
                        UpdateInvestigate(dt, p, world);
                        break;
                    case ZombieState.Chase:
                        UpdateChase(dt, p, world);
                        break;
                    case ZombieState.Attack:
                        UpdateAttack(dt, p, world);
                        break;
                    case ZombieState.Search:
                        UpdateSearch(dt, p, world);
                        break;
                    case ZombieState.Feed:
                        if (p.TargetAlive) ReturnToAnchor(TransitionReason.LostTarget);
                        break;
                }
            }
            WriteCommands(p, ref c);
        }

        // Called after resolving the strike flagged by StrikeNow, before the next Tick; no call counts as a miss.
        public void ReportStrike(bool hit)
        {
            if (awaitingStrikeReport) strikeHit = hit;
        }

        public void Reset(Vector3 anchor)
        {
            var from = State;
            Restart(anchor);
            LastTransition = new ZombieTransition { From = from, To = State, Reason = TransitionReason.Reset, Time = now };
            TransitionedThisTick = true;
        }

        void Restart(Vector3 anchor)
        {
            Anchor = anchor;
            State = ZombieState.Wander;
            StateTime = 0f;
            wanderPoint = anchor;
            Suspicion = 0f;
            LastKnownPosition = anchor;
            LastKnownVelocity = Vector3.zero;
            AttackPhase = AttackPhase.None;
            awaitingStrikeReport = false;
            sinceStimulus = tuning.SuspicionHold;
            sinceSeen = float.MaxValue;
            sinceKnown = float.MaxValue;
            sinceCall = tuning.CallCooldown;
            attackCooldown = 0f;
        }

        void UpdateSuspicion(float dt, in PerceptionSnapshot p)
        {
            var sees = p.TargetAlive && p.TargetVisible;
            if (sees)
            {
                var rate = PerceptionMath.SpeedSuspicionMultiplier(p.TargetSpeed) * p.ConeFactor / PerceptionMath.SuspicionFillTime(p.TargetDistance, p.SightRange);
                if (p.VisibleFraction < 1f) rate *= tuning.OneRayFactor;
                if (State is ZombieState.Alert or ZombieState.Investigate or ZombieState.Search) rate *= tuning.AlertedFillMultiplier;
                Suspicion = Mathf.Min(1f, Suspicion + rate * dt);
                sinceStimulus = 0f;
            }
            else
            {
                sinceStimulus += dt;
                // Only the part of the frame past the hold decays, so the result does not depend on the frame rate.
                var decayTime = Mathf.Min(dt, sinceStimulus - tuning.SuspicionHold);
                if (decayTime > 0f) Suspicion = Mathf.Max(0f, Suspicion - tuning.SuspicionDecay * decayTime);
            }
            if (p.HeardNoise)
            {
                // Noise alone may alert but never starts a chase: only sight fills the last stretch.
                var cap = sees ? 1f : Mathf.Max(Suspicion, tuning.ChaseThreshold - NoiseCapMargin);
                Suspicion = Mathf.Min(Suspicion + tuning.NoiseSuspicion, cap);
                sinceStimulus = 0f;
            }
        }

        void UpdateKnowledge(float dt, in PerceptionSnapshot p)
        {
            sinceKnown += dt;
            if (Perceives(p))
            {
                sinceSeen = 0f;
                Know(p.TargetPosition, p.TargetVelocity);
            }
            else sinceSeen += dt;
        }

        void Know(Vector3 position, Vector3 velocity)
        {
            LastKnownPosition = position;
            LastKnownVelocity = velocity;
            sinceKnown = 0f;
        }

        static bool Perceives(in PerceptionSnapshot p) => p.TargetAlive && (p.TargetVisible || p.InProximity);

        static bool IsAware(ZombieState state) => state is ZombieState.Alert or ZombieState.Investigate or ZombieState.Chase or ZombieState.Search or ZombieState.Attack;

        void ForgetDeadTarget(in PerceptionSnapshot p)
        {
            if (p.TargetDistance <= tuning.FeedRadius) Transition(ZombieState.Feed, TransitionReason.TargetDead);
            else ReturnToAnchor(TransitionReason.TargetDead);
        }

        void Transition(ZombieState to, TransitionReason reason)
        {
            LastTransition = new ZombieTransition { From = State, To = to, Reason = reason, Time = now };
            TransitionedThisTick = true;
            if (State == ZombieState.Attack)
            {
                AttackPhase = AttackPhase.None;
                awaitingStrikeReport = false;
            }
            State = to;
            StateTime = 0f;
        }

        void StartLookAround(in PerceptionSnapshot p)
        {
            lookBase = Planar(p.Forward);
            lookTimer = 0f;
        }

        void WriteCommands(in PerceptionSnapshot p, ref BrainCommands c)
        {
            c = default;
            c.HasLookPoint = true;
            c.EmitCall = callThisTick;
            c.StrikeNow = strikeThisTick;
            c.AttackPhase = AttackPhase;
            c.LungeDirection = lungeDirection;
            c.Pose = PoseModifiers.Neutral;
            c.Pose.Hunch = hunch;
            c.Pose.HeadRoll = headRoll;
            switch (State)
            {
                case ZombieState.Idle:
                    Hold(ref c, AtEyes(p.Position + Planar(p.Forward) * LookDistance), CalmArms);
                    break;
                case ZombieState.Wander:
                    MoveTo(ref c, wanderPoint, tuning.WanderSpeed * WanderSpeedFactor, CalmArms);
                    break;
                case ZombieState.Alert:
                    Hold(ref c, AtEyes(alertStimulus), AlertArms);
                    break;
                case ZombieState.Investigate:
                    if (investigateLooking) Hold(ref c, LookAround(p), AlertArms);
                    else MoveTo(ref c, investigatePoint, tuning.InvestigateSpeed * SpeedFactor, AlertArms);
                    break;
                case ZombieState.Search:
                    if (searchPausing) Hold(ref c, LookAround(p), AlertArms);
                    else MoveTo(ref c, searchPoint, tuning.SearchSpeed * SpeedFactor, AlertArms);
                    break;
                case ZombieState.Chase:
                    MoveTo(ref c, chaseDestination, ChaseSpeed, ChaseArms);
                    break;
                case ZombieState.Attack:
                    WriteAttack(ref c);
                    break;
                case ZombieState.Feed:
                    Hold(ref c, p.TargetPosition, FeedArms);
                    c.Pose.Feed = 1f;
                    break;
            }
        }

        void MoveTo(ref BrainCommands c, Vector3 destination, float speed, float arms)
        {
            c.HasDestination = true;
            c.Destination = destination;
            c.Speed = speed;
            c.LookPoint = AtEyes(destination);
            c.Pose.ArmsForward = arms;
        }

        static void Hold(ref BrainCommands c, Vector3 lookPoint, float arms)
        {
            c.Stop = true;
            c.LookPoint = lookPoint;
            c.Pose.ArmsForward = arms;
        }

        Vector3 LookAround(in PerceptionSnapshot p)
        {
            var yaw = LookAroundAngle * Mathf.Sin(lookTimer * (2f * Mathf.PI / LookAroundPeriod));
            return AtEyes(p.Position + Quaternion.AngleAxis(yaw, Vector3.up) * lookBase * LookDistance);
        }

        Vector3 AtEyes(Vector3 groundPoint) => groundPoint + Vector3.up * tuning.EyeHeight;

        static Vector3 Planar(Vector3 v)
        {
            v.y = 0f;
            return v.normalized;
        }

        static float PlanarDistance(Vector3 a, Vector3 b)
        {
            var offset = a - b;
            offset.y = 0f;
            return offset.magnitude;
        }

        float Range(float min, float max) => min + (max - min) * (float)random.NextDouble();

        float Signed() => (float)(random.NextDouble() * 2.0 - 1.0);
    }
}
