using System;
using static DeadCounty.EditorTools.AmbienceDsp;

namespace DeadCounty.EditorTools
{
    // Rare signs of the outbreak somewhere in the county: a scream, a car alarm, gunfire. Muffled, band-limited and
    // drowned in echoes so they can never pass for a threat next to the player.
    static class DistantRecipes
    {
        // Variant 0 a woman's scream that breaks, 1 a man's cry in two bursts: a sawtooth voice with vibrato and
        // jitter through an "ah" formant set.
        public static float[] Scream(Random rng, int kind)
        {
            var b = Synth.Buffer(2.8f);
            var jitter = PeriodicWander(rng, 1f, 9, 23, 4);
            if (kind % 2 == 0)
                Synth.Mix(b, Voice(rng, 1.3f, u => 700f + 380f * Smooth(0f, 0.25f, u) - 330f * Smooth(0.45f, 1.3f, u), jitter, 0.85f), 0.08f);
            else
            {
                Synth.Mix(b, Voice(rng, 0.55f, u => 310f + 120f * Smooth(0f, 0.15f, u) - 60f * Smooth(0.2f, 0.55f, u), jitter, 0.55f), 0.08f);
                Synth.Mix(b, Voice(rng, 0.8f, u => 360f + 140f * Smooth(0f, 0.2f, u) - 140f * Smooth(0.3f, 0.8f, u), jitter, 0.55f), 0.85f);
            }
            Synth.HighPass(b, 350f);
            Echo(b, 0.47f, 0.3f, 900f, 2);
            return AmbienceRecipes.Outdoors(b, 1300f, 2.2f, 0.55f);
        }

        static float[] Voice(Random rng, float seconds, Func<float, float> pitch, Func<float, float> jitter, float formantScale)
        {
            var voice = Synth.Buffer(seconds);
            Synth.Tone(voice, u => pitch(u) * (1f + 0.035f * (float)Math.Sin(2 * Math.PI * 6.5f * u) + 0.02f * jitter(u)), Synth.Wave.Saw);
            Synth.White(voice, rng, 0.15f);
            var vowel = AmbienceRecipes.Formants(voice, 0.6f, (900f * formantScale + 400f, 4f, 1f), (1400f * formantScale + 500f, 5f, 0.7f), (2800f, 6f, 0.3f));
            Synth.Shape(vowel, u => Smooth(0f, 0.06f, u) * Smooth(seconds, seconds - 0.25f, u) * (0.8f + 0.2f * u / seconds));
            Synth.Saturate(vowel, 2f);
            return vowel;
        }

        // A car alarm a few streets away, cut off after a couple of seconds: variant 0 the two-tone yelp, 1 the
        // rising whoop. Slapback from the facades, then a long tail.
        public static float[] CarAlarm(Random rng, int kind)
        {
            var b = Synth.Buffer(2.9f);
            var length = 1.6f + 0.3f * (float)rng.NextDouble();
            var siren = Synth.Buffer(length);
            if (kind % 2 == 0)
                Synth.Tone(siren, u => (int)(u / 0.13f) % 2 == 0 ? 1450f : 1050f, Synth.Wave.Square);
            else
                Synth.Tone(siren, u => 700f + 950f * (u % 0.38f) / 0.38f, Synth.Wave.Square);
            Synth.LowPass(siren, 2400f);
            Synth.Envelope(siren, 0.01f, length - 0.03f, 0.02f);
            Synth.Mix(b, siren, 0.05f, 0.6f);
            Synth.HighPass(b, 420f);
            Echo(b, 0.23f, 0.38f, 1500f, 3);
            return AmbienceRecipes.Outdoors(b, 1900f, 1.6f, 0.5f);
        }

        // Gunfire far off: the crack is gone with the distance, what is left is a dull thump rolling back from the
        // hills. Variant 1 fires twice.
        public static float[] Gunshot(Random rng, int kind)
        {
            var b = Synth.Buffer(2.5f);
            Shot(b, rng, 0.03f, 1f);
            if (kind % 2 == 1) Shot(b, rng, 0.62f, 0.8f);
            Synth.HighPass(b, 80f);
            Echo(b, 0.58f, 0.32f, 700f, 3);
            return AmbienceRecipes.Outdoors(b, 1400f, 2.5f, 0.6f);
        }

        static void Shot(float[] b, Random rng, float at, float gain)
        {
            var shot = Synth.Buffer(0.4f);
            var crack = Synth.Buffer(0.006f);
            Synth.White(crack, rng);
            Synth.Mix(shot, crack, 0f, 0.9f);
            var body = Synth.Buffer(0.4f);
            Synth.Brown(body, rng);
            Synth.BandPass(body, 380f, 0.8f);
            Synth.Envelope(body, 0.002f, 0.01f, 0.3f);
            Synth.Mix(shot, body, 0f, 2.5f);
            Ping(shot, 0f, 120f, 0.05f, 0.6f, 70f);
            Synth.LowPass(shot, 1600f);
            Synth.Mix(b, shot, at, gain);
        }
    }
}
