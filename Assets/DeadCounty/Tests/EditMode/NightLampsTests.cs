using DeadCounty.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DeadCounty.Tests
{
    public class NightLampsTests
    {
        [TearDown]
        public void TearDown() => NightLamps.ResetStatics();

        [TestCase(false, 0f, false)]
        [TestCase(false, 0.54f, false)]
        [TestCase(false, 0.55f, true)]
        [TestCase(false, 1f, true)]
        [TestCase(true, 1f, true)]
        [TestCase(true, 0.46f, true)]
        [TestCase(true, 0.45f, false)]
        [TestCase(true, 0f, false)]
        public void HysteresisHoldsBetweenThresholds(bool currentlyOn, float nightFactor, bool expected) =>
            Assert.That(NightLamps.ShouldBeOn(currentlyOn, nightFactor), Is.EqualTo(expected));

        [Test]
        public void RegisterUnregisterReset()
        {
            var a = EditorUtility.CreateGameObjectWithHideFlags("LampA", HideFlags.DontSave).AddComponent<Light>();
            var b = EditorUtility.CreateGameObjectWithHideFlags("LampB", HideFlags.DontSave).AddComponent<Light>();
            try
            {
                Assert.That(NightLamps.Count, Is.EqualTo(0));
                NightLamps.Register(a);
                NightLamps.Register(a);
                Assert.That(NightLamps.Count, Is.EqualTo(1), "double registration must be idempotent");
                NightLamps.Register(b);
                Assert.That(NightLamps.Count, Is.EqualTo(2));
                NightLamps.Unregister(a);
                Assert.That(NightLamps.Count, Is.EqualTo(1));
                NightLamps.ResetStatics();
                Assert.That(NightLamps.Count, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(a.gameObject);
                Object.DestroyImmediate(b.gameObject);
            }
        }
    }
}
