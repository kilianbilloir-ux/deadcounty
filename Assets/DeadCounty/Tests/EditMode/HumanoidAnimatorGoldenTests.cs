using System.Globalization;
using System.IO;
using System.Text;
using DeadCounty.Animation;
using NUnit.Framework;
using UnityEngine;

namespace DeadCounty.Tests
{
    // Regression guard of the player's procedural animation: with the default (GP_Survivor) profile, every
    // joint the animator writes, sampled at fixed frames of 3 s runs at constant speed (idle, walk, run,
    // sprint, crouch, a walking turn with the head aimed away until it recenters, a turn on the spot), must
    // match HumanoidAnimatorGolden. After an intended change of the player's animation, run Record and paste
    // its output into HumanoidAnimatorGolden.cs.
    public class HumanoidAnimatorGoldenTests
    {
        const int Frames = 180;
        const float Tolerance = 1e-4f;
        static readonly int[] SampledFrames = { 50, 120, 179 };

        public struct Case
        {
            public string Name;
            public float Speed, YawRate, AimOffset, AimPitch;
            public bool Crouch, AimFixed;

            public override string ToString() => Name;
        }

        public static readonly Case[] Cases =
        {
            new() { Name = "Idle", Speed = 0f },
            new() { Name = "Walk", Speed = 1.5f },
            new() { Name = "Run", Speed = 3.5f },
            new() { Name = "Sprint", Speed = 6f },
            new() { Name = "Crouch", Speed = 1f, Crouch = true },
            new() { Name = "WalkTurn", Speed = 1.5f, YawRate = 60f, AimFixed = true, AimPitch = 15f },
            new() { Name = "TurnInPlace", Speed = 0f, YawRate = 120f, AimOffset = 40f, AimPitch = -10f },
        };

        static float[][] Run(Case c)
        {
            using var rig = new HumanoidTestRig(ScriptableObject.CreateInstance<GaitProfile>());
            rig.Initialize();
            rig.Motion.IsCrouching = c.Crouch;
            rig.Motion.AimPitch = c.AimPitch;
            var samples = new float[SampledFrames.Length][];
            var next = 0;
            for (var frame = 0; frame < Frames; frame++)
            {
                rig.Motion.AimYaw = c.AimFixed ? 0f : (frame + 1) * c.YawRate * HumanoidTestRig.Dt + c.AimOffset;
                rig.Step(c.Speed, c.YawRate);
                if (next < SampledFrames.Length && frame == SampledFrames[next]) samples[next++] = rig.Sample();
            }
            return samples;
        }

        [TestCaseSource(nameof(Cases))]
        public void MatchesRecordedPose(Case c)
        {
            var expected = HumanoidAnimatorGolden.Samples[c.Name];
            var actual = Run(c);
            for (var s = 0; s < SampledFrames.Length; s++)
            {
                var a = actual[s];
                var e = expected[s];
                Assert.That(a.Length, Is.EqualTo(e.Length));
                for (var i = 0; i < a.Length; i++)
                {
                    // q and -q are the same rotation.
                    var r = HumanoidTestRig.RotationStartOf(i);
                    var sign = r >= 0 && a[r] * e[r] + a[r + 1] * e[r + 1] + a[r + 2] * e[r + 2] + a[r + 3] * e[r + 3] < 0f ? -1f : 1f;
                    Assert.That(a[i] * sign, Is.EqualTo(e[i]).Within(Tolerance), $"{c.Name} frame {SampledFrames[s]} value {i}");
                }
            }
        }

        [Test, Explicit("Rewrites the golden values, only after an intended change of the player's animation")]
        public void Record()
        {
            var code = new StringBuilder();
            foreach (var c in Cases)
            {
                code.Append("            [\"").Append(c.Name).AppendLine("\"] = new[]");
                code.AppendLine("            {");
                foreach (var sample in Run(c))
                {
                    code.Append("                new[] { ");
                    for (var i = 0; i < sample.Length; i++)
                        code.Append(sample[i].ToString("0.000000", CultureInfo.InvariantCulture)).Append(i < sample.Length - 1 ? "f, " : "f");
                    code.AppendLine(" },");
                }
                code.AppendLine("            },");
            }
            var path = Path.Combine(Application.dataPath, "..", "Temp", "HumanoidAnimatorGolden.txt");
            File.WriteAllText(path, code.ToString());
            TestContext.WriteLine($"Written to {Path.GetFullPath(path)}");
        }
    }
}
