using DeadCounty.AI;
using NUnit.Framework;
using UnityEngine;

namespace DeadCounty.Tests
{
    // Chase, search and attack rules of the zombie brain. The attack timeline is the one DodgeSimulationTests
    // validates against the player's locomotion: wind-up 1 s, lunge 0.2 s, recover 0.6 / 0.9 s, cooldown 0.4 s.
    public class ZombieBrainChaseTests
    {
        const float Dt = BrainSim.Dt;
        static readonly ZombieTuning Tuning = ZombieTuning.Standard;
        static readonly AttackSettings Attack = AttackSettings.Default;

        [Test]
        public void LostTargetIsTrackedThenPredictedThenSearchedThenForgotten()
        {
            var sim = new BrainSim { MoveBody = false, TargetVisible = true };
            sim.PlaceTarget(4f);
            sim.RunUntil(ZombieState.Chase, 5f);
            sim.MoveBody = true;
            sim.MoveTarget = true;
            sim.TargetVelocity = Vector3.right * 1.5f;
            sim.Run(1f);
            Assert.That(sim.State, Is.EqualTo(ZombieState.Chase));
            Assert.That(sim.Commands.Speed, Is.EqualTo(Tuning.ChaseSpeed * sim.Brain.SpeedFactor).Within(1e-5f));
            Assert.That(sim.Commands.Pose.ArmsForward, Is.EqualTo(0.8f));

            sim.TargetVisible = false;
            var lostFrame = sim.Frame;
            for (var i = 0; i < 60; i++)
            {
                sim.Step();
                BrainSim.AssertNear(sim.Commands.Destination, sim.TargetPosition, $"tracking frame {i}:");
            }
            var lastKnown = sim.TargetPosition;
            var lastVelocity = sim.TargetVelocity;
            BrainSim.AssertNear(sim.Brain.LastKnownPosition, lastKnown);

            // Out of the zombie's knowledge, the target turns away.
            sim.TargetVelocity = Vector3.back * 1.5f;
            for (var i = 1; i <= 120; i++)
            {
                sim.Step();
                var predicted = lastKnown + lastVelocity * Mathf.Min(i * Dt, Tuning.PredictionHorizon);
                BrainSim.AssertNear(sim.Commands.Destination, predicted, $"prediction frame {i}:");
            }
            Assert.That(Vector3.Distance(sim.Commands.Destination, sim.TargetPosition), Is.GreaterThan(2f));

            var searchFrame = sim.RunUntil(ZombieState.Search, 20f);
            Assert.That(sim.Last.Transition.Reason, Is.EqualTo(TransitionReason.LostTarget));
            Assert.That((searchFrame - lostFrame + 1) * Dt, Is.GreaterThanOrEqualTo(Tuning.GiveUpTime - 1e-3f));
            var firstPoint = sim.Commands.Destination - lastKnown;
            Assert.That(firstPoint.x, Is.InRange(3f - 1f, 5f + 1f), "first search point ahead along the last heading");

            var wanderFrame = sim.RunUntil(ZombieState.Wander, 20f);
            Assert.That(sim.Last.Transition.Reason, Is.EqualTo(TransitionReason.SearchDone));
            Assert.That((wanderFrame - searchFrame) * Dt, Is.EqualTo(Tuning.SearchDuration).Within(1e-3f));
            BrainSim.AssertNear(sim.Commands.Destination, sim.Brain.Anchor);
        }

        [TestCase(1.5f, 0f, 0f, true)]
        [TestCase(1.5f, 44f, 0f, true)]
        [TestCase(1.5f, -44f, 0f, true)]
        [TestCase(1.5f, 46f, 0f, false)]
        [TestCase(1.5f, -46f, 0f, false)]
        [TestCase(1.7f, 0f, 0f, false)]
        [TestCase(1.1f, 0f, 1.5f, true)]
        [TestCase(1.2f, 0f, 1.5f, false)]
        public void AttackTriggersWithinDistanceAndAngle(float distance, float yawDeg, float awaySpeed, bool expected)
        {
            var sim = BrainSim.Chasing();
            sim.TargetVisible = true;
            sim.PlaceTarget(distance, yawDeg, awaySpeed);
            var attacked = false;
            for (var i = 0; i < 30; i++)
            {
                sim.Step();
                attacked |= sim.State == ZombieState.Attack;
            }
            Assert.That(attacked, Is.EqualTo(expected));
        }

        [Test]
        public void HiddenTargetIsNeverAttacked()
        {
            var sim = BrainSim.Chasing();
            sim.PlaceTarget(1.2f);
            sim.Run(0.5f);
            Assert.That(sim.State, Is.EqualTo(ZombieState.Chase));
        }

        [Test]
        public void WaitsForItsTurnWhileAnotherZombieWindsUp()
        {
            var sim = BrainSim.Chasing();
            sim.TargetVisible = true;
            sim.World.AttackTurnTaken = true;
            sim.PlaceTarget(1.2f);
            sim.Run(0.5f);
            Assert.That(sim.State, Is.EqualTo(ZombieState.Chase));

            sim.World.AttackTurnTaken = false;
            sim.Step();
            Assert.That(sim.State, Is.EqualTo(ZombieState.Attack));
            Assert.That(sim.World.LastTurnInterval, Is.EqualTo(Attack.TurnInterval));
        }

