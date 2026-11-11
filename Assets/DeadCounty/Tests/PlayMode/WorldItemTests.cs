#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using DeadCounty.Items;
using DeadCounty.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace DeadCounty.Tests
{
    // Items on the ground next to the generated rig: their model, and placed items coming back on respawn.
    public class WorldItemTests : PlayerRigTests
    {
        ItemDefinition water;
        WorldItem lying;

        [UnitySetUp]
        public IEnumerator SetUpItem()
        {
            water = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/DeadCounty/Data/Items/IT_Eau.asset");
            var player = Health.transform;
            lying = WorldItem.Spawn(water, 1, player.position + player.forward * 1f, Quaternion.identity);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDownItem()
        {
            if (lying != null) Object.Destroy(lying.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ADroppedItemIsDrawnWithItsModel()
        {
            Assert.That(water.WorldModel, Is.Not.Null, "the builder gives every item a ground model");
            Assert.That(lying.transform.Find("Bouteille"), Is.Not.Null, "a bottle of water must look like one");
            Assert.That(lying.GetComponent<BoxCollider>(), Is.Not.Null, "the interaction raycasts need a collider");
            yield return null;
        }

        [UnityTest]
        public IEnumerator APlacedItemComesBackWhenThePlayerRespawns()
        {
            typeof(WorldItem).GetField("placed", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(lying, true);
            lying.Interact(Health.transform, false);
            Assert.That(lying.gameObject.activeSelf, Is.False, "taken");

            var flow = Rig.GetComponent<PlayerDeathFlow>();
            typeof(PlayerDeathFlow).GetField("restartDelay", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(flow, 0.1f);
            Health.ApplyHit(10000f, Health.transform.position + Vector3.forward);
            yield return new WaitForSeconds(0.2f);
            yield return Tap(Keyboard, new KeyboardState(Key.Space), new KeyboardState());
            Assert.That(Health.IsAlive);
            Assert.That(lying.gameObject.activeSelf, "a placed item must be back after the respawn, like the furniture's loot");
        }
    }
}
#endif
