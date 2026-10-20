using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DeadCounty.Tests
{
    public class PlayerRigPrefabTests
    {
        const string RigPath = "Assets/DeadCounty/Prefabs/PlayerRig.prefab";

        // At high frame rates a start from rest moves less than a millimetre per frame: a non-zero threshold
        // froze the player in place.
        [Test]
        public void CharacterControllerMovesEvenTinySteps()
        {
            var rig = AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
            Assert.That(rig, Is.Not.Null, RigPath);
            Assert.That(rig.GetComponentInChildren<CharacterController>(true).minMoveDistance, Is.Zero);
        }
    }
}
