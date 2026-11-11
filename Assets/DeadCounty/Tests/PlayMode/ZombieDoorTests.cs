using System.Collections;
using DeadCounty.AI;
using DeadCounty.Perception;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DeadCounty.Tests
{
    // Zombies going after a noise behind a shut door: the NavMesh crosses the doorway, the door does not let them.
    public class ZombieDoorTests : ZombieWorldTests
    {
        const int NoiseSource = -5;
        static readonly Vector3 NoisePoint = new(0f, 0f, 4f);

        IEnumerator HearBehindTheDoor(params ZombieAgent[] zombies)
        {
            // Only the noise matters; the stand-in target would show up in the doorway once it opens.
            World.Target.Visible = false;
            foreach (var zombie in zombies) yield return ZombieTestWorld.Settle(zombie);
            NoiseSystem.Emit(NoisePoint, 12f, NoiseKind.Debug, NoiseSource);
        }

        [UnityTest]
        public IEnumerator HuntingZombieStopsAtTheDoorAndForcesItInFiveSeconds()
        {
            World = new ZombieTestWorld(4f);
            var door = World.Door(new Vector3(-0.5f, 0f, 0f), 90f);
            var zombie = World.SpawnZombie(new Vector3(0f, 0f, -4f));
            yield return HearBehindTheDoor(zombie);

            yield return ZombieTestWorld.WaitUntil(() => door.ForceProgress > 0f, 15f);
            Assert.That(door.ForceProgress, Is.GreaterThan(0f), $"never pushed, {zombie.State} at {zombie.Position}");
            var start = Time.time;
            var closest = float.NegativeInfinity;
            string armsDown = null;
            while (door.IsShut && Time.time - start < 10f)
            {
                closest = Mathf.Max(closest, zombie.Position.z);
                if (armsDown == null && zombie.Commands.Pose.ArmsForward != 1f)
                    armsDown = $"arms down {Time.time - start:0.00} s into the push, {zombie.State}, door at {door.ForceProgress:0.00}";
                yield return null;
            }
            Assert.That(door.IsShut, Is.False, "the door held");
            Assert.That(Time.time - start, Is.InRange(4.8f, 5.6f));
            Assert.That(closest, Is.LessThan(-0.3f), "the zombie walked into the shut door");
            Assert.That(armsDown, Is.Null);

            yield return ZombieTestWorld.WaitUntil(() => zombie.Position.z > 1f, 10f);
            Assert.That(zombie.Position.z, Is.GreaterThan(1f), "the zombie did not go through the forced door");
        }

        [UnityTest]
        public IEnumerator TwoZombiesForceTheDoorTwiceAsFast()
        {
            World = new ZombieTestWorld(4f);
            var door = World.Door(new Vector3(-0.5f, 0f, 0f), 90f);
            var left = World.SpawnZombie(new Vector3(-0.4f, 0f, -4f), 0f, 1);
            var right = World.SpawnZombie(new Vector3(0.4f, 0f, -4f), 0f, 2);
            yield return HearBehindTheDoor(left, right);

            yield return ZombieTestWorld.WaitUntil(() => door.ForceProgress > 0f, 15f);
            var start = Time.time;
            yield return ZombieTestWorld.WaitUntil(() => !door.IsShut, 10f);
            Assert.That(door.IsShut, Is.False, "the door held");
            Assert.That(Time.time - start, Is.LessThan(4f), "two pushers must be faster than one");
        }

        [UnityTest]
        public IEnumerator CalmZombieTurnsBackAtAShutDoor()
        {
            World = new ZombieTestWorld(4f);
            var door = World.Door(new Vector3(-0.5f, 0f, 0f), 90f);
            var zombie = World.SpawnZombie(new Vector3(0f, 0f, -2f));
            yield return ZombieTestWorld.Settle(zombie);
            // Its wander anchor moves behind the door: every walk home ends at the door.
            zombie.Brain.Reset(NoisePoint);

            var nearest = float.NegativeInfinity;
            var start = Time.time;
            while (Time.time - start < 15f)
            {
                nearest = Mathf.Max(nearest, zombie.Position.z);
                yield return null;
            }
            Assert.That(nearest, Is.GreaterThan(-1.2f), "never walked up to the door");
            Assert.That(nearest, Is.LessThan(-0.3f), "a calm zombie went through the shut door");
            Assert.That(door.ForceProgress, Is.Zero, "a calm zombie pushed the door");
            Assert.That(ZombieTestWorld.IsCalm(zombie.State), $"{zombie.State}");
        }
    }
}