        [TestCase(true, 0.6f)]
        [TestCase(false, 0.9f)]
        public void AttackTimelineStrikesOnceThenRecoversThenCoolsDown(bool reported, float recoverTime)
        {
            var sim = BrainSim.Chasing();
            sim.TargetVisible = true;
            sim.ReportStrikes = reported;
            sim.PlaceTarget(1.2f);
            var forward = sim.Forward;
            var start = sim.RunUntil(ZombieState.Attack, 1f);
            Assert.That(sim.Last.Transition.Reason, Is.EqualTo(TransitionReason.AttackStart));
            Assert.That(sim.Commands.AttackPhase, Is.EqualTo(AttackPhase.Windup));
            Assert.That(sim.Commands.HasDestination);
            BrainSim.AssertNear(sim.Commands.Destination, sim.TargetPosition);
            Assert.That(sim.Commands.Pose.ArmsForward, Is.LessThan(0.82f));

            int lunge = -1, strike = -1, recover = -1, chase = -1, next = -1, strikes = 0;
            for (var i = 0; i < 300 && next < 0; i++)
            {
                sim.Step();
                var frame = sim.Frame - 1;
                var c = sim.Commands;
                if (frame - start == Mathf.RoundToInt(Attack.ArmsUpAt / Dt) - 1) Assert.That(c.Pose.ArmsForward, Is.EqualTo(1f).Within(1e-4f), "arms up");
                if (c.StrikeNow)
                {
                    strikes++;
                    strike = frame;
                }
                if (lunge < 0 && c.AttackPhase == AttackPhase.Lunge)
                {
                    lunge = frame;
                    Assert.That(c.HasDestination || c.Stop, Is.False);
                    Assert.That(c.Pose.Lunge, Is.EqualTo(1f));
                    Assert.That(c.Speed, Is.EqualTo(Attack.LungeDistance / Attack.LungeDuration).Within(1e-4f));
                    BrainSim.AssertNear(c.LungeDirection, forward);
                    // The direction is locked at the start of the lunge.
                    sim.Forward = Vector3.right;
                }
                if (lunge >= 0 && c.AttackPhase == AttackPhase.Lunge) BrainSim.AssertNear(c.LungeDirection, forward);
                if (recover < 0 && c.AttackPhase == AttackPhase.Recover)
                {
                    recover = frame;
                    Assert.That(c.Stop);
                    sim.Forward = forward;
                }
                if (chase < 0 && sim.State == ZombieState.Chase)
                {
                    chase = frame;
                    Assert.That(sim.Last.Transition.Reason, Is.EqualTo(TransitionReason.AttackDone));
                    Assert.That(c.EmitCall, Is.False);
                }
                if (chase >= 0 && sim.State == ZombieState.Attack) next = frame;
            }
            Assert.That(strikes, Is.EqualTo(1));
            Assert.That((lunge - start) * Dt, Is.EqualTo(Attack.Windup).Within(1e-3f));
            Assert.That((strike - start + 1) * Dt, Is.EqualTo(Attack.Windup + Attack.LungeDuration).Within(1e-3f));
            Assert.That(recover, Is.EqualTo(strike + 1));
            Assert.That((chase - recover) * Dt, Is.EqualTo(recoverTime).Within(1e-3f));
            Assert.That((next - chase) * Dt, Is.EqualTo(Attack.Cooldown).Within(1e-3f));
        }

        [Test]
        public void TargetHiddenAfterTheStrikeIsSearched()
        {
            var sim = BrainSim.Chasing();
            sim.TargetVisible = true;
            sim.PlaceTarget(1.2f);
            sim.RunUntil(ZombieState.Attack, 1f);
            for (var i = 0; i < 120 && !sim.Commands.StrikeNow; i++) sim.Step();
            Assert.That(sim.Commands.StrikeNow);
            sim.TargetVisible = false;
            sim.RunUntil(ZombieState.Search, 2f);
            Assert.That(sim.Last.Transition.From, Is.EqualTo(ZombieState.Attack));
            Assert.That(sim.Last.Transition.Reason, Is.EqualTo(TransitionReason.LostTarget));
        }

        [Test]
        public void SearchingZombieFollowsANoiseAtOnce()
        {
            var sim = BrainSim.Chasing();
            sim.MoveBody = true;
            sim.RunUntil(ZombieState.Search, 10f);
            var noise = sim.Position + new Vector3(4f, 0f, 2f);
            sim.Hear(noise);
            sim.Step();
            Assert.That(sim.State, Is.EqualTo(ZombieState.Investigate));
            Assert.That(sim.Last.Transition.Reason, Is.EqualTo(TransitionReason.HeardNoise));
            BrainSim.AssertNear(sim.Commands.Destination, noise);
        }
    }
}
