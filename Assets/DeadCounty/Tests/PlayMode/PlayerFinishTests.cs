#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using DeadCounty.AI;
using DeadCounty.Audio;
using DeadCounty.Items;
using DeadCounty.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace DeadCounty.Tests
{
    // The generated rig against real zombies on a NavMesh: felling them and finishing them on the ground, and what
    // each blow sounds like (generated cues, by asset name). The zombies perceive a stub target, not the rig.
    public class PlayerFinishTests : PlayerRigTests
    {
        ZombieTestWorld world;
        PlayerCombat combat;
        PlayerInventory inventory;
        Transform player;
        readonly List<string> played = new();

        [UnitySetUp]
        public IEnumerator SetUpWorld()
        {
            combat = Rig.GetComponentInChildren<PlayerCombat>();
            inventory = Rig.GetComponentInChildren<PlayerInventory>();
            player = combat.transform;
            played.Clear();
            combat.GetComponent<PlayerActionAudio>().Played += (cue, _) => played.Add(cue.name);
            // Bare hands, so the weapon a test takes goes straight to the hand.
            inventory.Use(inventory.Weapon);
            world = new ZombieTestWorld(1f);
            world.Target.Visible = false;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDownWorld()
        {
            yield return world.Teardown();
        }

        // `forward` and `right` in metres from the player along its own axes, facing it or turned away.
        ZombieAgent Zombie(float forward, bool facingPlayer, float right = 0f) =>
            world.SpawnZombie(player.position + player.forward * forward + player.right * right, player.eulerAngles.y + (facingPlayer ? 180f : 0f));

        // Facing the player, felled from behind so that it falls face down toward the player.
        IEnumerator FelledTowardThePlayer(ZombieAgent zombie)
        {
            yield return ZombieTestWorld.Settle(zombie);
            Assert.That(zombie.Health.ApplyDamage(1f, zombie.Position + player.forward, 0f, 0f, true), Is.True);
            yield return new WaitForSeconds(0.6f);
            Assert.That(zombie.IsDown, Is.True);
        }

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
        public IEnumerator AShoveInTheBackAlwaysFellsAZombie()
        {
            var zombie = Zombie(0.9f, false);
            yield return ZombieTestWorld.Settle(zombie);
            yield return Swing();
            Assert.That(combat.Ground, Is.False);
            Assert.That(zombie.State, Is.EqualTo(ZombieState.Down), "a shove in the back left the zombie standing");
            Assert.That(zombie.Health.LastKnockdown, Is.True);
            Assert.That(played, Does.Contain("SC_Shove"));
        }

        [UnityTest]
        public IEnumerator BareHandsStompAZombieOnTheGroundForDoubleDamageAndKeepItThere()
        {
            var zombie = Zombie(2f, true);
            yield return FelledTowardThePlayer(zombie);
            var lying = zombie.Brain.StateTime;
            yield return Swing();
            Assert.That(combat.Ground, Is.True, "the attack did not come down on the body");
            Assert.That(zombie.Health.Pool.Current, Is.EqualTo(100f - 1f - 26f), "a stomp takes twice 13 hit points");
            Assert.That(zombie.State, Is.EqualTo(ZombieState.Down));
            Assert.That(zombie.Brain.StateTime, Is.LessThan(lying), "the stomp did not start the lie over");
            Assert.That(played, Is.EquivalentTo(new[] { "SC_Effort", "SC_Stomp" }), "a stomp grunts and lands, it does not whoosh");
        }

        [UnityTest]
        public IEnumerator ACrowbarFinishesAZombieOnTheGroundInOneBlow()
        {
            inventory.Receive(new[] { new ItemStack(AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/DeadCounty/Data/Items/IT_PiedDeBiche.asset"), 1) });
            var zombie = Zombie(2f, true);
            yield return FelledTowardThePlayer(zombie);
            var kills = 0;
            combat.Struck += (_, killed) => kills += killed;
            yield return Swing();
            Assert.That(combat.Ground, Is.True);
            Assert.That(zombie.Health.IsAlive, Is.False);
            Assert.That(kills, Is.EqualTo(1));
            Assert.That(played, Is.SupersetOf(new[] { "SC_Swing_Heavy", "SC_Hit_Crowbar", "SC_Kill" }));
        }

        [UnityTest]
        public IEnumerator ASwingAtAStandingZombiePassesOverABodyOnTheGround()
        {
            inventory.Receive(new[] { new ItemStack(AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/DeadCounty/Data/Items/IT_Batte.asset"), 1) });
            var down = Zombie(2.1f, true);
            var standing = Zombie(1f, true, 0.5f);
            yield return ZombieTestWorld.Settle(standing);
            yield return FelledTowardThePlayer(down);
            yield return Swing();
            Assert.That(combat.Ground, Is.False, "the nearest zombie stands, yet the attack went for the ground");
            Assert.That(standing.Health.Pool.Current, Is.LessThan(100f));
            Assert.That(down.Health.Pool.Current, Is.EqualTo(99f), "a swing across hit the body on the ground");
            Assert.That(played, Does.Contain("SC_Hit_Bat").And.Not.Contain("SC_Kill"));
        }
    }
}
#endif
