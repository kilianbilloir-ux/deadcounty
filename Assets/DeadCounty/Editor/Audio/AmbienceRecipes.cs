using System;
using static DeadCounty.EditorTools.AmbienceDsp;

namespace DeadCounty.EditorTools
{
    // The county's soundscape: wind and night insect beds, a room tone, and the calls of the animals still around.
    // Beds are rendered one crossfade longer than their loop with every modulation periodic over the loop, then
    // wrapped (AmbienceDsp.Loop). Calls carry their own distance: no highs, a little open-air tail.
    static class AmbienceRecipes
    {
        public const float WindLoop = 10f;
        public const float NightLoop = 10f;
        public const float RoomLoop = 3f;
        const float Seam = 1.5f;

        // Moving air in an open landscape (Farnell, Designing Sound, "Wind"): a low rumble and a band-passed
        // whoosh that follow a slow gust curve, a faint whistle only in the strongest gusts, leaves above 2.5 kHz.
        public static float[] Wind(Random rng)
        {
            var b = Synth.Buffer(WindLoop + Seam);
            var gust = PeriodicWander(rng, WindLoop, 1, 3, 4);
            var flutter = PeriodicWander(rng, WindLoop, 12, 38, 6);
            float Speed(float t) => Math.Clamp(0.55f + 0.75f * gust(t), 0.2f, 1f);

            var rumble = Synth.Buffer(Synth.Seconds(b));
            Synth.Brown(rumble, rng);
            Sweep(rumble, Mode.Low, t => 160f + 380f * Speed(t), 0.7f);
            Synth.HighPass(rumble, 30f);
            Synth.Shape(rumble, t => 0.35f + 0.65f * Speed(t));
            Synth.Mix(b, rumble, 0f, 0.9f);

            var whoosh = Synth.Buffer(Synth.Seconds(b));
            Synth.White(whoosh, rng);
            Sweep(whoosh, Mode.Band, t => 320f + 700f * Speed(t) + 90f * flutter(t), 1.3f);
            Synth.Shape(whoosh, t => (float)Math.Pow(Speed(t), 1.6f));
            Synth.Mix(b, whoosh, 0f, 0.55f);

            var whistle = Synth.Buffer(Synth.Seconds(b));
            Synth.White(whistle, rng);
            Sweep(whistle, Mode.Band, t => 520f + 420f * Speed(t) + 40f * flutter(t), 22f);
            Synth.Shape(whistle, t => Smooth(0.6f, 0.95f, Speed(t)));
            Synth.Mix(b, whistle, 0f, 0.5f);

            var leaves = Synth.Buffer(Synth.Seconds(b));
            Synth.White(leaves, rng);
            Synth.HighPass(leaves, 2500f);
            Sweep(leaves, Mode.Band, t => 3800f + 1600f * flutter(t), 0.8f);
            Synth.Shape(leaves, t => Speed(t) * Speed(t) * (0.55f + 0.45f * flutter(t)));
            Synth.Mix(b, leaves, 0f, 0.3f);

            b = Loop(b, Seam);
            Synth.Normalize(b);
            return b;
        }

        // Field crickets (carrier ~4.8 kHz, 3-4 pulses of ~18 ms per chirp, 2-3 chirps a second), a far chorus of
        // them, a tree cricket's steady trill and a high hiss. Every chirp grid divides the loop, so the insects
        // wrap exactly and only the hiss needs a crossfade.
        public static float[] Night(Random rng)
        {
            var b = Synth.Buffer(NightLoop);
            var near = Synth.Buffer(NightLoop);
            for (var c = 0; c < 4; c++) Cricket(near, rng, 0.35f + 0.65f * (float)rng.NextDouble());
            Synth.LowPass(near, 7500f);
            Synth.Mix(b, near);

            var far = Synth.Buffer(NightLoop);
            for (var c = 0; c < 9; c++) Cricket(far, rng, 0.12f + 0.15f * (float)rng.NextDouble());
            Synth.LowPass(far, 4200f);
            Synth.Mix(b, far);

            var trill = Synth.Buffer(NightLoop);
            var spacing = NightLoop / (float)Math.Round(NightLoop / 0.024f);
            var swell = PeriodicWander(rng, NightLoop, 1, 3, 2);
            for (var t = 0f; t < NightLoop; t += spacing)
                Grain(trill, t, 0.014f, _ => 2900f, 0.09f * Smooth(-0.3f, 0.6f, swell(t)));
            Synth.Mix(b, trill);

            var hiss = Synth.Buffer(NightLoop + Seam);
            Synth.White(hiss, rng);
            Synth.BandPass(hiss, 7200f, 2f);
            Synth.Shape(hiss, t => 0.5f + 0.5f * swell(t + 3f));
            Synth.Mix(b, Loop(hiss, Seam), 0f, 0.05f);
            Synth.Normalize(b);
            return b;
        }

