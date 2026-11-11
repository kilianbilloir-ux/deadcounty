using DeadCounty.AI;
using DeadCounty.Perception;
using NUnit.Framework;
using UnityEngine;

namespace DeadCounty.Tests
{
    // Calm, alert, noise, proximity, feeding and reset rules of the zombie brain, at 60 Hz with a fake world.
    public class ZombieBrainTests
    {
        const float Dt = BrainSim.Dt;
        static readonly ZombieTuning Tuning = ZombieTuning.Standard;

        [Test]
        public void CalmZombieNeverLeavesItsLeash()
        {
            var anchor = new Vector3(10f, 0f, -5f);
            // Points up to 12.8 m away: the brain must reject the ones past the 12 m leash itself.
            var sim = new BrainSim(3, anchor);
            sim.World.Scale = 1.6f;
            var idleFrames = 0;
            var wanderFrames = 0;
            var maxDistance = 0f;
            for (var i = 0; i < 60 * 600; i++)
            {
                sim.Step();
                Assert.That(sim.State, Is.EqualTo(ZombieState.Idle).Or.EqualTo(ZombieState.Wander));
                if (sim.State == ZombieState.Idle) idleFrames++;
                else wanderFrames++;
                maxDistance = Mathf.Max(maxDistance, BrainSim.PlanarDistance(sim.Position, anchor));
            }
            TestContext.WriteLine($"idle {idleFrames * Dt:0} s, wander {wanderFrames * Dt:0} s, max distance {maxDistance:0.00} m");
            Assert.That(maxDistance, Is.LessThanOrEqualTo(Tuning.LeashRadius));
            Assert.That(maxDistance, Is.GreaterThan(Tuning.WanderRadius * 0.5f));
            Assert.That(idleFrames, Is.GreaterThan(0));
            Assert.That(wanderFrames, Is.GreaterThan(0));
            BrainSim.AssertNear(sim.World.LastCenter, anchor);
            Assert.That(sim.World.LastRadius, Is.EqualTo(Tuning.WanderRadius));
        }

        [Test]
        public void SightAt15MetresAlertsAtThresholdAndChasesOnlyAfterMinAlertTime()
        {
            var sim = new BrainSim { MoveBody = false, TargetVisible = true };
            sim.PlaceTarget(15f);
            var alertFrame = sim.RunUntil(ZombieState.Alert, 5f);
            Assert.That(sim.Last.Transition.Reason, Is.EqualTo(TransitionReason.SawTarget));
            Assert.That(sim.Brain.Suspicion, Is.GreaterThanOrEqualTo(Tuning.AlertThreshold));
            var expectedFill = Tuning.AlertThreshold * PerceptionMath.SuspicionFillTime(15f, 20f) / PerceptionMath.SpeedSuspicionMultiplier(0f);
            Assert.That((alertFrame + 1) * Dt, Is.EqualTo(expectedFill).Within(Dt));

            var fullAt = -1;
            for (var i = 0; i < 600 && sim.State == ZombieState.Alert; i++)
            {
                sim.Step();
                if (fullAt < 0 && sim.Brain.Suspicion >= 1f) fullAt = sim.Frame - 1;
            }
            var chaseFrame = sim.Frame - 1;
            Assert.That(sim.State, Is.EqualTo(ZombieState.Chase));
            Assert.That(sim.Last.Transition.Reason, Is.EqualTo(TransitionReason.SawTarget));
            Assert.That(fullAt, Is.GreaterThan(alertFrame).And.LessThan(chaseFrame), "suspicion should be full well before the alert ends");
            Assert.That((chaseFrame - alertFrame) * Dt, Is.EqualTo(Tuning.MinAlertTime).Within(1e-3f));
            Assert.That(sim.Commands.EmitCall, "a chase starts with a call");
        }

        [Test]
        public void FullSuspicionUpCloseStillWaitsForMinAlertTime()
        {
            var sim = new BrainSim { MoveBody = false, TargetVisible = true };
            sim.PlaceTarget(3f);
            sim.TargetVelocity = Vector3.right * 6f;
            var alertFrame = sim.RunUntil(ZombieState.Alert, 1f);
            sim.Step();
            sim.Step();
            Assert.That(sim.Brain.Suspicion, Is.GreaterThan(0.5f));
            var chaseFrame = sim.RunUntil(ZombieState.Chase, 3f);
            Assert.That((chaseFrame - alertFrame) * Dt, Is.GreaterThanOrEqualTo(Tuning.MinAlertTime - 1e-3f));
            Assert.That((chaseFrame - alertFrame) * Dt, Is.EqualTo(Tuning.MinAlertTime).Within(1e-3f));
        }

