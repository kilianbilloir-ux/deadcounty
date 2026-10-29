using System;
using System.Linq;
using NUnit.Framework;

namespace DeadCounty.Tests
{
    // Synth lives in DeadCounty.Editor, which the test assemblies do not reference: it is reached by reflection.
    public class SynthTests
    {
        const int Rate = 32000;
        static readonly Type Synth = Type.GetType("DeadCounty.EditorTools.Synth, DeadCounty.Editor", true);

        static object Call(string method, params object[] args) => Synth.GetMethod(method).Invoke(null, args);
        static float[] Buffer(float seconds) => (float[])Call("Buffer", seconds);
        static float Loudness(float[] b) => (float)Call("Loudness", b);

        static float[] Noise(int seed, float seconds, float gain)
        {
            var b = Buffer(seconds);
            Call("White", b, new Random(seed), gain);
            return b;
        }

        [Test]
        public void TheRateMatchesTheBuffers()
        {
            Assert.That(Synth.GetField("Rate").GetValue(null), Is.EqualTo(Rate));
            Assert.That(Buffer(0.5f).Length, Is.EqualTo(Rate / 2));
        }

        [Test]
        public void TheSameSeedRendersTheSameSamples()
        {
            float[] Render(int seed) => (float[])Call("Grains", new Random(seed), 0.2f, 40, 1000f, 5000f, null);
            Assert.That(Render(5), Is.EqualTo(Render(5)));
            Assert.That(Render(5), Is.Not.EqualTo(Render(6)));
        }

        [Test]
        public void NormalizeSetsThePeak()
        {
            var b = Noise(1, 0.3f, 0.2f);
            Call("Normalize", b, 0.9f);
            Assert.That(b.Max(Math.Abs), Is.EqualTo(0.9f).Within(1e-5f));
        }

        [Test]
        public void NormalizeLoudnessMatchesAClickAndANoiseBedButCapsThePeak()
        {
            var bed = Noise(2, 0.5f, 0.05f);
            var click = Buffer(0.3f);
            Call("Ring", click, 0.01f, 2000f, 0.05f, 0.1f, 0f);
            Call("NormalizeLoudness", bed, -20f, 0.95f);
            Call("NormalizeLoudness", click, -20f, 0.95f);
            Assert.That(Loudness(bed), Is.EqualTo(-20f).Within(0.1f));
            Assert.That(Loudness(click), Is.EqualTo(-20f).Within(0.1f));

            Call("NormalizeLoudness", click, 0f, 0.5f);
            Assert.That(click.Max(Math.Abs), Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void LoopableKeepsTheLengthAndJoinsTheEndToTheStart()
        {
            var b = Buffer(2f);
            Call("Brown", b, new Random(3), 1f);
            Call("LowPass", b, 2000f, 0.707f);
            var length = b.Length;
            Call("Loopable", b, 0.2f);
            Assert.That(b.Length, Is.EqualTo(length));
            var steps = Enumerable.Range(1, b.Length - 1).Select(i => Math.Abs(b[i] - b[i - 1])).OrderBy(d => d).ToArray();
            var typical = steps[(int)(steps.Length * 0.99f)];
            Assert.That(Math.Abs(b[0] - b[b.Length - 1]), Is.LessThanOrEqualTo(typical), "the seam jumps more than 99 % of the steps inside the loop");
        }

        [Test]
        public void EnvelopeIsSilentAfterItsDecay()
        {
            var b = Noise(4, 0.5f, 1f);
            Call("Envelope", b, 0.01f, 0.05f, 0.1f);
            Assert.That(b.Skip((int)(0.17f * Rate)).All(s => s == 0f));
            Assert.That(b.Take((int)(0.06f * Rate)).Max(Math.Abs), Is.GreaterThan(0.5f));
        }

        [Test]
        public void TrimDropsTheSilentTail()
        {
            var b = Buffer(1f);
            Call("Ring", b, 0f, 500f, 0.1f, 1f, 0f);
            var trimmed = (float[])Call("Trim", b, -60f);
            Assert.That(trimmed.Length, Is.InRange((int)(0.09f * Rate), (int)(0.11f * Rate)));
            Assert.That(trimmed[trimmed.Length - 1], Is.EqualTo(0f));
        }
    }
}
