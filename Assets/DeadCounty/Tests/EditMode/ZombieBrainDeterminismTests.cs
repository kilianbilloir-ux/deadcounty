using DeadCounty.AI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace DeadCounty.Tests
{
    public class ZombieBrainDeterminismTests
    {
        static readonly ZombieTuning Tuning = ZombieTuning.Standard;

        // 80 s touching every state: calm, a noise investigated and searched, a slow target seen, chased and
        // struck, then lost, searched for and forgotten.
        static void RunScenario(BrainSim sim)
        {
            for (var frame = 0; frame < 4800; frame++)
            {
                if (frame == 600) sim.Hear(new Vector3(3f, 0f, 5f));
                if (frame == 1500)
                {
                    sim.TargetPosition = new Vector3(0f, 0f, 9f);
                    sim.TargetVelocity = new Vector3(0.5f, 0f, 0f);
                    sim.MoveTarget = true;
                }
                if (frame == 2700) sim.TargetVelocity = new Vector3(2f, 0f, 1f);
                sim.TargetVisible = frame >= 1500 && frame < 2700;
                sim.Step();
            }
        }

        static bool Visited(BrainSim sim, ZombieState state)
        {
            foreach (var entry in sim.Log)
                if (entry.Transition.To == state) return true;
            return false;
        }

        [Test]
        public void SameSeedAndInputsReplayIdentically()
        {
            var a = new BrainSim(11);
            var b = new BrainSim(11);
            RunScenario(a);
            RunScenario(b);
            TestContext.WriteLine(string.Join("\n", a.Log));

            Assert.That(b.Log.Count, Is.EqualTo(a.Log.Count));
            for (var i = 0; i < a.Log.Count; i++)
            {
                Assert.That(b.Log[i].ToString(), Is.EqualTo(a.Log[i].ToString()));
                Assert.That(b.Log[i].Transition.Time, Is.EqualTo(a.Log[i].Transition.Time));
            }
            Assert.That(b.Position, Is.EqualTo(a.Position));
            Assert.That(b.Brain.Suspicion, Is.EqualTo(a.Brain.Suspicion));
            Assert.That(b.Strikes, Is.EqualTo(a.Strikes));

            foreach (var state in new[] { ZombieState.Idle, ZombieState.Alert, ZombieState.Investigate, ZombieState.Chase, ZombieState.Attack, ZombieState.Search, ZombieState.Wander })
                Assert.That(Visited(a, state), $"scenario never reached {state}");
            Assert.That(a.Strikes, Is.GreaterThan(0));

            var other = new BrainSim(12);
            RunScenario(other);
            Assert.That(other.Position, Is.Not.EqualTo(a.Position), "another seed should play out differently");
        }

        [Test]
        public void SeedsSpreadSpeedFactorsWithinTheirJitter()
        {
            float minWander = 2f, maxWander = 0f, min = 2f, max = 0f;
            for (var seed = 0; seed < 64; seed++)
            {
                var brain = new ZombieBrain(Tuning, seed, Vector3.zero);
                minWander = Mathf.Min(minWander, brain.WanderSpeedFactor);
                maxWander = Mathf.Max(maxWander, brain.WanderSpeedFactor);
                min = Mathf.Min(min, brain.SpeedFactor);
                max = Mathf.Max(max, brain.SpeedFactor);
                Assert.That(new ZombieBrain(Tuning, seed, Vector3.one).SpeedFactor, Is.EqualTo(brain.SpeedFactor));
            }
            Assert.That(minWander, Is.GreaterThanOrEqualTo(1f - Tuning.WanderSpeedJitter));
            Assert.That(maxWander, Is.LessThanOrEqualTo(1f + Tuning.WanderSpeedJitter));
            Assert.That(maxWander - minWander, Is.GreaterThan(Tuning.WanderSpeedJitter));
            Assert.That(min, Is.GreaterThanOrEqualTo(1f - Tuning.SpeedJitter));
            Assert.That(max, Is.LessThanOrEqualTo(1f + Tuning.SpeedJitter));
            Assert.That(max - min, Is.GreaterThan(Tuning.SpeedJitter));
            // Chase speed always stays under the player's 1.5 m/s walk.
            Assert.That(Tuning.ChaseSpeed * max, Is.LessThan(1.5f));
        }

        [Test]
        public void TickDoesNotAllocate()
        {
            // The first run JIT-compiles every path the measured run then takes.
            RunScenario(new BrainSim(11) { Log = null });
            var sim = new BrainSim(11) { Log = null };
            Assert.That(() => RunScenario(sim), Is.Not.AllocatingGCMemory());
            Assert.That(sim.Strikes, Is.GreaterThan(0));
        }
    }
}
