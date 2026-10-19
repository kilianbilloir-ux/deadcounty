using DeadCounty.Perception;
using UnityEngine;

namespace DeadCounty.AI
{
    // Chase, attack and search: the zombie has committed to its target.
    public partial class ZombieBrain
    {
        const float SearchAheadMin = 3f;
        const float SearchAheadMax = 5f;
        const float SearchAheadJitter = 1f;

        Vector3 chaseDestination;

        float attackElapsed;
        float recoverDuration;
        Vector3 lungeDirection;
        bool awaitingStrikeReport;
        bool strikeHit;

        Vector3 searchCenter;
        Vector3 searchDirection;
        float searchDuration;
        int searchPointCount;
        int searchPointIndex;
        float searchRadiusStart;
        float searchRadiusEnd;
        bool searchAhead;
        Vector3 searchPoint;
        bool searchPausing;
        float pauseRemaining;

        void EnterChase(TransitionReason reason, bool growl)
        {
            Transition(ZombieState.Chase, reason);
            chaseDestination = LastKnownPosition;
            if (growl && sinceCall >= tuning.CallCooldown - Epsilon)
            {
                callThisTick = true;
                sinceCall = 0f;
            }
        }

        void UpdateChase(float dt, in PerceptionSnapshot p, IZombieWorld world)
        {
            if (CanStartAttack(p))
            {
                EnterAttack(dt, p, world);
                return;
            }
            var perceived = Perceives(p);
            // Momentum around corners: for TrackTime after losing sight the zombie still knows where the target went.
            var tracking = !perceived && sinceSeen <= tuning.TrackTime + Epsilon;
            if (tracking) Know(p.TargetPosition, p.TargetVelocity);
            // Another zombie's call says nothing about where the target is.
            else if (!perceived && p.HeardNoise && p.NoiseKind != NoiseKind.ZombieCall) Know(p.NoisePosition, Vector3.zero);
            chaseDestination = perceived || tracking
                ? p.TargetPosition
                : LastKnownPosition + LastKnownVelocity * Mathf.Min(sinceKnown, tuning.PredictionHorizon);
            if (sinceSeen >= tuning.GiveUpTime - Epsilon && (p.ArrivedAtDestination || p.PathFailed))
                EnterSearch(TransitionReason.LostTarget, p, world, true);
        }

        bool CanStartAttack(in PerceptionSnapshot p)
        {
            if (!p.TargetVisible || attackCooldown > Epsilon) return false;
            var toTarget = p.TargetPosition - p.Position;
            toTarget.y = 0f;
            var planarDistance = toTarget.magnitude;
            var awaySpeed = planarDistance > Epsilon ? Vector3.Dot(p.TargetVelocity, toTarget / planarDistance) : 0f;
            return p.TargetDistance <= AttackResolver.TriggerDistance(tuning.Attack, awaySpeed)
                && Vector3.Angle(Planar(p.Forward), toTarget) <= tuning.Attack.TriggerHalfAngle;
        }

        // The trigger frame is the first wind-up frame, so the attack timeline matches DodgeSimulationTests.
        void EnterAttack(float dt, in PerceptionSnapshot p, IZombieWorld world)
        {
            Transition(ZombieState.Attack, TransitionReason.AttackStart);
            AttackPhase = AttackPhase.Windup;
            attackElapsed = 0f;
            UpdateAttack(dt, p, world);
        }

        // attackElapsed is the time into the current phase at the end of this frame; overshoot carries over.
        void UpdateAttack(float dt, in PerceptionSnapshot p, IZombieWorld world)
        {
            ref readonly var a = ref tuning.Attack;
            attackElapsed += dt;
            if (AttackPhase == AttackPhase.Windup && attackElapsed > a.Windup + Epsilon)
            {
                attackElapsed -= a.Windup;
                AttackPhase = AttackPhase.Lunge;
                lungeDirection = Planar(p.Forward);
            }
            if (AttackPhase == AttackPhase.Lunge)
            {
                if (awaitingStrikeReport)
                {
                    awaitingStrikeReport = false;
                    attackElapsed -= a.LungeDuration;
                    recoverDuration = strikeHit ? a.RecoverAfterHit : a.RecoverAfterMiss;
                    AttackPhase = AttackPhase.Recover;
                }
                else if (attackElapsed >= a.LungeDuration - Epsilon)
                {
                    strikeThisTick = true;
                    awaitingStrikeReport = true;
                    strikeHit = false;
                }
            }
            if (AttackPhase == AttackPhase.Recover && attackElapsed > recoverDuration + Epsilon)
            {
                attackCooldown = a.Cooldown;
                if (Perceives(p)) EnterChase(TransitionReason.AttackDone, false);
                else EnterSearch(TransitionReason.LostTarget, p, world, true);
            }
        }