        // One cricket singing in bouts: its chirp period divides the night loop, so the pattern wraps in phase.
        static void Cricket(float[] b, Random rng, float gain)
        {
            var chirps = (int)Math.Round(NightLoop / (0.36f + 0.24f * (float)rng.NextDouble()));
            var period = NightLoop / chirps;
            var carrier = 4300f + 900f * (float)rng.NextDouble();
            var pulses = rng.Next(3, 5);
            var pulseGap = 0.03f + 0.012f * (float)rng.NextDouble();
            var offset = (float)rng.NextDouble() * period;
            var bout = PeriodicWander(rng, NightLoop, 1, 3, 2);
            for (var t = offset - period; t < NightLoop; t += period)
            {
                var level = gain * Smooth(-0.35f, 0.25f, bout(t));
                if (level <= 0.01f) continue;
                for (var p = 0; p < pulses; p++)
                    Grain(b, t + p * pulseGap, 0.018f, u => carrier * (1f - 2.5f * u), level * (p == 0 ? 0.7f : 1f), 0.12f);
            }
        }

        // Air in a closed room: low and still, felt more than heard.
        public static float[] RoomTone(Random rng)
        {
            var b = Synth.Buffer(RoomLoop + Seam);
            Synth.Brown(b, rng);
            Synth.LowPass(b, 220f);
            Synth.HighPass(b, 40f);
            var air = Synth.Buffer(Synth.Seconds(b));
            Synth.White(air, rng);
            Synth.BandPass(air, 650f, 0.7f);
            Synth.LowPass(air, 1200f);
            Synth.Mix(b, air, 0f, 0.03f);
            var drift = PeriodicWander(rng, RoomLoop, 1, 3, 2);
            Synth.Shape(b, t => 0.85f + 0.15f * drift(t));
            b = Loop(b, Seam);
            Synth.Normalize(b);
            return b;
        }

        // A song from a tree 15-35 m away; species picks one of the six phrase shapes.
        public static float[] Bird(Random rng, int species)
        {
            var b = Synth.Buffer(2.2f);
            float R(float min, float max) => min + (max - min) * (float)rng.NextDouble();
            var t = 0.03f;
            switch (species % 6)
            {
                case 0:
                    for (var n = rng.Next(2, 4); n > 0; n--, t += R(0.32f, 0.42f))
                    {
                        var high = R(5000f, 5600f);
                        Grain(b, t, 0.2f, u => high * (float)Math.Pow(0.55f, u / 0.2f), 0.8f, 0.05f);
                    }
                    break;
                case 1:
                    var top = R(4600f, 5400f);
                    for (var n = 0; n < 14; n++, t += 0.058f)
                        Grain(b, t, 0.036f, u => top * (0.74f + 7f * u) * (1f - 0.012f * n), 0.5f + 0.5f * (float)Math.Sin(n * 0.25f));
                    break;
                case 2:
                    var fee = R(3800f, 4100f);
                    Grain(b, t, 0.3f, u => fee * (1f - 0.02f * (float)Math.Sin(u * 10f)), 0.9f, 0.04f);
                    Grain(b, t + 0.38f, 0.38f, u => fee * 0.86f * (1f + 0.012f * (float)Math.Sin(u * 160f)), 0.75f, 0.04f);
                    break;
                case 3:
                    for (var n = 0; n < 3; n++, t += 0.11f) Grain(b, t, 0.05f, u => 2900f - 6000f * u, 0.7f);
                    var centre = R(3800f, 4300f);
                    Grain(b, t, 0.42f, u => centre + 450f * (float)Math.Sin(2 * Math.PI * 95f * u), 0.55f);
                    t += 0.5f;
                    Grain(b, t, 0.12f, u => 3200f + 9000f * u, 0.6f);
                    Grain(b, t + 0.16f, 0.16f, u => 4400f - 8000f * u, 0.7f);
                    break;
                case 4:
                    for (var n = rng.Next(4, 6); n > 0; n--, t += R(0.2f, 0.26f))
                    {
                        var low = R(2300f, 2700f);
                        var rise = R(700f, 1100f);
                        var length = R(0.12f, 0.17f);
                        Grain(b, t, length, u => low + rise * (float)Math.Sin(Math.PI * u / length), 0.8f, 0.08f);
                    }
                    break;
                default:
                    for (var n = rng.Next(2, 4); n > 0; n--, t += R(0.14f, 0.2f))
                        Grain(b, t, 0.03f, u => 6200f - 90000f * u, 0.9f);
                    break;
            }
            return Outdoors(b, 6500f, 0.9f, 0.18f);
        }

