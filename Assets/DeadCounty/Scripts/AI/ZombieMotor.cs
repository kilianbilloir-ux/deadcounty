using System;
using DeadCounty.Locomotion;
using UnityEngine;
using UnityEngine.AI;

namespace DeadCounty.AI
{
    // Moves a zombie body along NavMesh paths with the shared PlanarLocomotion physics. The NavMeshAgent only plans
    // paths and avoidance: the body follows its desired velocity at its own acceleration and turn rate, always
    // along its heading, then goes back through nextPosition, which keeps it on the mesh at the mesh height.
    public class ZombieMotor
    {
        const float WanderStoppingDistance = 0.4f;
        const float ChaseStoppingDistance = 0.9f;
        const float ArriveTolerance = 0.05f;
        const float RepathDistance = 0.5f;
        const float ChaseRepathInterval = 0.25f;
        const float RepathInterval = 1f;
        const float StillDestination = 0.01f;
        // Below this the desired velocity has no usable direction (arrived, path pending).
        const float MinSteerSpeed = 0.05f;
        const float MinLookDistance = 0.1f;
        const float AvoidanceRadius = 0.25f;
        const int MinAvoidancePriority = 40;
        const int AvoidancePriorityRange = 21;
        const float AgentAcceleration = 100f;
        const float MinAgentSpeed = 0.5f;
        const float StuckMinSpeed = 0.2f;
        const float StuckDistance = 0.3f;
        const float StuckTime = 3f;
        const float WarpSearchRadius = 2f;

        readonly NavMeshAgent agent;
        readonly Transform body;
        readonly LocomotionSettings locomotion;
        readonly AttackSettings attack;
        LocomotionState state;
        Vector3 requestedDestination;
        bool destinationSet;
        bool requestFailed;
        float sinceRepath;
        Vector3 stuckOrigin;
        float stuckTimer;

        public ZombieMotor(NavMeshAgent agent, Transform body, in LocomotionSettings locomotion, in AttackSettings attack, int seed)
        {
            this.agent = agent;
            this.body = body;
            this.locomotion = locomotion;
            this.attack = attack;
            agent.updatePosition = false;
            agent.updateRotation = false;
            agent.autoBraking = false;
            agent.baseOffset = 0f;
            agent.acceleration = AgentAcceleration;
            agent.radius = AvoidanceRadius;
            agent.stoppingDistance = WanderStoppingDistance;
            // Unequal priorities let crowds at a doorway resolve instead of pushing each other symmetrically.
            agent.avoidancePriority = MinAvoidancePriority + (seed % AvoidancePriorityRange + AvoidancePriorityRange) % AvoidancePriorityRange;
            state = LocomotionState.Facing(body.forward);
            Position = body.position;
            stuckOrigin = Position;
        }

        public Vector3 Position { get; private set; }
        public Vector3 Heading => state.Heading;
        // Planar displacement of the last Move over its dt, after the NavMesh constrained it.
        public Vector3 Velocity { get; private set; }
        public float MeasuredSpeed { get; private set; }
        public bool Arrived { get; private set; }
        public bool PathFailed { get; private set; }

        // Returns true when the body was warped (stuck recovery): animators must snap rather than read it as motion.
        public bool Move(in BrainCommands c, bool chasing, Vector3 targetPosition, bool hasTarget, float dt)
        {
            var start = Position;
            var lunging = c.AttackPhase == AttackPhase.Lunge;
            agent.stoppingDistance = chasing ? ChaseStoppingDistance : WanderStoppingDistance;
            // Only the direction of the desired velocity is used; matching the speed keeps avoidance sampling at
            // the zombie's own scale.
            agent.speed = Mathf.Max(c.Speed, MinAgentSpeed);
            if (c.HasDestination && !lunging) RequestPath(c.Destination, chasing, dt);
            else ClearPath();

            if (lunging) Lunge(c, targetPosition, hasTarget, dt);
            else Walk(c, dt);

            agent.nextPosition = Position;
            Position = agent.nextPosition;
            body.SetPositionAndRotation(Position, Quaternion.LookRotation(state.Heading));
            var displacement = Position - start;
            displacement.y = 0f;
            Velocity = displacement / dt;
            MeasuredSpeed = Velocity.magnitude;
            // Neighbours predict this body from the agent velocity, so it must be the real one.
            agent.velocity = Velocity;

            Arrived = c.HasDestination && destinationSet && !requestFailed && !agent.pathPending
                && agent.remainingDistance <= agent.stoppingDistance + ArriveTolerance;
            PathFailed = c.HasDestination && (requestFailed || (!agent.pathPending && agent.pathStatus == NavMeshPathStatus.PathInvalid));
            return DetectStuck(c, dt);
        }

        // Dormant or dead: no path, no motion, the agent stays as a still obstacle for the others.
        public void Halt()
        {
            ClearPath();
            state.Speed = 0f;
            state.PlanarVelocity = Vector3.zero;
            Velocity = Vector3.zero;
            MeasuredSpeed = 0f;
            Arrived = false;
            PathFailed = false;
            stuckOrigin = Position;
            stuckTimer = 0f;
            if (agent.isOnNavMesh) agent.velocity = Vector3.zero;
        }

