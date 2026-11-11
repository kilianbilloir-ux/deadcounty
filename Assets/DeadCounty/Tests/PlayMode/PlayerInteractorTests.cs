#if UNITY_EDITOR
using System.Collections;
using DeadCounty.Player;
using DeadCounty.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DeadCounty.Tests
{
    // The player rig in front of a door: picking it, the HUD prompt next to it, opening it with E.
    public class PlayerInteractorTests : PlayerRigTests
    {
        Door door;
        Camera view;

        // A door whose opening is centred `distance` metres in front of the player, across their heading.
        Door DoorAhead(float distance)
        {
            var player = Health.transform;
            var center = player.position + player.forward * distance;
            return TestDoor.Create(center - player.right * 0.5f, player.eulerAngles.y + 90f);
        }

        [UnityTearDown]
        public IEnumerator TearDownDoor()
        {
            if (door != null) Object.Destroy(door.gameObject);
            if (view != null) Object.Destroy(view.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DoorInReachIsPickedPromptedAndOpenedWithE()
        {
            view = new GameObject("TestCamera", typeof(Camera)) { tag = "MainCamera" }.GetComponent<Camera>();
            var player = Health.transform;
            view.transform.SetPositionAndRotation(player.position - player.forward * 2f + Vector3.up * 1.8f, player.rotation);
            door = DoorAhead(1.2f);
            yield return null;
            yield return null;

            var interactor = Rig.GetComponentInChildren<PlayerInteractor>();
            Assert.That(interactor.Current, Is.EqualTo(door));
            var root = Rig.GetComponent<UIDocument>().rootVisualElement;
            Assert.That(root.Q("interaction").style.display.value, Is.EqualTo(DisplayStyle.Flex), "no prompt");
            Assert.That(root.Q<Label>("interaction-name").text, Is.EqualTo("Porte"));
            Assert.That(root.Q<Label>("interaction-action").text, Does.EndWith("Ouvrir"));

            yield return Tap(Keyboard, new KeyboardState(Key.E), new KeyboardState());
            yield return new WaitForSeconds(0.7f);
            Assert.That(door.IsShut, Is.False, "E did not open the door");
            Assert.That(Vector3.Dot(door.transform.GetChild(0).GetChild(0).position - player.position, player.forward), Is.GreaterThan(1.2f), "the door opened toward the player");
            Assert.That(root.Q<Label>("interaction-action").text, Does.EndWith("Fermer"));
        }

        [UnityTest]
        public IEnumerator DoorOutOfReachOrBehindIsIgnored()
        {
            door = DoorAhead(3f);
            yield return null;
            var interactor = Rig.GetComponentInChildren<PlayerInteractor>();
            Assert.That(interactor.Current, Is.Null, "picked a door 3 m away");

            Object.Destroy(door.gameObject);
            door = DoorAhead(-1f);
            yield return null;
            Assert.That(interactor.Current, Is.Null, "picked a door behind the player");
        }
    }
}
#endif
