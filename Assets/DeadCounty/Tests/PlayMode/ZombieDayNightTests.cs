using System.Collections;
using DeadCounty.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DeadCounty.Tests
{
    // The day/night cycle moves NightFactor and turns the sun in a live scene.
    public class ZombieDayNightTests : ZombieWorldTests
    {
        [UnityTest]
        public IEnumerator CycleMovesNightFactorAndTurnsTheSun()
        {
            World = new ZombieTestWorld(4f);
            var rig = new GameObject("TestDayNight");
            rig.SetActive(false);
            var sun = rig.AddComponent<Light>();
            sun.type = LightType.Directional;
            var director = rig.AddComponent<DayNightDirector>();
            director.Configure(sun, 19f, 4f);
            rig.SetActive(true);
            var first = sun.transform.rotation;
            var min = 1f;
            var max = 0f;
            Quaternion last;
            float hour;
            // 90 frames at x4 with a 4 s cycle cover 1.5 cycles (19h to 7h): both extremes are hit.
            try
            {
                for (var i = 0; i < 90; i++)
                {
                    yield return null;
                    min = Mathf.Min(min, WorldLighting.NightFactor);
                    max = Mathf.Max(max, WorldLighting.NightFactor);
                }
                last = sun.transform.rotation;
                hour = director.Hour;
            }
            finally
            {
                Object.DestroyImmediate(rig);
            }
            Assert.That(min, Is.LessThan(0.05f), "the cycle never reached day");
            Assert.That(max, Is.GreaterThan(0.95f), "the cycle never reached night");
            Assert.That(Quaternion.Angle(first, last), Is.GreaterThan(90f), "the sun never turned");
            Assert.That(hour, Is.EqualTo(7f).Within(1.5f), "the clock did not advance a cycle and a half");
        }
    }
}