        [Test]
        public void LosingSightDuringAlertGoesBackToWanderWithoutChase()
        {
            var anchor = new Vector3(2f, 0f, 3f);
            var sim = new BrainSim(7, anchor) { MoveBody = false, TargetVisible = true };
            sim.PlaceTarget(15f);
            var alertFrame = sim.RunUntil(ZombieState.Alert, 5f);
            sim.TargetVisible = false;
            var wanderFrame = sim.RunUntil(ZombieState.Wander, 10f);
            Assert.That(sim.Last.Transition.From, Is.EqualTo(ZombieState.Alert));
            Assert.That(sim.Last.Transition.Reason, Is.EqualTo(TransitionReason.AlertTimeout));
            // 0.3 suspicion: 0.5 s hold then 0.5 s of decay, so the minimum alert time is what ends it.
            Assert.That((wanderFrame - alertFrame) * Dt, Is.EqualTo(Tuning.MinAlertTime).Within(1e-3f));
            Assert.That(sim.Brain.Suspicion, Is.LessThan(0.1f));
            BrainSim.AssertNear(sim.Commands.Destination, anchor);
            for (var i = 0; i < 600; i++)
            {
                sim.Step();
                Assert.That(sim.State, Is.Not.EqualTo(ZombieState.Chase).And.Not.EqualTo(ZombieState.Alert));
            }
        }

        [Test]
        public void NoiseAlertsThenInvestigatesLooksSearchesAndWandersBack()
        {
            var sim = new BrainSim();
            sim.Step();
            var noise = new Vector3(0f, 0f, 6f);
            sim.Hear(noise);
            var alertFrame = sim.RunUntil(ZombieState.Alert, 1f);
            Assert.That(sim.Last.Transition.Reason, Is.EqualTo(TransitionReason.HeardNoise));
            Assert.That(sim.Brain.Suspicion, Is.EqualTo(Tuning.NoiseSuspicion).Within(1e-5f));
            Assert.That(sim.Commands.Stop);
            BrainSim.AssertNear(sim.Commands.LookPoint, noise + Vector3.up * Tuning.EyeHeight);

            var investigateFrame = sim.RunUntil(ZombieState.Investigate, 3f);
            Assert.That(sim.Last.Transition.Reason, Is.EqualTo(TransitionReason.AlertTimeout));
            Assert.That((investigateFrame - alertFrame) * Dt, Is.EqualTo(Tuning.MinAlertTime).Within(1e-3f));
            BrainSim.AssertNear(sim.Commands.Destination, noise);
            Assert.That(sim.Commands.Speed, Is.EqualTo(Tuning.InvestigateSpeed * sim.Brain.SpeedFactor).Within(1e-5f));

            for (var i = 0; i < 60 * 20 && !sim.Commands.Stop; i++) sim.Step();
            var arrivalFrame = sim.Frame - 1;
            Assert.That(sim.State, Is.EqualTo(ZombieState.Investigate));
            BrainSim.AssertNear(sim.Position, noise, "arrived", 0.11f);
            var firstLook = sim.Commands.LookPoint;
            sim.Run(1f);
            Assert.That(Vector3.Distance(sim.Commands.LookPoint, firstLook), Is.GreaterThan(1f), "looks around");

            var searchFrame = sim.RunUntil(ZombieState.Search, 3f);
            Assert.That(sim.Last.Transition.Reason, Is.EqualTo(TransitionReason.Arrived));
            Assert.That((searchFrame - arrivalFrame) * Dt, Is.EqualTo(Tuning.InvestigateLookTime).Within(1e-3f));
            Assert.That(BrainSim.PlanarDistance(sim.Commands.Destination, noise), Is.LessThanOrEqualTo(Tuning.InvestigateSearchRadius + 1e-4f));

            var wanderFrame = sim.RunUntil(ZombieState.Wander, 7f);
            Assert.That(sim.Last.Transition.Reason, Is.EqualTo(TransitionReason.SearchDone));
            Assert.That((wanderFrame - searchFrame) * Dt, Is.LessThanOrEqualTo(Tuning.InvestigateSearchDuration + 1e-3f));
            BrainSim.AssertNear(sim.Commands.Destination, sim.Brain.Anchor);
        }

        [Test]
        public void NoiseAloneNeverStartsAChase()
        {
            var sim = new BrainSim();
            for (var i = 0; i < 60 * 30; i++)
            {
                if (i % 20 == 0) sim.Hear(new Vector3(0f, 0f, 4f), NoiseKind.Glass);
                sim.Step();
                Assert.That(sim.State, Is.Not.EqualTo(ZombieState.Chase));
                Assert.That(sim.Brain.Suspicion, Is.LessThan(Tuning.ChaseThreshold));
            }
            Assert.That(sim.Brain.Suspicion, Is.GreaterThan(Tuning.ChaseThreshold - 0.1f));
        }

