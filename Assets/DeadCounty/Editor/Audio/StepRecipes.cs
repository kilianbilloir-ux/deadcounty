using System;
using static DeadCounty.EditorTools.Synth;

namespace DeadCounty.EditorTools
{
    // Boot footsteps per ground, after Farnell (Designing Sound, "Footsteps"): a walking step is a heel strike then
    // the ball of the foot about 90 ms later; a running step lands both within 35 ms and harder, then scuffs. Each
    // contact excites the ground (grit, pebbles, planks, plate modes) on top of the low thump of the body.
    // Levels are set by loudness, not peak, so a grass swish and a concrete click sit where they should.
    public static class StepRecipes
    {
        const float Length = 0.42f;

        delegate void Contact(float[] b, Random rng, float at, float gain, float sharp);

        public static float[] Concrete(Random rng, bool run) => Step(rng, run, -20f, 1f, (b, r, at, g, sharp) =>
        {
            Thump(b, r, at, g * 0.4f, 95f);
            Filtered(b, Burst(r, 0.0003f, 0.02f), at, g * 0.9f * sharp, f => BandPass(f, R(r, 3000f, 4200f), 0.7f));
            Filtered(b, Burst(r, 0.0005f, 0.035f), at, g * 0.55f, f => BandPass(f, R(r, 900f, 1200f), 1.2f));
            Filtered(b, Grains(r, 0.04f, 10, 3000f, 8000f), at, g * 0.4f * sharp, f => HighPass(f, 2500f));
        });

        public static float[] Asphalt(Random rng, bool run) => Step(rng, run, -20f, 1.3f, (b, r, at, g, sharp) =>
        {
            Thump(b, r, at, g * 0.65f, 90f);
            Filtered(b, Burst(r, 0.0004f, 0.014f), at, g * 0.65f * sharp, f => BandPass(f, R(r, 2000f, 2700f), 0.7f));
            Filtered(b, Burst(r, 0.0005f, 0.03f), at, g * 0.45f, f => BandPass(f, R(r, 650f, 800f), 1f));
            Filtered(b, Grains(r, 0.05f, 20, 2500f, 7000f), at, g * 0.6f * sharp, f => HighPass(f, 1500f));
        });

        public static float[] Wood(Random rng, bool run)
        {
            var plank = R(rng, 140f, 190f);
            var creaks = rng.NextDouble() < 0.35;
            return Step(rng, run, -20f, 0.5f, (b, r, at, g, sharp) =>
            {
                Thump(b, r, at, g * 0.5f, 85f);
                Ring(b, at, plank * R(r, 0.97f, 1.03f), 0.14f, g * 0.35f);
                Ring(b, at, plank * R(r, 2.3f, 2.5f), 0.09f, g * 0.25f);
                Ring(b, at, plank * R(r, 3.8f, 4.1f), 0.06f, g * 0.16f);
                Ring(b, at, plank * R(r, 5.9f, 6.4f), 0.04f, g * 0.09f);
                Filtered(b, Burst(r, 0.0005f, 0.02f), at, g * 0.7f, f => BandPass(f, R(r, 1000f, 1300f), 1.5f));
                Filtered(b, Burst(r, 0.0002f, 0.006f), at, g * 0.3f * sharp, f => HighPass(f, 3000f));
            }, b =>
            {
                if (creaks) Creak(b, rng, R(rng, 0.12f, 0.2f), R(rng, 0.12f, 0.18f), 0.12f);
            });
        }

        public static float[] Metal(Random rng, bool run)
        {
            var f0 = R(rng, 280f, 380f);
            var ratios = new[] { 1f, 2.32f, 4.25f, 6.63f, 9.38f };
            var decays = new[] { 0.22f, 0.17f, 0.12f, 0.08f, 0.06f };
            var gains = new[] { 0.22f, 0.18f, 0.13f, 0.09f, 0.06f };
            return Step(rng, run, -19f, 0.6f, (b, r, at, g, sharp) =>
            {
                Thump(b, r, at, g * 0.45f, 110f);
                Filtered(b, Burst(r, 0.0002f, 0.005f), at, g * 0.6f * sharp, f => HighPass(f, 2500f));
                for (var m = 0; m < ratios.Length; m++)
                    Ring(b, at, f0 * ratios[m] * R(r, 0.99f, 1.01f), decays[m] * R(r, 0.8f, 1.2f), g * gains[m] * (m == 0 ? 1f : sharp));
            });
        }

        public static float[] Gravel(Random rng, bool run) => Step(rng, run, -20f, 0.8f, (b, r, at, g, sharp) =>
        {
            Thump(b, r, at, g * 0.4f, 80f);
            Filtered(b, Burst(r, 0.004f, 0.09f), at, g * 0.35f, f => BandPass(f, R(r, 1100f, 1500f), 0.8f));
            Filtered(b, Grains(r, 0.11f, 45, 1500f, 6500f, t => Math.Max(0.1f, 1f - t / 0.11f)), at, g * 1.1f * sharp, f => HighPass(f, 900f));
            Mix(b, Grains(r, 0.08f, 15, 700f, 2000f), at, g * 0.7f);
        });

