using DeadCounty.AI;
using NUnit.Framework;
using UnityEngine;

namespace DeadCounty.Tests
{
    public class ZombieBrainDownTests
    {
        static readonly Vector3 Attacker = new(0f, 0f, -1.2f);

        static BrainSim Felled()
        {
            var sim = new BrainSim();
            sim.Step();
            sim.Brain.TakeHit(sim.Position, Attacker, 1.1f, 0.9f, true);
            return sim;
        }

        [Test]
        public void AFelledZombieLiesStillThenGetsUpAndChasesWhoeverFelledIt()
        {
            var sim = Felled();
            var tuning = sim.Tuning;
            var down = sim.Brain.DownDuration;
            Assert.That(sim.State, Is.EqualTo(ZombieState.Down));
            Assert.That(sim.Brain.LastTransition.Reason, Is.EqualTo(TransitionReason.Felled));
            Assert.That(down, Is.InRange(tuning.DownTime * (1f - tuning.DownTimeJitter), tuning.DownTime * (1f + tuning.DownTimeJitter)));

            sim.Run(down - 0.1f);
            Assert.That(sim.State, Is.EqualTo(ZombieState.Down));
            Assert.That(sim.Brain.Rising, Is.False, "started getting up before the end of the lie");
            Assert.That(sim.Commands.Stop, Is.True);
            Assert.That(sim.Commands.HasLookPoint, Is.False, "the body would turn on the ground");
            sim.Run(0.2f);
            Assert.That(sim.Brain.Rising, Is.True);
            Assert.That(sim.State, Is.EqualTo(ZombieState.Down), "a rising zombie is still on the ground");

            sim.Run(tuning.RiseTime);
            Assert.That(sim.State, Is.EqualTo(ZombieState.Chase));
            Assert.That(sim.Last.Transition.Reason, Is.EqualTo(TransitionReason.Recovered));
            BrainSim.AssertNear(sim.Brain.LastKnownPosition, Attacker, "the zombie does not know where the blow came from");
        }

        [Test]
        public void EveryBlowOnTheGroundStartsTheLieOver()
        {
            var sim = Felled();
            var lie = sim.Brain.DownDuration;
            sim.Run(lie + 0.5f);
            Assert.That(sim.Brain.Rising, Is.True);

            sim.Brain.TakeHit(sim.Position, Attacker, 0f, 0f);
            Assert.That(sim.State, Is.EqualTo(ZombieState.Down));
            Assert.That(sim.Brain.Rising, Is.False, "a blow while getting up did not lay it back down");
            sim.Run(sim.Brain.DownDuration - 0.1f);
            Assert.That(sim.State, Is.EqualTo(ZombieState.Down));
            Assert.That(sim.Brain.Rising, Is.False);
        }

        [Test]
        public void AFallCoversTheKnockbackAwayFromTheAttacker()
        {
            var sim = Felled();
            var pushed = Vector3.zero;
            for (var i = 0; i < 30; i++)
            {
                sim.Step();
                pushed += sim.Commands.PushDirection * (sim.Commands.PushSpeed * BrainSim.Dt);
            }
            BrainSim.AssertNear(pushed, new Vector3(0f, 0f, 0.9f), "the fall did not slide the body away from the blow");
        }

        [Test]
        public void AFelledZombieNeverStrikes()
        {
            var sim = BrainSim.Chasing();
            sim.TargetVisible = true;
            sim.PlaceTarget(1.2f);
            sim.RunUntil(ZombieState.Attack, 5f);
            sim.Brain.TakeHit(sim.Position, sim.TargetPosition, 0f, 0f, true);
            Assert.That(sim.Brain.AttackPhase, Is.EqualTo(AttackPhase.None));
            sim.Run(sim.Brain.DownDuration);
            Assert.That(sim.Strikes, Is.Zero, "struck from the ground");
            Assert.That(sim.State, Is.EqualTo(ZombieState.Down));
        }

        [Test]
        public void AZombieFelledWhileItsTargetDiesGoesHomeOnceUp()
        {
            var sim = Felled();
            sim.TargetAlive = false;
            sim.Run(sim.Brain.DownDuration + sim.Tuning.RiseTime + 0.1f);
            Assert.That(sim.State, Is.EqualTo(ZombieState.Idle).Or.EqualTo(ZombieState.Wander));
        }

        [Test]
        public void AStaggeredZombieCanStillBeFelled()
        {
            var sim = new BrainSim();
            sim.Step();
            sim.Brain.TakeHit(sim.Position, Attacker, 0.9f, 0f);
            sim.Run(0.3f);
            sim.Brain.TakeHit(sim.Position, Attacker, 0.9f, 0f, true);
            Assert.That(sim.State, Is.EqualTo(ZombieState.Down));
        }
    }
}
