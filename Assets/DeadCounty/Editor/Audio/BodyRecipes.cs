using System;
using static DeadCounty.EditorTools.StepRecipes;
using static DeadCounty.EditorTools.Synth;

namespace DeadCounty.EditorTools
{
    // The survivor's own body: breathing, effort, pain, heartbeat. Breath is noise shaped by the formants of an
    // open mouth; voiced sounds are jittered glottal pulses through the formants of a vowel (source-filter model).
    public static class BodyRecipes
    {
        // Formant centre, Q and gain of the vowels used (adult male, Peterson and Barney).
        static readonly (float Hz, float Q, float Gain)[] Uh = { (640f, 6f, 1f), (1190f, 8f, 0.5f), (2390f, 10f, 0.25f) };
        static readonly (float Hz, float Q, float Gain)[] Ah = { (730f, 6f, 1f), (1090f, 8f, 0.6f), (2440f, 10f, 0.25f) };
        static readonly (float Hz, float Q, float Gain)[] Eh = { (530f, 6f, 1f), (1840f, 9f, 0.45f), (2480f, 10f, 0.25f) };

        // Exhaled through the mouth, "haah"; voiced adds a faint groan for the exhausted variants.
        public static float[] BreathOut(Random rng, bool voiced)
        {
            var seconds = R(rng, 0.35f, 0.5f);
            var b = Breath(rng, seconds, R(rng, 600f, 800f), R(rng, 1100f, 1400f), 400f);
            if (voiced)
            {
                var f0 = R(rng, 100f, 125f);
                Mix(b, Voice(rng, seconds, t => f0 * (1f - 0.1f * t / seconds), Uh, 0.02f), 0f, 0.35f);
            }
            Shape(b, t => Swell(t, seconds, 0.12f));
            Normalize(b, 0.8f);
            return b;
        }

        // Drawn in quickly, higher and hissier than the exhale.
        public static float[] BreathIn(Random rng)
        {
            var seconds = R(rng, 0.28f, 0.4f);
            var b = Breath(rng, seconds, R(rng, 420f, 520f), R(rng, 1700f, 2100f), 1200f);
            Shape(b, t => Swell(t, seconds, 0.6f));
            Normalize(b, 0.6f);
            return b;
        }

        // Short pained grunt: glottal onset, falling pitch, a breathy tail.
        public static float[] Hurt(Random rng)
        {
            var seconds = R(rng, 0.22f, 0.34f);
            var f0 = R(rng, 140f, 185f);
            var vowel = rng.Next(3) switch { 0 => Uh, 1 => Ah, _ => Eh };
            var b = Buffer(seconds + 0.15f);
            Mix(b, Voice(rng, seconds, t => f0 * (1f - 0.3f * t / seconds), vowel, 0.03f));
            Shape(b, t => t < 0.012f ? t / 0.012f : (float)Math.Exp(-3.5f * Math.Max(0f, t - 0.06f) / seconds));
            var gasp = Breath(rng, 0.25f, 700f, 1300f, 500f);
            Envelope(gasp, 0.01f, 0.02f, 0.2f);
            Mix(b, gasp, seconds * 0.6f, 0.25f);
            Saturate(b, 2f);
            LowPass(b, 5000f);
            HighPass(b, 90f);
            Normalize(b, 0.9f);
            return b;
        }

        // Long falling groan, then the body hits the ground and the last breath leaves it.
        public static float[] Death(Random rng)
        {
            var seconds = R(rng, 0.9f, 1.15f);
            var f0 = R(rng, 125f, 145f);
            var b = Buffer(seconds + 0.9f);
            var groan = Voice(rng, seconds, t => f0 * (float)Math.Exp(-0.55 * t / seconds), Uh, 0.06f);
            Shape(groan, t => Math.Min(1f, t / 0.05f) * (float)Math.Pow(Math.Max(0f, 1f - t / seconds), 0.7));
            Mix(b, groan);
            var fall = seconds * R(rng, 0.55f, 0.7f);
            Thump(b, rng, fall, 0.9f, 60f);
            Ring(b, fall, R(rng, 55f, 65f), 0.18f, 0.6f, 1f);
            Filtered(b, Grains(rng, 0.15f, 14, 1500f, 4000f), fall + 0.02f, 0.4f, f => HighPass(f, 1200f));
            var last = Breath(rng, 0.6f, 600f, 1100f, 300f);
            Shape(last, t => Swell(t, 0.6f, 0.15f));
            Mix(b, last, seconds + 0.1f, 0.3f);
            Saturate(b, 1.5f);
            HighPass(b, 40f);
            Normalize(b, 0.9f);
            return b;
        }

