using System.Collections;
using System.Collections.Generic;
using DeadCounty.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DeadCounty.Tests
{
    // Night lamps follow NightFactor with hysteresis, independently of the day/night scene rig.
    public class NightLampControllerTests
    {
        readonly List<GameObject> owned = new();
        float savedNightFactor;

        [SetUp]
        public void SetUp()
        {
            savedNightFactor = WorldLighting.NightFactor;
            NightLamps.ResetStatics();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            WorldLighting.NightFactor = savedNightFactor;
            NightLamps.ResetStatics();
            foreach (var go in owned) if (go != null) Object.Destroy(go);
            owned.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator NightFactorOneTurnsAllOnZeroTurnsAllOff()
        {
            var spotA = Spot("SpotA");
            var spotB = Spot("SpotB");
            var go = new GameObject("TestNightLamps");
            owned.Add(go);
            go.SetActive(false);
            var controller = go.AddComponent<NightLampController>();
            controller.Configure(new List<Light> { spotA, spotB });
            go.SetActive(true);

            WorldLighting.NightFactor = 1f;
            yield return null;
            yield return null;
            Assert.That(spotA.enabled && spotB.enabled, Is.True, "lamps did not turn on at night");

            WorldLighting.NightFactor = 0f;
            yield return null;
            yield return null;
            Assert.That(!spotA.enabled && !spotB.enabled, Is.True, "lamps did not turn off at day");
        }

        [UnityTest]
        public IEnumerator HysteresisHoldsAtMidpoint()
        {
            var spot = Spot("SpotH");
            var go = new GameObject("TestNightHyst");
            owned.Add(go);
            go.SetActive(false);
            var controller = go.AddComponent<NightLampController>();
            controller.Configure(new List<Light> { spot });
            go.SetActive(true);

            WorldLighting.NightFactor = 1f;
            yield return null;
            yield return null;
            Assert.That(spot.enabled, Is.True);
            WorldLighting.NightFactor = 0.5f;
            yield return null;
            yield return null;
            Assert.That(spot.enabled, Is.True, "flickered off in the dead band after night");

            WorldLighting.NightFactor = 0f;
            yield return null;
            yield return null;
            Assert.That(spot.enabled, Is.False);
            WorldLighting.NightFactor = 0.5f;
            yield return null;
            yield return null;
            Assert.That(spot.enabled, Is.False, "flickered on in the dead band after day");
        }

        Light Spot(string name)
        {
            var go = new GameObject(name);
            owned.Add(go);
            var light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            return light;
        }
    }
}
