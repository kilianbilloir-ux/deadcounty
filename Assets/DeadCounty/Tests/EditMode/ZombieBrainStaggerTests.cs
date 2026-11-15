using DeadCounty.AI;
using NUnit.Framework;
using UnityEngine;

namespace DeadCounty.Tests
{
    public class ZombieBrainStaggerTests
    {
        const float Dt = BrainSim.Dt;

        static readonly Vector3 Attacker = new(0f, 0f, -1.2f);

        [Test]
        public void AStruckZombieReelsThenChasesWhoeverStruckIt()
        {
            var sim = new BrainSim();
            sim.Step();
            Assert.That(sim.State, Is.EqualTo(ZombieState.Idle).Or.EqualTo(ZombieState.Wander));

            sim.Brain.TakeHit(sim.Position, Attacker, 0.9f, 0.5f);
            Assert.That(sim.State, Is.EqualTo(ZombieState.Stagger));
            Assert.That(sim.Brain.LastTransition.Reason, Is.EqualTo(TransitionReason.Hit));

            sim.Run(0.8f);
            Assert.That(sim.State, Is.EqualTo(ZombieState.Stagger), "recovered before the stagger was over");
            Assert.That(sim.Commands.Stop, Is.True);
            sim.Run(0.2f);
            Assert.That(sim.State, Is.EqualTo(ZombieState.Chase));
            Assert.That(sim.Last.Transition.Reason, Is.EqualTo(TransitionReason.Recovered));
            BrainSim.AssertNear(sim.Brain.LastKnownPosition, Attacker, "the zombie does not know where the blow came from");
        }

        [Test]
        public void TheShoveCoversTheKnockbackAwayFromTheAttackerWhateverTheFrameRate()
        {
            foreach (var dt in new[] { 1f / 144f, 1f / 60f, 1f / 15f })
            {
                var tuning = ZombieTuning.Standard;
                var brain = new ZombieBrain(tuning, 3, Vector3.zero);
                var commands = new BrainCommands();
                brain.TakeHit(Vector3.zero, Attacker, 0.9f, 0.5f);
                var pushed = Vector3.zero;
                for (var time = 0f; time < 0.9f; time += dt)
                {
                    brain.Tick(dt, new PerceptionSnapshot { Time = time, Forward = Vector3.forward, TargetAlive = true }, new FakeWorld(), ref commands);
                    pushed += commands.PushDirection * (commands.PushSpeed * dt);
                }
                BrainSim.AssertNear(pushed, new Vector3(0f, 0f, 0.5f), $"at {1f / dt:0} fps");
            }
        }

        [Test]
        public void ABlowBreaksAWindup()
        {
            var sim = BrainSim.Chasing();
            sim.TargetVisible = true;
            sim.PlaceTarget(1.2f);
            sim.RunUntil(ZombieState.Attack, 5f);
            Assert.That(sim.Brain.AttackPhase, Is.EqualTo(AttackPhase.Windup));

            sim.Brain.TakeHit(sim.Position, sim.TargetPosition, 0.9f, 0f);
            Assert.That(sim.Brain.AttackPhase, Is.EqualTo(AttackPhase.None));
            sim.Run(0.85f);
            Assert.That(sim.Strikes, Is.Zero, "the interrupted attack still struck");
            Assert.That(sim.Commands.PushSpeed, Is.Zero, "a blow without knockback moved the zombie");
        }

        [Test]
        public void AZombieStruckByTheDeadGoesHome()
        {
            var sim = new BrainSim();
            sim.Step();
            sim.Brain.TakeHit(sim.Position, Attacker, 0.3f, 0f);
            sim.TargetAlive = false;
            sim.Run(0.5f);
            Assert.That(sim.State, Is.EqualTo(ZombieState.Idle).Or.EqualTo(ZombieState.Wander));
        }

        [Test]
        public void ANewBlowStartsTheStaggerOver()
        {
            var sim = new BrainSim();
            sim.Step();
            sim.Brain.TakeHit(sim.Position, Attacker, 0.6f, 0f);
            sim.Run(0.5f);
            sim.Brain.TakeHit(sim.Position, Attacker, 0.6f, 0f);
            sim.Run(0.5f);
            Assert.That(sim.State, Is.EqualTo(ZombieState.Stagger));
            sim.Run(0.2f);
            Assert.That(sim.State, Is.EqualTo(ZombieState.Chase));
        }
    }
}
