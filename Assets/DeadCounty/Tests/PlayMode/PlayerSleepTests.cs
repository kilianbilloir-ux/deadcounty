#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using DeadCounty.Items;
using DeadCounty.Perception;
using DeadCounty.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DeadCounty.Tests
{
    // Sleeping, waking and bleeding on the generated rig, and the afflictions they show.
    public class PlayerSleepTests : PlayerRigTests
    {
        const float Minute = 60f;

        PlayerSleep sleep;
        PlayerNeeds needs;
        ThirdPersonController controller;
        GameObject bed;

        VisualElement Root => Rig.GetComponent<UIDocument>().rootVisualElement;

        [UnitySetUp]
        public IEnumerator SetUpSleep()
        {
            sleep = Rig.GetComponentInChildren<PlayerSleep>();
            needs = Rig.GetComponentInChildren<PlayerNeeds>();
            controller = Rig.GetComponentInChildren<ThirdPersonController>();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDownSleep()
        {
            if (bed != null) Object.Destroy(bed);
            Time.timeScale = 1f;
            yield return null;
        }

        // Tired but fed and watered, so nothing hurts while asleep.
        IEnumerator FallAsleepTired()
        {
            needs.Model.Tick(20f * Minute, false);
            needs.Model.Restore(1f, 1f);
            Assert.That(sleep.TrySleep(), "a tired player could not sleep");
            yield return null;
        }

        [Test]
        public void ARestedPlayerCannotSleep()
        {
            Assert.That(sleep.CanSleep, Is.False);
            Assert.That(sleep.TrySleep(), Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator SleepRunsTimeTwentyTimesFasterAndStillsThePlayer()
        {
            yield return FallAsleepTired();
            Assert.That(sleep.Asleep);
            Assert.That(Time.timeScale, Is.EqualTo(20f));
            Assert.That(controller.Controllable, Is.False);
            var fatigue = needs.Model.Fatigue;
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(needs.Model.Fatigue, Is.GreaterThan(fatigue), "fatigue did not come back");
            Assert.That(Root.Q("sleep").style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        [UnityTest]
        public IEnumerator ARestedSleeperWakesUpAndTimeGoesBackToNormal()
        {
            yield return FallAsleepTired();
            needs.Model.Sleep(4f * Minute);
            yield return null;
            Assert.That(sleep.Asleep, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(controller.Controllable);
            Assert.That(Root.Q<Label>("loot-toast").text, Is.EqualTo("Reposé"));
        }

        [UnityTest]
        public IEnumerator ANoiseWithinTenMetresWakesTheSleeperButNotOneFarther()
        {
            yield return FallAsleepTired();
            var position = sleep.transform.position;
            NoiseSystem.Emit(position + Vector3.right * 14f, 4f, NoiseKind.Impact, -9);
            yield return null;
            Assert.That(sleep.Asleep, "woken by a noise 14 m away");
            NoiseSystem.Emit(position + Vector3.right * 6f, 4f, NoiseKind.Impact, -9);
            yield return null;
            Assert.That(sleep.Asleep, Is.False);
            Assert.That(Root.Q<Label>("loot-toast").text, Is.EqualTo("Réveillé par un bruit"));
        }

        [UnityTest]
        public IEnumerator AHitWakesTheSleeper()
        {
            yield return FallAsleepTired();
            Health.ApplyHit(10f, Health.transform.position + Vector3.forward);
            yield return null;
            Assert.That(sleep.Asleep, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator SleepHealsAWoundedPlayer()
        {
            Health.Pool.Apply(50f);
            yield return FallAsleepTired();
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(Health.Pool.Current, Is.GreaterThan(50.5f));
        }

        [UnityTest]
        public IEnumerator ABedPromptsToSleepOnlyWhenTiredAndLaysThePlayerDown()
        {
            var player = Health.transform;
            bed = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bed.SetActive(false);
            bed.transform.position = player.position + player.forward * 1.3f + Vector3.up * 0.25f;
            bed.transform.localScale = new Vector3(1.6f, 0.5f, 1f);
            var furniture = bed.AddComponent<Bed>();
            bed.SetActive(true);
            Physics.SyncTransforms();
            yield return null;
            var interactor = Rig.GetComponentInChildren<PlayerInteractor>();
            Assert.That(interactor.Current, Is.EqualTo(furniture));
            Assert.That(furniture.Action, Is.Null);
            Assert.That(furniture.DisplayName, Is.EqualTo("Lit (pas fatigué)"));

            needs.Model.Tick(20f * Minute, false);
            Assert.That(furniture.Action, Is.EqualTo("Dormir"));
            yield return Tap(Keyboard, new KeyboardState(Key.E), new KeyboardState());
            yield return new WaitForSeconds(1.2f);
            Assert.That(sleep.Asleep, "E on a bed did not put the player to sleep");
        }

        [UnityTest]
        public IEnumerator AWoundBleedsUntilABandageStopsIt()
        {
            typeof(PlayerHealth).GetField("bleedChance", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(Health, 1f);
            Health.ApplyHit(10f, Health.transform.position + Vector3.forward);
            Assert.That(Health.Bleeding);
            yield return new WaitForSeconds(2f);
            Assert.That(Health.Pool.Current, Is.EqualTo(89f).Within(0.2f), "0.5 per second after the 10 point hit");
            Assert.That(Root.Q("status-bleeding").style.display.value, Is.EqualTo(DisplayStyle.Flex));

            var inventory = Rig.GetComponentInChildren<PlayerInventory>();
            var bandage = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/DeadCounty/Data/Items/IT_Bandage.asset");
            inventory.Items.Add(bandage);
            Assert.That(inventory.Use(bandage));
            Assert.That(Health.Bleeding, Is.False);
            yield return null;
            Assert.That(Root.Q("status-bleeding").style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [UnityTest]
        public IEnumerator EmptyNeedsShowTheirAfflictions()
        {
            needs.Model.Tick(25f * Minute + 1f, false);
            yield return null;
            Assert.That(Root.Q("status-hungry").style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(Root.Q("status-thirsty").style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(Root.Q<Label>("status-tired").text, Is.EqualTo("ÉPUISÉ"));
            Assert.That(Root.Q("status-bleeding").style.display.value, Is.EqualTo(DisplayStyle.None));
        }
    }
}
#endif
