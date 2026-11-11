using DeadCounty.Perception;
using UnityEngine;

namespace DeadCounty.AI
{
    // Wandering, alert and investigation: everything before the zombie commits to a chase.
    public partial class ZombieBrain
    {
        Vector3 wanderPoint;
        float idleRemaining;

        Vector3 alertStimulus;
        float alertMinTime;
        bool alertHeard;
        bool alertProximity;

        Vector3 investigatePoint;
        bool investigateLooking;

        void UpdateCalm(float dt, in PerceptionSnapshot p, IZombieWorld world)
        {
            if (ReactToTarget(p)) return;
            if (p.HeardNoise)
            {
                var called = p.NoiseKind == NoiseKind.ZombieCall;
                EnterAlert(called ? TransitionReason.Called : TransitionReason.HeardNoise, p.NoisePosition, called ? tuning.CalledAlertTime : tuning.MinAlertTime);
                return;
            }
            if (State == ZombieState.Idle)
            {
                idleRemaining -= dt;
                if (idleRemaining <= 0f) PickCalmActivity(world);
            }
            else if (p.ArrivedAtDestination || p.PathFailed) PickCalmActivity(world);
        }

        void PickCalmActivity(IZombieWorld world)
        {
            if (random.NextDouble() < tuning.IdleChance)
            {
                BeIdle(Range(tuning.IdleMin, tuning.IdleMax));
                return;
            }
            var a = random.NextDouble();
            var b = random.NextDouble();
            if (world.TryRandomPoint(Anchor, tuning.WanderRadius, a, b, out var point) && PlanarDistance(point, Anchor) <= tuning.LeashRadius)
            {
                if (State != ZombieState.Wander) Transition(ZombieState.Wander, TransitionReason.IdleDone);
                wanderPoint = point;
            }
            else BeIdle(tuning.IdleMin);
        }

        void BeIdle(float duration)
        {
            if (State != ZombieState.Idle) Transition(ZombieState.Idle, TransitionReason.Arrived);
            idleRemaining = duration;
        }

        void ReturnToAnchor(TransitionReason reason)
        {
            Transition(ZombieState.Wander, reason);
            wanderPoint = Anchor;
        }

        // Shared by every state that has not committed to the target yet. Proximity wins over sight.
        bool ReactToTarget(in PerceptionSnapshot p)
        {
            if (!p.TargetAlive) return false;
            if (p.InProximity) EnterAlert(TransitionReason.Proximity, p.TargetPosition, tuning.ProximitySnarl);
            else if (p.TargetVisible && Suspicion >= tuning.AlertThreshold) EnterAlert(TransitionReason.SawTarget, p.TargetPosition, tuning.MinAlertTime);
            else return false;
            return true;
        }

        void EnterAlert(TransitionReason reason, Vector3 stimulus, float minTime)
        {
            Transition(ZombieState.Alert, reason);
            alertStimulus = stimulus;
            alertMinTime = minTime;
            alertProximity = reason == TransitionReason.Proximity;
            alertHeard = reason is TransitionReason.HeardNoise or TransitionReason.Called;
        }

        void UpdateAlert(in PerceptionSnapshot p)
        {
            var perceived = Perceives(p);
            if (perceived) alertStimulus = p.TargetPosition;
            else if (p.HeardNoise) alertStimulus = p.NoisePosition;
            alertHeard |= p.HeardNoise;
            if (p.TargetAlive && p.InProximity && !alertProximity)
            {
                alertProximity = true;
                alertMinTime = Mathf.Min(alertMinTime, tuning.ProximitySnarl);
            }

            // The minimum alert is the telegraph: nothing, not even full suspicion, cuts it short.
            if (StateTime < alertMinTime - Epsilon) return;
            if (alertProximity)
            {
                Suspicion = 1f;
                EnterChase(TransitionReason.Proximity, true);
            }
            else if (Suspicion >= tuning.ChaseThreshold) EnterChase(TransitionReason.SawTarget, true);
            else if (alertHeard && !perceived) EnterInvestigate(TransitionReason.AlertTimeout, alertStimulus);
            else if (!alertHeard && Suspicion < CalmSuspicion) ReturnToAnchor(TransitionReason.AlertTimeout);
        }

        void EnterInvestigate(TransitionReason reason, Vector3 point)
        {
            Transition(ZombieState.Investigate, reason);
            investigatePoint = point;
            investigateLooking = false;
        }

        void UpdateInvestigate(float dt, in PerceptionSnapshot p, IZombieWorld world)
        {
            if (ReactToTarget(p)) return;
            if (p.HeardNoise)
            {
                investigatePoint = p.NoisePosition;
                investigateLooking = false;
                return;
            }
            if (!investigateLooking)
            {
                if (p.ArrivedAtDestination || p.PathFailed)
                {
                    investigateLooking = true;
                    StartLookAround(p);
                }
                return;
            }
            lookTimer += dt;
            if (lookTimer >= tuning.InvestigateLookTime - Epsilon) EnterSearch(TransitionReason.Arrived, p, world, false);
        }
    }
}