        // A crow on a roof or a wire: harsh, nasal caws (sawtooth through formants, a rough edge).
        public static float[] Crow(Random rng)
        {
            var b = Synth.Buffer(2.4f);
            var caws = rng.Next(2, 4);
            var t = 0.05f;
            for (var c = 0; c < caws; c++)
            {
                var length = 0.26f + 0.08f * (float)rng.NextDouble();
                var pitch = 540f + 60f * (float)rng.NextDouble();
                var caw = Synth.Buffer(length);
                Synth.Tone(caw, u => pitch * (1.05f - 0.22f * u / length) * (1f + 0.04f * (float)Math.Sin(2 * Math.PI * 70f * u)), Synth.Wave.Saw);
                Synth.White(caw, rng, 0.25f);
                var voiced = Formants(caw, 0.55f, (850f, 3f, 0.6f), (1450f, 4f, 1f), (2300f, 5f, 0.5f));
                Synth.Envelope(voiced, 0.015f, length * 0.4f, length * 0.6f);
                Synth.Saturate(voiced, 2.5f);
                Synth.Mix(b, voiced, t, c == caws - 1 ? 0.8f : 1f);
                t += length + 0.16f + 0.1f * (float)rng.NextDouble();
            }
            Synth.HighPass(b, 150f);
            return Outdoors(b, 3500f, 1.3f, 0.3f);
        }

        // Great horned owl at the edge of the woods: hoo, hoo-hoo, hoooo, hoo. Variant 1 is a quavering tawny call.
        public static float[] Owl(Random rng, int kind)
        {
            var b = Synth.Buffer(2.4f);
            var f0 = kind % 2 == 0 ? 330f + 30f * (float)rng.NextDouble() : 560f + 40f * (float)rng.NextDouble();
            (float at, float length)[] hoots = kind % 2 == 0
                ? new[] { (0.05f, 0.34f), (0.52f, 0.13f), (0.7f, 0.13f), (1.0f, 0.46f), (1.6f, 0.3f) }
                : new[] { (0.05f, 0.55f), (0.95f, 0.12f), (1.1f, 0.62f) };
            for (var h = 0; h < hoots.Length; h++)
            {
                var (at, length) = hoots[h];
                var quaver = kind % 2 == 1 && h == hoots.Length - 1;
                var hoot = Synth.Buffer(length);
                Synth.Tone(hoot, u => f0 * (1f + 0.04f * (float)Math.Sin(Math.PI * u / length)) * (quaver ? 1f + 0.03f * (float)Math.Sin(2 * Math.PI * 22f * u) : 1f), Synth.Wave.Sine);
                Synth.Tone(hoot, u => 2f * f0, Synth.Wave.Sine, 0.12f);
                var breath = Synth.Buffer(length);
                Synth.White(breath, rng);
                Synth.BandPass(breath, f0 * 2f, 4f);
                Synth.LowPass(breath, f0 * 3f);
                Synth.Mix(hoot, breath, 0f, 0.12f);
                Synth.Shape(hoot, u => Smooth(0f, 0.07f, u) * Smooth(length, length - 0.14f, u));
                Synth.Mix(b, hoot, at, 0.8f);
            }
            return Outdoors(b, 2400f, 1.6f, 0.38f);
        }

        // A dog two farms away: rough barks through a muzzle-sized formant set, thrown back by the valley.
        public static float[] Dog(Random rng, int kind)
        {
            var b = Synth.Buffer(2.2f);
            var barks = kind % 2 == 0 ? 2 : 3;
            var gap = kind % 2 == 0 ? 0.42f : 0.28f;
            for (var k = 0; k < barks; k++)
            {
                var bark = Synth.Buffer(0.22f);
                var f0 = 430f + 60f * (float)rng.NextDouble();
                Synth.Tone(bark, u => f0 * (1f - 1.6f * Math.Min(u, 0.2f)), Synth.Wave.Saw);
                var burst = Synth.Buffer(0.03f);
                Synth.White(burst, rng);
                Synth.Mix(bark, burst, 0f, 0.8f);
                var voiced = Formants(bark, 0.5f, (620f, 2f, 1f), (1150f, 3f, 0.8f), (2400f, 4f, 0.35f));
                Synth.Envelope(voiced, 0.005f, 0.04f, 0.16f);
                Synth.Saturate(voiced, 3f);
                Synth.Mix(b, voiced, 0.04f + k * gap + 0.04f * (float)rng.NextDouble(), k == 0 ? 1f : 0.85f);
            }
            Synth.HighPass(b, 220f);
            Echo(b, 0.31f, 0.3f, 900f, 2);
            return Outdoors(b, 1500f, 1.8f, 0.45f);
        }

        // Parallel band-passes: a vowel shaped out of a buzzy source.
        public static float[] Formants(float[] source, float gain, params (float hz, float q, float level)[] bands)
        {
            var output = new float[source.Length];
            foreach (var (hz, q, level) in bands)
            {
                var band = (float[])source.Clone();
                Synth.BandPass(band, hz, q);
                Synth.Mix(output, band, 0f, level * gain);
            }
            return output;
        }

        // Distance baked in: the air eats the highs, the landscape adds a diffuse tail. The buffer must leave room
        // for the tail; what stays inaudible at its end is trimmed.
        public static float[] Outdoors(float[] b, float cutoffHz, float tailSeconds, float wet)
        {
            Synth.LowPass(b, cutoffHz);
            Reverb(b, tailSeconds, cutoffHz * 0.5f, wet);
            Synth.Normalize(b);
            return Trim(b);
        }
    }
}
