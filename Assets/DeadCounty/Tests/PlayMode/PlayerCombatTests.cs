#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using DeadCounty.AI;
using DeadCounty.Combat;
using DeadCounty.Core;
using DeadCounty.Items;
using DeadCounty.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace DeadCounty.Tests
{
    // The generated rig swinging at stand-in zombies: a ZombieHealth in a capsule on the Zombie layer, with no brain.
    public class PlayerCombatTests : PlayerRigTests
    {
        const string ItemsDir = "Assets/DeadCounty/Data/Items/";

        readonly List<GameObject> spawned = new();
        PlayerCombat combat;
        PlayerInventory inventory;
        Transform player;

        static ItemDefinition Load(string file) => AssetDatabase.LoadAssetAtPath<ItemDefinition>(ItemsDir + file + ".asset");

        [UnitySetUp]
        public IEnumerator SetUpCombat()
        {
            combat = Rig.GetComponentInChildren<PlayerCombat>();
            inventory = Rig.GetComponentInChildren<PlayerInventory>();
            player = combat.transform;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDownCombat()
        {
            foreach (var go in spawned) Object.Destroy(go);
            spawned.Clear();
            yield return null;
        }

        // `forward` and `right` are metres from the player along its own axes.
        ZombieHealth Dummy(float forward, float right = 0f)
        {
            var body = new GameObject("TestDummy") { layer = GameLayers.Zombie };
            spawned.Add(body);
            body.transform.position = player.position + player.forward * forward + player.right * right;
            var capsule = body.AddComponent<CapsuleCollider>();
            capsule.radius = 0.3f;
            capsule.height = 1.8f;
            capsule.center = new Vector3(0f, 0.9f, 0f);
            var health = body.AddComponent<ZombieHealth>();
            Physics.SyncTransforms();
            return health;
        }

        void Take(string file) => inventory.Receive(new[] { new ItemStack(Load(file), 1) });

        // In seconds: an empty scene runs hundreds of frames in the time of one swing.
        static IEnumerator Until(System.Func<bool> done, float maxSeconds = 3f)
        {
            for (var deadline = Time.time + maxSeconds; !done() && Time.time < deadline;) yield return null;
        }

        IEnumerator Swing()
        {
            Assert.That(combat.Begin(), Is.True, "the swing did not start");
            yield return Until(() => !combat.Swing.Busy);
            Assert.That(combat.Swing.Busy, Is.False, "the swing never ended");
        }

        [UnityTest]
        public IEnumerator TheBlowLandsOnTheZombieInFrontOnlyWhenTheWindupEnds()
        {
            Take("IT_Batte");
            var zombie = Dummy(1.2f);
            Assert.That(combat.Begin(), Is.True);
            yield return null;
            Assert.That(combat.Swing.Phase, Is.EqualTo(SwingPhase.Windup));
            Assert.That(zombie.Pool.Current, Is.EqualTo(100f), "hit before the wind-up ended");
            yield return Until(() => combat.Swing.Phase != SwingPhase.Windup);
            Assert.That(zombie.Pool.Current, Is.EqualTo(66f), "a bat takes 34 of a zombie's 100 hit points");
            Assert.That(zombie.LastStagger, Is.EqualTo(0.9f));
            Assert.That(zombie.LastKnockback, Is.EqualTo(0.5f));
        }

        [UnityTest]
        public IEnumerator ThreeBatBlowsKillAZombie()
        {
            Take("IT_Batte");
            var zombie = Dummy(1.2f);
            var kills = 0;
            combat.Struck += (_, killed) => kills += killed;
            for (var i = 0; i < 3; i++) yield return Swing();
            Assert.That(zombie.IsAlive, Is.False);
            Assert.That(kills, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ZombiesBehindOrOutOfReachAreMissed()
        {
            Take("IT_Batte");
            var behind = Dummy(-1f);
            var far = Dummy(2.2f);
            var beside = Dummy(0f, 1.2f);
            var hits = -1;
            combat.Struck += (landed, _) => hits = landed;
            yield return Swing();
            Assert.That(hits, Is.Zero);
            Assert.That(behind.Pool.Current + far.Pool.Current + beside.Pool.Current, Is.EqualTo(300f));
        }

        [UnityTest]
        public IEnumerator ABatReachesTwoZombiesAndAKnifeOne()
        {
            Take("IT_Batte");
            var near = Dummy(0.9f, -0.3f);
            var next = Dummy(1.2f, 0.4f);
            var third = Dummy(1.6f);
            yield return Swing();
            Assert.That(near.Pool.Current, Is.EqualTo(66f));
            Assert.That(next.Pool.Current, Is.EqualTo(66f));
            Assert.That(third.Pool.Current, Is.EqualTo(100f), "a bat hit a third zombie");

            inventory.Drop(Load("IT_Batte"));
            Take("IT_Couteau");
            yield return Swing();
            Assert.That(near.Pool.Current, Is.EqualTo(44f));
            Assert.That(next.Pool.Current, Is.EqualTo(66f), "a knife hit a second zombie");
        }

        [UnityTest]
        public IEnumerator NothingLandsThroughAWall()
        {
            Take("IT_Batte");
            var zombie = Dummy(1.3f);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            spawned.Add(wall);
            wall.transform.position = player.position + player.forward * 0.7f + Vector3.up;
            wall.transform.localScale = new Vector3(3f, 2f, 0.1f);
            Physics.SyncTransforms();
            yield return Swing();
            Assert.That(zombie.Pool.Current, Is.EqualTo(100f));
        }

        [UnityTest]
        public IEnumerator BareHandsShoveAndBarelyHurt()
        {
            var zombie = Dummy(0.9f);
            yield return Swing();
            Assert.That(combat.Weapon, Is.Null);
            Assert.That(zombie.Pool.Current, Is.EqualTo(96f));
            Assert.That(zombie.LastKnockback, Is.EqualTo(0.9f));
        }

        [UnityTest]
        public IEnumerator ASwingCostsStaminaAndAnExhaustedOneIsWeak()
        {
            Take("IT_Batte");
            var zombie = Dummy(1.2f);
            Assert.That(combat.Begin(), Is.True);
            Assert.That(Stamina.Model.Current, Is.EqualTo(91f));
            yield return Until(() => !combat.Swing.Busy);

            Stamina.Model.Exert(1000f);
            Assert.That(Stamina.Model.Exhausted, Is.True);
            yield return Swing();
            Assert.That(zombie.Pool.Current, Is.EqualTo(100f - 34f - 17f));
            Assert.That(zombie.LastKnockback, Is.Zero);
        }

        // The body faces away from the camera heading: the wind-up brings it round before the blow.
        [UnityTest]
        public IEnumerator TheWindupTurnsThePlayerToWhereTheCameraLooks()
        {
            var controller = Rig.GetComponentInChildren<ThirdPersonController>();
            Take("IT_PiedDeBiche");
            var zombie = Dummy(-1.2f);
            Assert.That(combat.Begin(), Is.True);
            controller.Commit(0.5f, controller.AimYaw + 180f);
            yield return Until(() => !combat.Swing.Busy);
            Assert.That(combat.Swing.Busy, Is.False, "the swing never ended");
            Assert.That(Vector3.Angle(player.forward, zombie.transform.position - player.position), Is.LessThan(8f));
            Assert.That(zombie.Pool.Current, Is.EqualTo(50f));
        }

        [UnityTest]
        public IEnumerator BeingHitBreaksAWindup()
        {
            Take("IT_PiedDeBiche");
            var zombie = Dummy(1.2f);
            Assert.That(combat.Begin(), Is.True);
            yield return null;
            Assert.That(Health.ApplyHit(10f, player.position + player.forward), Is.True);
            yield return null;
            Assert.That(combat.Swing.Busy, Is.False);
            yield return new WaitForSeconds(0.6f);
            Assert.That(zombie.Pool.Current, Is.EqualTo(100f));
        }

        [UnityTest]
        public IEnumerator TheAttackButtonSwings()
        {
            yield return null;
            yield return Tap(Gamepad, new GamepadState { rightTrigger = 1f }, new GamepadState());
            Assert.That(combat.Swing.Busy, Is.True);
        }

        [UnityTest]
        public IEnumerator EmptyHandsTakeTheFirstWeaponFoundAndUsingItPutsItAway()
        {
            var bat = Load("IT_Batte");
            Assert.That(inventory.Weapon, Is.Null);
            Take("IT_Batte");
            Assert.That(inventory.Weapon, Is.EqualTo(bat));
            Assert.That(inventory.Items.Count(bat), Is.EqualTo(1), "a weapon in hand stays in the inventory");
            Take("IT_Couteau");
            Assert.That(inventory.Weapon, Is.EqualTo(bat), "a second weapon replaced the one in hand");
            yield return null;
            Assert.That(InHand(), Is.Not.Null, "the weapon is not shown in the hand");

            Assert.That(inventory.UseVerb(bat), Is.EqualTo("Ranger"));
            Assert.That(inventory.Use(bat), Is.True);
            Assert.That(inventory.Weapon, Is.Null);
            Assert.That(inventory.Items.Count(bat), Is.EqualTo(1));
            yield return null;
            Assert.That(InHand(), Is.Null);

            Assert.That(inventory.UseVerb(bat), Is.EqualTo("Équiper"));
            Assert.That(inventory.Use(Load("IT_Couteau")), Is.True);
            Assert.That(inventory.Weapon, Is.EqualTo(Load("IT_Couteau")));
        }

        [UnityTest]
        public IEnumerator DroppingTheWeaponInHandLeavesBareHands()
        {
            Take("IT_Batte");
            Assert.That(inventory.Drop(Load("IT_Batte")), Is.True);
            Assert.That(inventory.Weapon, Is.Null);
            yield return null;
            Assert.That(InHand(), Is.Null);
            foreach (var pickup in Object.FindObjectsByType<WorldItem>(FindObjectsInactive.Include)) Object.Destroy(pickup.gameObject);
        }

        Transform InHand()
        {
            foreach (var part in player.GetComponentsInChildren<Transform>())
                if (part.name == "Arme_En_Main") return part;
            return null;
        }
    }
}
#endif
