#if UNITY_EDITOR
using System.Collections;
using DeadCounty.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace DeadCounty.Tests
{
    // Sitting down on the ground and on a seat with the generated rig, and what it does to stamina.
    public class PlayerRestTests : PlayerRigTests
    {
        PlayerRest rest;
        ThirdPersonController controller;
        Transform player, hips;
        GameObject bench;

        [UnitySetUp]
        public IEnumerator SetUpRest()
        {
            rest = Rig.GetComponentInChildren<PlayerRest>();
            controller = Rig.GetComponentInChildren<ThirdPersonController>();
            player = controller.transform;
            hips = player.Find("Model/Hanches");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDownRest()
        {
            if (bench != null) Object.Destroy(bench);
            yield return null;
        }

        IEnumerator Press(Key key) => Tap(Keyboard, new KeyboardState(key), new KeyboardState());

        // A seat 0.5 m high, 1.6 m wide, facing -Z like the builder's furniture, 1.2 m in front of the player.
        IEnumerator PlaceBench()
        {
            bench = new GameObject("TestBench");
            bench.transform.SetPositionAndRotation(player.position + player.forward * 1.2f, player.rotation);
            var box = bench.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.25f, 0f);
            box.size = new Vector3(1.6f, 0.5f, 0.5f);
            bench.AddComponent<Seat>().Configure("Banc", 0.5f, 0.45f, -0.05f);
            yield return new WaitForFixedUpdate();
            yield return null;
        }

        [UnityTest]
        public IEnumerator RestSitsOnTheGroundAndMovingGetsUp()
        {
            var standing = hips.localPosition.y;
            yield return Press(Key.R);
            Assert.That(rest.Seated, "the Rest key did not sit the player down");
            Assert.That(rest.OnSeat, Is.False);
            yield return new WaitForSeconds(1.2f);
            Assert.That(hips.localPosition.y, Is.LessThan(0.3f), "the body is not sitting on the ground");
            Assert.That(controller.CurrentSpeed, Is.Zero);

            InputSystem.QueueStateEvent(Keyboard, new KeyboardState(Key.W));
            yield return null;
            yield return null;
            Assert.That(rest.Seated, Is.False, "moving did not get the player up");
            yield return new WaitForSeconds(1.2f);
            InputSystem.QueueStateEvent(Keyboard, new KeyboardState());
            Assert.That(hips.localPosition.y, Is.GreaterThan(standing - 0.15f), "the body did not stand back up");
        }

        [UnityTest]
        public IEnumerator SittingBringsStaminaBackThreeTimesFasterThanStanding()
        {
            Stamina.Model.Exert(60f);
            yield return new WaitForSeconds(4f);
            var standing = Stamina.Model.Current - 40f;
            Assert.That(standing, Is.EqualTo(4f).Within(0.5f), "2 per second after the 2 s delay, standing still");

            Stamina.Model.Exert(Stamina.Model.Current - 40f);
            Assert.That(rest.SitOnGround());
            yield return new WaitForSeconds(4f);
            Assert.That(Stamina.Model.Current - 40f, Is.EqualTo(12f).Within(1f), "6 per second after the delay, sitting down");
        }

        [UnityTest]
        public IEnumerator ASeatTakesThePlayerAndGivesThemBackWhereTheyStood()
        {
            yield return PlaceBench();
            var stood = player.position;
            yield return Press(Key.E);
            Assert.That(rest.Seated, "Interact on a bench did not sit the player down");
            Assert.That(rest.OnSeat);
            var seat = bench.GetComponent<Seat>();
            Assert.That(Vector3.Distance(player.position, bench.transform.TransformPoint(new Vector3(0f, 0f, -0.05f))), Is.LessThan(0.05f), "the player is not on the seat");
            Assert.That(Vector3.Angle(player.forward, -bench.transform.forward), Is.LessThan(1f), "the sitter does not face the front of the bench");
            Assert.That(Rig.GetComponentInChildren<PlayerInteractor>().Current, Is.Null, "a seated player is still offered the bench");
            Assert.That(bench.GetComponent<Collider>().enabled, Is.False, "the block of the bench still pushes the camera into the sitter's head");
            yield return new WaitForSeconds(1.2f);
            Assert.That(hips.localPosition.y, Is.EqualTo(seat.Height + 0.12f).Within(0.05f), "the hips are not at seat height");

            yield return Press(Key.R);
            Assert.That(rest.Seated, Is.False);
            Assert.That(Vector3.Distance(player.position, stood), Is.LessThan(0.05f), "getting up did not put the player back in front of the bench");
            Assert.That(bench.GetComponent<Collider>().enabled, "the bench is no longer solid once left");
        }

        [UnityTest]
        public IEnumerator AHitOrTheAttackButtonGetsASeatedPlayerUpWithoutSwinging()
        {
            var combat = Rig.GetComponentInChildren<PlayerCombat>();
            Assert.That(rest.SitOnGround());
            yield return null;
            yield return Tap(Gamepad, new GamepadState { rightTrigger = 1f }, new GamepadState());
            Assert.That(rest.Seated, Is.False, "the attack button did not get the player up");
            Assert.That(combat.Swing.Busy, Is.False, "the press that got the player up also swung");

            yield return new WaitForSeconds(0.3f);
            Assert.That(rest.SitOnGround());
            Health.ApplyHit(10f, player.position + player.forward);
            Assert.That(rest.Seated, Is.False, "a hit left the player sitting");
        }
    }
}
#endif
