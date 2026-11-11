using System.Collections;
using DeadCounty.AI;
using DeadCounty.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DeadCounty.Tests
{
    // Night flashlight: lit at 20 m the target is seen (25 m range), unlit it is not (8 m range).
    // Only the flag is driven here; senses read FlashlightOn, no real spot needed.
    public class ZombieFlashlightTests : ZombieWorldTests
    {
        [UnityTest]
        public IEnumerator FlashlightOnAt20mAtNightAlertsThenChases()
        {
            World = new ZombieTestWorld(4f);
            World.Target.Visible = false;
            World.Target.Position = new Vector3(0f, 0f, 20f);
            var zombie = World.SpawnZombie(Vector3.zero);
            yield return ZombieTestWorld.Settle(zombie);

            WorldLighting.NightFactor = 1f;
            World.Target.FlashlightOn = true;
            World.Target.Visible = true;

            yield return ZombieTestWorld.WaitUntil(() => World.Saw(zombie, ZombieState.Chase, out _), 6f);
            Assert.That(World.Saw(zombie, ZombieState.Alert, out var alert), Is.True, "never alerted on the lit target");
            Assert.That(alert.Reason, Is.EqualTo(TransitionReason.SawTarget));
            Assert.That(World.Saw(zombie, ZombieState.Chase, out _), Is.True, $"never chased, still {zombie.State}");
        }

        [UnityTest]
        public IEnumerator FlashlightOffAt20mAtNightStaysCalm()
        {
            World = new ZombieTestWorld(4f);
            World.Target.Visible = false;
            World.Target.Position = new Vector3(0f, 0f, 20f);
            var zombie = World.SpawnZombie(Vector3.zero);
            yield return ZombieTestWorld.Settle(zombie);

            WorldLighting.NightFactor = 1f;
            World.Target.FlashlightOn = false;
            World.Target.Visible = true;
            var senses = zombie.SenseCount;

            yield return ZombieTestWorld.WaitUntil(() => World.Saw(zombie, ZombieState.Alert, out _), 3f);
            Assert.That(World.Saw(zombie, ZombieState.Alert, out _), Is.False, "alerted on the unlit target");
            Assert.That(zombie.SenseCount, Is.GreaterThan(senses + 5), "the zombie did not sense while waiting");
        }
    }
}