        // Lub-dub. The 110 Hz overtone keeps it audible on speakers that cannot play 50 Hz.
        public static float[] Heartbeat(Random rng)
        {
            var b = Buffer(0.6f);
            var dub = R(rng, 0.26f, 0.3f);
            Ring(b, 0.005f, R(rng, 50f, 56f), 0.16f, 1f, 0.5f);
            Ring(b, 0.005f, R(rng, 105f, 115f), 0.07f, 0.5f);
            Filtered(b, Burst(rng, 0.004f, 0.1f), 0.005f, 0.6f, f => LowPass(f, 90f));
            Ring(b, dub, R(rng, 64f, 72f), 0.11f, 0.7f, 0.4f);
            Ring(b, dub, R(rng, 125f, 140f), 0.05f, 0.35f);
            Filtered(b, Burst(rng, 0.003f, 0.07f), dub, 0.4f, f => LowPass(f, 120f));
            HighPass(b, 30f);
            Normalize(b, 0.9f);
            return b;
        }

        // Push-off: a short "hup" and the clothes and pack moving.
        public static float[] Jump(Random rng)
        {
            var b = Buffer(0.4f);
            var hup = Breath(rng, 0.16f, R(rng, 650f, 800f), R(rng, 1100f, 1300f), 500f);
            Envelope(hup, 0.01f, 0.03f, 0.12f);
            Mix(b, hup, 0f, 0.6f);
            Filtered(b, Burst(rng, 0.02f, 0.18f), 0.01f, 0.35f, f => BandPass(f, R(rng, 2000f, 2800f), 0.5f));
            Filtered(b, Grains(rng, 0.12f, 8, 1500f, 4000f), 0.05f, 0.3f, f => HighPass(f, 1200f));
            HighPass(b, 80f);
            Normalize(b, 0.7f);
            return b;
        }

        // The body landing, under the surface step played with it: chest thump and the gear jolting.
        public static float[] Land(Random rng)
        {
            var b = Buffer(0.45f);
            Ring(b, 0.003f, R(rng, 55f, 65f), 0.14f, 0.9f, 1f);
            Filtered(b, Burst(rng, 0.002f, 0.1f), 0.003f, 0.7f, f => LowPass(f, 200f));
            Filtered(b, Grains(rng, 0.12f, 10, 1500f, 4000f), 0.02f, 0.4f, f => HighPass(f, 1200f));
            Filtered(b, Burst(rng, 0.01f, 0.12f), 0.01f, 0.2f, f => BandPass(f, 2000f, 0.6f));
            HighPass(b, 35f);
            Normalize(b, 0.9f);
            return b;
        }

        // Turbulent air through the vocal tract: noise through two formants plus a wide band of breathiness.
        public static float[] Breath(Random rng, float seconds, float f1, float f2, float hiss)
        {
            var noise = Buffer(seconds);
            White(noise, rng);
            var b = Buffer(seconds);
            foreach (var (hz, q, g) in new[] { (f1, 4f, 1f), (f2, 5f, 0.6f), (2600f, 6f, 0.35f) })
            {
                var copy = (float[])noise.Clone();
                BandPass(copy, hz * R(rng, 0.95f, 1.05f), q);
                Mix(b, copy, 0f, g);
            }
            HighPass(noise, hiss);
            BandPass(noise, 2500f, 0.7f);
            Mix(b, noise, 0f, 0.12f);
            LowPass(b, 4500f);
            LowPass(b, 4500f);
            return b;
        }

        // Glottal pulses with pitch jitter, plus some aspiration, through a vowel's formants.
        public static float[] Voice(Random rng, float seconds, Func<float, float> f0, (float Hz, float Q, float Gain)[] vowel, float breathiness)
        {
            var source = Buffer(seconds);
            var jitter = Drift(rng, 25f, seconds);
            Pulses(source, rng, t => f0(t) * (1f + 0.03f * jitter(t)), 0.03f);
            LowPass(source, 2500f, 0.5f);
            White(source, rng, breathiness * 0.3f);
            var b = Buffer(seconds);
            foreach (var (hz, q, g) in vowel)
            {
                var copy = (float[])source.Clone();
                BandPass(copy, hz * R(rng, 0.96f, 1.04f), q);
                Mix(b, copy, 0f, g);
            }
            LowPass(b, 3500f);
            Normalize(b, 1f);
            return b;
        }

        // 0 to 1 and back over `seconds`, peaking at `peakAt` (fraction), with soft ends.
        static float Swell(float t, float seconds, float peakAt)
        {
            var x = Math.Clamp(t / seconds, 0f, 1f);
            var u = x < peakAt ? x / peakAt : 1f - (x - peakAt) / (1f - peakAt);
            return (float)Math.Sin(u * Math.PI / 2.0);
        }
    }
}