        public static float[] Grass(Random rng, bool run) => Step(rng, run, -24f, 0.4f, (b, r, at, g, sharp) =>
        {
            Thump(b, r, at, g * 0.3f, 70f);
            Filtered(b, Burst(r, 0.002f, 0.05f), at, g * 0.35f, f => LowPass(f, 250f));
            Filtered(b, Burst(r, 0.012f, 0.12f), at, g * 0.5f * sharp, f =>
            {
                BandPass(f, R(r, 2800f, 3600f), 0.5f);
                HighPass(f, 1000f);
            });
            Filtered(b, Grains(r, 0.08f, 14, 2500f, 7000f), at + 0.005f, g * 0.45f, f => HighPass(f, 2000f));
        });

        public static float[] Dirt(Random rng, bool run) => Step(rng, run, -22f, 0.7f, (b, r, at, g, sharp) =>
        {
            Thump(b, r, at, g * 0.5f, 75f);
            Filtered(b, Burst(r, 0.002f, 0.06f), at, g * 0.8f, f => LowPass(f, R(r, 400f, 500f)));
            Filtered(b, Grains(r, 0.05f, 12, 1500f, 5000f), at, g * 0.45f * sharp, f => HighPass(f, 1200f));
        });

        // Heel, ball of the foot, then the sole scuffing the ground; `scuff` scales that last layer per ground.
        // Running steps are 2 dB louder before the gait volume applies on top.
        static float[] Step(Random rng, bool run, float loudness, float scuff, Contact contact, Action<float[]> after = null)
        {
            var b = Buffer(Length);
            var heel = R(rng, 0.003f, 0.008f);
            var toe = heel + (run ? R(rng, 0.025f, 0.04f) : R(rng, 0.075f, 0.11f));
            var sharp = run ? 1.4f : 1f;
            contact(b, rng, heel, run ? 1f : R(rng, 0.7f, 0.85f), sharp);
            contact(b, rng, toe, run ? R(rng, 0.5f, 0.7f) : R(rng, 0.45f, 0.65f), sharp);
            var scrape = Burst(rng, 0.008f, run ? 0.1f : 0.07f);
            HighPass(scrape, R(rng, 2000f, 3000f));
            Mix(b, scrape, toe, scuff * (run ? 0.25f : 0.12f));
            after?.Invoke(b);
            HighPass(b, 40f);
            NormalizeLoudness(b, loudness + (run ? 2f : 0f));
            return Trim(b);
        }

        // Low body thump with a little pitch drop, plus the matching low noise.
        public static void Thump(float[] b, Random rng, float at, float gain, float hz)
        {
            Ring(b, at, hz * R(rng, 0.9f, 1.1f), R(rng, 0.05f, 0.07f), gain, 0.8f);
            Filtered(b, Burst(rng, 0.001f, 0.04f), at, gain * 0.5f, f => LowPass(f, 300f));
        }

        // Stick-slip of a board (slow, rough pulses: a croak) or a hinge (fast pulses that ring into a squeal):
        // jittered pulses through a few narrow resonances, the rate wandering as the pressure changes.
        public static void Creak(float[] b, Random rng, float at, float seconds, float gain, bool hinge = false)
        {
            var creak = Buffer(seconds);
            var drift = Drift(rng, hinge ? 4f : 12f, seconds);
            var rate = hinge ? R(rng, 160f, 340f) : R(rng, 40f, 90f);
            Pulses(creak, rng, t => rate * (1f + (hinge ? 0.3f : 0.35f) * drift(t)), hinge ? 0.06f : 0.25f);
            var body = hinge ? R(rng, 500f, 800f) : R(rng, 600f, 900f);
            var sharpness = hinge ? 3f : 1f;
            var resonance = Buffer(seconds);
            foreach (var (ratio, q, g) in new[] { (1f, 6f, 1f), (2.1f, 8f, 0.6f), (3.3f, 10f, 0.35f) })
            {
                var copy = (float[])creak.Clone();
                BandPass(copy, body * ratio, q * sharpness);
                Mix(resonance, copy, 0f, g);
            }
            Shape(resonance, t => (float)Math.Sin(Math.PI * Math.Min(1f, t / seconds)));
            Normalize(resonance, 1f);
            Mix(b, resonance, at, gain);
        }

        public static void Filtered(float[] into, float[] layer, float at, float gain, Action<float[]> filter)
        {
            filter(layer);
            Mix(into, layer, at, gain);
        }

        public static float R(Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);
    }
}