        [Test]
        public void ZombieCallShortensTheAlert()
        {
            var sim = new BrainSim();
            sim.Step();
            sim.Hear(new Vector3(5f, 0f, 5f), NoiseKind.ZombieCall);
            var alertFrame = sim.RunUntil(ZombieState.Alert, 1f);
            Assert.That(sim.Last.Transition.Reason, Is.EqualTo(TransitionReason.Called));
            var investigateFrame = sim.RunUntil(ZombieState.Investigate, 2f);
            Assert.That((investigateFrame - alertFrame) * Dt, Is.EqualTo(Tuning.CalledAlertTime).Within(1e-3f));
        }

        [Test]
        public void ProximityChasesAfterTheSnarl()
        {
            var sim = new BrainSim { MoveBody = false };
            sim.Step();
            sim.TargetPosition = new Vector3(0f, 0f, -1f);
            sim.InProximity = true;
            var alertFrame = sim.RunUntil(ZombieState.Alert, 1f);
            Assert.That(sim.Last.Transition.Reason, Is.EqualTo(TransitionReason.Proximity));
            var chaseFrame = sim.RunUntil(ZombieState.Chase, 1f);
            Assert.That(sim.Last.Transition.Reason, Is.EqualTo(TransitionReason.Proximity));
            Assert.That((chaseFrame - alertFrame) * Dt, Is.GreaterThanOrEqualTo(Tuning.ProximitySnarl - 1e-3f));
            Assert.That((chaseFrame - alertFrame) * Dt, Is.EqualTo(Tuning.ProximitySnarl).Within(1e-3f));
            Assert.That(sim.Commands.EmitCall);
            BrainSim.AssertNear(sim.Commands.Destination, sim.TargetPosition);
        }

        [TestCase(2.5f, ZombieState.Feed)]
        [TestCase(5f, ZombieState.Wander)]
        public void DeadTargetIsFedOnNearbyAndForgottenFarAway(float distance, ZombieState expected)
        {
            var sim = BrainSim.Chasing();
            sim.PlaceTarget(distance);
            sim.TargetAlive = false;
            sim.Step();
            Assert.That(sim.State, Is.EqualTo(expected));
            Assert.That(sim.Last.Transition.Reason, Is.EqualTo(TransitionReason.TargetDead));
            if (expected == ZombieState.Feed)
            {
                Assert.That(sim.Commands.Stop);
                Assert.That(sim.Commands.Pose.Feed, Is.EqualTo(1f));
                sim.Run(5f);
                Assert.That(sim.State, Is.EqualTo(ZombieState.Feed));
            }
            else BrainSim.AssertNear(sim.Commands.Destination, sim.Brain.Anchor);
        }

        [Test]
        public void ResetReturnsToWanderAtTheNewAnchor()
        {
            var sim = BrainSim.Chasing();
            Assert.That(sim.Brain.Suspicion, Is.EqualTo(1f));
            var anchor = new Vector3(20f, 0f, 20f);
            sim.Brain.Reset(anchor);
            Assert.That(sim.State, Is.EqualTo(ZombieState.Wander));
            Assert.That(sim.Brain.Suspicion, Is.Zero);
            Assert.That(sim.Brain.AttackPhase, Is.EqualTo(AttackPhase.None));
            Assert.That(sim.Brain.Anchor, Is.EqualTo(anchor));
            Assert.That(sim.Brain.TransitionedThisTick);
            Assert.That(sim.Brain.LastTransition.From, Is.EqualTo(ZombieState.Chase));
            Assert.That(sim.Brain.LastTransition.Reason, Is.EqualTo(TransitionReason.Reset));

            sim.Position = anchor;
            sim.MoveBody = true;
            sim.Step();
            BrainSim.AssertNear(sim.Commands.Destination, anchor);
            for (var i = 0; i < 60 * 60; i++)
            {
                sim.Step();
                Assert.That(sim.State, Is.EqualTo(ZombieState.Idle).Or.EqualTo(ZombieState.Wander));
                Assert.That(BrainSim.PlanarDistance(sim.Position, anchor), Is.LessThanOrEqualTo(Tuning.LeashRadius));
            }
        }

        [Test]
        public void HearingMultiplierFollowsAwareness()
        {
            var sim = new BrainSim { MoveBody = false };
            Assert.That(sim.Brain.HearingMultiplier, Is.EqualTo(Tuning.HearingUnaware));
            sim.Hear(Vector3.forward * 5f);
            sim.Step();
            Assert.That(sim.Brain.HearingMultiplier, Is.EqualTo(Tuning.HearingAlert));
            Assert.That(BrainSim.Chasing().Brain.HearingMultiplier, Is.EqualTo(Tuning.HearingHunting));
        }
    }
}