        void WriteAttack(ref BrainCommands c)
        {
            ref readonly var a = ref tuning.Attack;
            switch (AttackPhase)
            {
                case AttackPhase.Windup:
                    MoveTo(ref c, LastKnownPosition, ChaseSpeed, Mathf.Lerp(ChaseArms, GrabArms, attackElapsed / a.ArmsUpAt));
                    c.AttackProgress = Mathf.Clamp01(attackElapsed / a.Windup);
                    break;
                case AttackPhase.Lunge:
                    c.Speed = a.LungeDistance / a.LungeDuration;
                    c.LookPoint = AtEyes(LastKnownPosition);
                    c.Pose.ArmsForward = GrabArms;
                    c.Pose.Lunge = 1f;
                    c.AttackProgress = Mathf.Clamp01(attackElapsed / a.LungeDuration);
                    break;
                case AttackPhase.Recover:
                    Hold(ref c, AtEyes(LastKnownPosition), ChaseArms);
                    c.AttackProgress = Mathf.Clamp01(attackElapsed / recoverDuration);
                    break;
            }
        }

        // A lost target is searched around its last known position, starting ahead along its last heading; an
        // investigation ends with a short search around the noise.
        void EnterSearch(TransitionReason reason, in PerceptionSnapshot p, IZombieWorld world, bool lostTarget)
        {
            Transition(ZombieState.Search, reason);
            searchPointIndex = 0;
            if (lostTarget)
            {
                searchCenter = LastKnownPosition;
                searchDirection = Planar(LastKnownVelocity);
                // A target last heard or seen standing: keep going the way the zombie was heading.
                if (searchDirection == Vector3.zero) searchDirection = Planar(LastKnownPosition - p.Position);
                searchDuration = tuning.SearchDuration;
                searchPointCount = tuning.SearchPoints;
                searchRadiusStart = tuning.SearchRadiusStart;
                searchRadiusEnd = tuning.SearchRadiusEnd;
            }
            else
            {
                searchCenter = investigatePoint;
                searchDirection = Vector3.zero;
                searchDuration = tuning.InvestigateSearchDuration;
                searchPointCount = tuning.InvestigateSearchPoints;
                searchRadiusStart = tuning.InvestigateSearchRadius;
                searchRadiusEnd = tuning.InvestigateSearchRadius;
            }
            searchAhead = searchDirection != Vector3.zero;
            NextSearchPoint(p, world);
        }

        void UpdateSearch(float dt, in PerceptionSnapshot p, IZombieWorld world)
        {
            if (ReactToTarget(p)) return;
            // Already hunting: a noise is followed at once, without a new alert.
            if (p.HeardNoise)
            {
                EnterInvestigate(TransitionReason.HeardNoise, p.NoisePosition);
                return;
            }
            if (StateTime >= searchDuration - Epsilon)
            {
                ReturnToAnchor(TransitionReason.SearchDone);
                return;
            }
            if (!searchPausing)
            {
                if (p.ArrivedAtDestination || p.PathFailed) PauseSearch(p);
                return;
            }
            lookTimer += dt;
            pauseRemaining -= dt;
            if (pauseRemaining > 0f) return;
            if (searchPointIndex >= searchPointCount) ReturnToAnchor(TransitionReason.SearchDone);
            else NextSearchPoint(p, world);
        }

        void NextSearchPoint(in PerceptionSnapshot p, IZombieWorld world)
        {
            var index = searchPointIndex++;
            var a = random.NextDouble();
            var b = random.NextDouble();
            bool found;
            if (index == 0 && searchAhead)
            {
                var ahead = searchCenter + searchDirection * Range(SearchAheadMin, SearchAheadMax);
                found = world.TryRandomPoint(ahead, SearchAheadJitter, a, b, out searchPoint);
            }
            else
            {
                var first = searchAhead ? 1 : 0;
                var randomCount = searchPointCount - first;
                var radius = Mathf.Lerp(searchRadiusStart, searchRadiusEnd, randomCount > 1 ? (float)(index - first) / (randomCount - 1) : 0f);
                if (searchAhead)
                {
                    // Most points lie on the side the target was heading, some deliberately behind: a fallible guess.
                    var side = random.NextDouble() < tuning.WrongSideChance ? -1f : 1f;
                    found = world.TryRandomPoint(searchCenter + searchDirection * (side * radius * 0.5f), radius * 0.5f, a, b, out searchPoint);
                }
                else found = world.TryRandomPoint(searchCenter, radius, a, b, out searchPoint);
            }
            if (found) searchPausing = false;
            else PauseSearch(p);
        }

        void PauseSearch(in PerceptionSnapshot p)
        {
            searchPausing = true;
            pauseRemaining = Range(tuning.SearchPauseMin, tuning.SearchPauseMax);
            StartLookAround(p);
        }
    }
}
