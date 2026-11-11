using System.Reflection;
using System.Text.RegularExpressions;
using DeadCounty.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace DeadCounty.Tests
{
    public class DayNightDirectorTests
    {
        static void SetPrivate(DayNightDirector director, string field, float value) =>
            typeof(DayNightDirector).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(director, value);

        // DontSave keeps the open scene clean; Awake never runs in EditMode (not at
        // AddComponent, not at SetActive), so tests invoke it explicitly like the game does.
        static GameObject InactiveRig(string name)
        {
            var rig = EditorUtility.CreateGameObjectWithHideFlags(name, HideFlags.DontSave);
            rig.SetActive(false);
            return rig;
        }

        static void RunAwake(DayNightDirector director) =>
            typeof(DayNightDirector).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(director, null);

        [Test]
        public void AwakeFallsBackWhenSerializedDayLengthIsZero()
        {
            WorldLighting.ResetStatics();
            var rig = InactiveRig("TestDayLengthFallback");
            try
            {
                var sun = rig.AddComponent<Light>();
                sun.type = LightType.Directional;
                var director = rig.AddComponent<DayNightDirector>();
                director.Configure(sun, 11f, 600f);
                SetPrivate(director, "dayLengthSeconds", 0f);
                LogAssert.Expect(LogType.Error, new Regex("invalid serialized dayLengthSeconds"));
                RunAwake(director);
                Assert.That(director.enabled, Is.True, "fallback keeps running");
                Assert.That(float.IsNaN(director.Hour), Is.False);
                Assert.That(WorldLighting.NightFactor, Is.InRange(0f, 1f));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Object.DestroyImmediate(rig);
                WorldLighting.ResetStatics();
            }
        }

        [Test]
        public void AwakeFallsBackWhenSerializedStartHourIsNaN()
        {
            WorldLighting.ResetStatics();
            var rig = InactiveRig("TestStartHourFallback");
            try
            {
                var sun = rig.AddComponent<Light>();
                sun.type = LightType.Directional;
                var director = rig.AddComponent<DayNightDirector>();
                director.Configure(sun, 11f, 600f);
                SetPrivate(director, "startHour", float.NaN);
                LogAssert.Expect(LogType.Error, new Regex("invalid serialized startHour"));
                RunAwake(director);
                Assert.That(director.Hour, Is.EqualTo(DayNightModel.StartHour));
                Assert.That(WorldLighting.NightFactor, Is.InRange(0f, 1f));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Object.DestroyImmediate(rig);
                WorldLighting.ResetStatics();
            }
        }
    }
}
