using System.Collections;
using DeadCounty.Core;
using DeadCounty.Perception;
using DeadCounty.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DeadCounty.Tests
{
    // One door with its hinge at (-0.5, 0, 0): the opening is centred on the origin along X, its normal is -Z.
    public class DoorTests
    {
        static readonly Vector3 UserSide = new(0f, 0f, -1f);
        static readonly Vector3 OtherSide = new(0f, 0f, 1f);

        Door door;
        GameObject blocker;
        Transform user;

        [UnitySetUp]
        public IEnumerator SetUpDoor()
        {
            door = TestDoor.Create(new Vector3(-0.5f, 0f, 0f), 90f);
            user = new GameObject("TestUser").transform;
            user.position = UserSide;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDownDoor()
        {
            Object.Destroy(door.gameObject);
            Object.Destroy(user.gameObject);
            if (blocker != null) Object.Destroy(blocker);
            yield return null;
        }

        static NoiseEvent LatestNoise()
        {
            var events = new NoiseEvent[1];
            Assert.That(NoiseSystem.Buffer.CopySince(NoiseSystem.Buffer.LatestId - 1, events), Is.EqualTo(1), "no noise");
            return events[0];
        }

        Vector3 LeafCenter => door.transform.GetChild(0).GetChild(0).position;

        [UnityTest]
        public IEnumerator InteractOpensAwayFromTheUserThenCloses()
        {
            Assert.That(door.IsShut);
            Assert.That(door.Action, Is.EqualTo("Ouvrir"));
            door.Interact(user, false);
            Assert.That(LatestNoise().Radius, Is.EqualTo(3f));
            yield return new WaitForSeconds(0.7f);
            Assert.That(door.IsShut, Is.False);
            Assert.That(door.Angle, Is.EqualTo(-95f).Within(0.01f));
            Assert.That(LeafCenter.z, Is.GreaterThan(0.3f), "the leaf swung toward the user");
            Assert.That(door.Action, Is.EqualTo("Fermer"));

            door.Interact(user, false);
            yield return new WaitForSeconds(0.7f);
            Assert.That(door.Angle, Is.EqualTo(0f).Within(0.01f));
            Assert.That(door.IsShut);
        }

        [UnityTest]
        public IEnumerator SprintingSlamsTheDoorFastAndLoud()
        {
            user.position = OtherSide;
            door.Interact(user, true);
            var noise = LatestNoise();
            Assert.That(noise.Radius, Is.EqualTo(12f));
            Assert.That(noise.Kind, Is.EqualTo(NoiseKind.Door));
            yield return new WaitForSeconds(0.25f);
            Assert.That(door.Angle, Is.EqualTo(95f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator ClosingWaitsForAClearDoorway()
        {
            door.Interact(user, false);
            yield return new WaitForSeconds(0.7f);
            blocker = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            blocker.layer = GameLayers.Zombie;
            blocker.transform.position = new Vector3(0f, 1f, 0.1f);
            Physics.SyncTransforms();
            door.Interact(user, false);
            yield return new WaitForSeconds(0.7f);
            Assert.That(door.IsShut, Is.False, "closed on a zombie standing in the doorway");

            Object.Destroy(blocker);
            yield return null;
            Physics.SyncTransforms();
            door.Interact(user, false);
            yield return new WaitForSeconds(0.7f);
            Assert.That(door.IsShut);
        }

        [UnityTest]
        public IEnumerator TwoZombiesPushingForceTheDoorInHalfTheTime()
        {
            var start = Time.time;
            while (door.IsShut && Time.time - start < 6f)
            {
                door.Push(OtherSide, Time.deltaTime);
                door.Push(OtherSide + Vector3.right * 0.4f, Time.deltaTime);
                yield return null;
            }
            var elapsed = Time.time - start;
            Assert.That(elapsed, Is.InRange(2.4f, 3f));
            Assert.That(LatestNoise().Radius, Is.EqualTo(6f));
            yield return new WaitForSeconds(0.4f);
            Assert.That(door.Angle, Is.EqualTo(95f).Within(0.01f), "the door must swing away from the zombies");
        }

        [UnityTest]
        public IEnumerator PushingLosesItsProgressOnceTheZombiesStop()
        {
            var start = Time.time;
            while (Time.time - start < 3f)
            {
                door.Push(OtherSide, Time.deltaTime);
                yield return null;
            }
            Assert.That(door.ForceProgress, Is.EqualTo(0.6f).Within(0.02f));
            // 1 s of push lost per second once nobody pushes.
            yield return new WaitForSeconds(2f);
            Assert.That(door.ForceProgress, Is.EqualTo(0.2f).Within(0.03f));
            Assert.That(door.IsShut);
        }
    }
}