        public void SetDormant(bool dormant)
        {
            if (dormant) Halt();
            if (agent.isOnNavMesh) agent.isStopped = dormant;
        }

        public void Reset(Vector3 position, Quaternion rotation)
        {
            Halt();
            if (!agent.Warp(position)) throw new InvalidOperationException($"{body.name}: no NavMesh near {position} to warp to");
            agent.isStopped = false;
            state = LocomotionState.Facing(rotation * Vector3.forward);
            Position = agent.nextPosition;
            stuckOrigin = Position;
            body.SetPositionAndRotation(Position, Quaternion.LookRotation(state.Heading));
        }

        void RequestPath(Vector3 destination, bool chasing, float dt)
        {
            sinceRepath += dt;
            var moved = PlanarDistance(destination, requestedDestination);
            // A still destination keeps its path: the corridor already follows avoidance detours.
            var due = !destinationSet || moved > RepathDistance
                || (sinceRepath >= (chasing ? ChaseRepathInterval : RepathInterval) && (moved > StillDestination || requestFailed));
            if (!due) return;
            destinationSet = true;
            requestedDestination = destination;
            sinceRepath = 0f;
            requestFailed = !agent.SetDestination(destination);
        }

        void ClearPath()
        {
            if (!destinationSet) return;
            destinationSet = false;
            requestFailed = false;
            if (agent.isOnNavMesh) agent.ResetPath();
        }

        void Walk(in BrainCommands c, float dt)
        {
            var desired = agent.desiredVelocity;
            desired.y = 0f;
            Vector3 wish;
            float targetSpeed;
            if (c.HasDestination && !c.Stop && desired.sqrMagnitude > MinSteerSpeed * MinSteerSpeed)
            {
                wish = desired.normalized;
                targetSpeed = c.Speed;
            }
            else
            {
                // Standing still the body turns to face what it looks at.
                wish = FacingWish(c);
                targetSpeed = 0f;
            }
            var settings = locomotion;
            if (c.AttackPhase == AttackPhase.Windup)
            {
                settings.StandingTurnRate = Mathf.Min(settings.StandingTurnRate, attack.WindupTurnRate);
                settings.MovingTurnRate = Mathf.Min(settings.MovingTurnRate, attack.WindupTurnRate);
            }
            PlanarLocomotion.Step(ref state, settings, wish, targetSpeed, true, true, dt);
            Position += state.PlanarVelocity * dt;
        }

        Vector3 FacingWish(in BrainCommands c)
        {
            if (!c.HasLookPoint) return state.Heading;
            var toLook = c.LookPoint - Position;
            toLook.y = 0f;
            return toLook.sqrMagnitude > MinLookDistance * MinLookDistance ? toLook.normalized : state.Heading;
        }

        // The lunge is a scripted dash along the locked direction that never ends closer than ContactDistance to
        // the target; the body arrives planted, without walking momentum.
        void Lunge(in BrainCommands c, Vector3 targetPosition, bool hasTarget, float dt)
        {
            var direction = c.LungeDirection;
            direction.y = 0f;
            direction = direction.sqrMagnitude > 1e-6f ? direction.normalized : state.Heading;
            var step = c.Speed * dt;
            if (hasTarget) step = StepBeforeContact(targetPosition - Position, direction, step, attack.ContactDistance);
            state.Heading = direction;
            state.Speed = 0f;
            state.PlanarVelocity = Vector3.zero;
            Position += direction * step;
        }

        // Longest advance along direction, up to step, that keeps the planar distance to the target at least contact.
        static float StepBeforeContact(Vector3 toTarget, Vector3 direction, float step, float contact)
        {
            toTarget.y = 0f;
            var along = Vector3.Dot(toTarget, direction);
            if (along <= 0f) return step;
            var clearance = toTarget.sqrMagnitude - contact * contact;
            if (clearance <= 0f) return 0f;
            var discriminant = along * along - clearance;
            return discriminant < 0f ? step : Mathf.Min(step, along - Mathf.Sqrt(discriminant));
        }

        bool DetectStuck(in BrainCommands c, float dt)
        {
            var trying = c.HasDestination && !c.Stop && c.Speed > StuckMinSpeed && !Arrived && c.AttackPhase != AttackPhase.Lunge;
            if (!trying || PlanarDistance(Position, stuckOrigin) > StuckDistance)
            {
                stuckOrigin = Position;
                stuckTimer = 0f;
                return false;
            }
            stuckTimer += dt;
            if (stuckTimer < StuckTime) return false;

            Debug.Log($"[AI] stuck {body.name} at {Position} on its way to {requestedDestination}, warping to the NavMesh");
            var destination = Position;
            if (NavMesh.SamplePosition(Position, out var hit, WarpSearchRadius, NavMesh.AllAreas)) destination = hit.position;
            Halt();
            if (!agent.Warp(destination)) throw new InvalidOperationException($"{body.name}: no NavMesh near {destination} to warp to");
            Position = agent.nextPosition;
            stuckOrigin = Position;
            body.position = Position;
            // The brain reads it as a failed path and picks another goal.
            PathFailed = true;
            return true;
        }

        static float PlanarDistance(Vector3 a, Vector3 b)
        {
            var offset = a - b;
            offset.y = 0f;
            return offset.magnitude;
        }
    }
}
