using System;
using static DeadCounty.EditorTools.VoiceSynth;

namespace DeadCounty.EditorTools
{
    // Recipes of the zombie voices, one variant per call. They sit low (70 to 120 Hz, formants of a long tract),
    // rough (period doubling, flapping false folds), wide-band and wet; each has its own pitch contour so the
    // player tells an alert from a chase or an attack without seeing the zombie. System-only, like Synth.
    public static class ZombieVoiceRecipes
    {
        const float AirHz = 2200f;
        const float Air = 0.15f;

        public static float[] Moan(Random rng)
        {
            var seconds = Range(rng, 1.4f, 1.9f);
            var f0 = Range(rng, 72f, 100f);
            var swell = Range(rng, 0.08f, 0.2f);
            var fall = Range(rng, 0.3f, 0.45f);
            var drift = Wander(rng, seconds, 0.18f);
            var doubling = Range(rng, 0.2f, 0.5f);
            var breath = Range(rng, 0.15f, 0.3f);
            var (a, b, c) = Pick(rng, (Oo, Uh, Aw), (Mm, Ah, Uh), (Uh, Aw, Oo), (Oo, Ah, Uh));
            Func<float, float> envelope = t => Smooth(0f, 0.18f, t / seconds) * (1f - Smooth(0.72f, 1f, t / seconds));
            var p = new Phonation
            {
                Pitch = t => f0 * (1f + swell * Smooth(0f, 0.3f, t / seconds) - (swell + fall) * Smooth(0.6f, 1f, t / seconds)) * (1f + 0.04f * drift(t)),
                Loudness = envelope,
                Breath = t => breath * (0.3f + envelope(t)),
                Doubling = t => doubling * (0.6f + 0.4f * Smooth(0.5f, 1f, t / seconds)),
                Jitter = Range(rng, 0.02f, 0.035f),
                Shimmer = Range(rng, 0.1f, 0.2f),
                OpenQuotient = Range(rng, 0.55f, 0.7f),
            };
            var wet = rng.NextDouble() < 0.6 ? Range(rng, 15f, 40f) : 0f;
            var rough = Range(rng, 0.25f, 0.45f);
            return Voice(rng, seconds, p, Path(a, b, c, seconds), Range(rng, 0.86f, 0.93f), Range(rng, 1.6f, 2.2f),
                _ => rough, Range(rng, 26f, 40f), Range(rng, 1.8f, 2.6f), wet, envelope);
        }

        // Inhale, a short catch, then a rattling exhale through a slack throat.
        public static float[] Breath(Random rng)
        {
            var inhale = Range(rng, 0.32f, 0.45f);
            var gap = Range(rng, 0.06f, 0.14f);
            var exhale = Range(rng, 0.5f, 0.7f);
            var seconds = inhale + gap + exhale + 0.05f;
            var start = inhale + gap;
            Func<float, float> inEnvelope = t => Smooth(0f, inhale * 0.7f, t) * (1f - Smooth(inhale * 0.75f, inhale, t));
            Func<float, float> outEnvelope = t => Smooth(start, start + 0.08f, t) * (1f - Smooth(start + exhale * 0.45f, start + exhale, t));
            var rattle = Range(rng, 0.3f, 0.5f);
            var creak = Range(rng, 38f, 58f);
            var drift = Wander(rng, seconds, 0.1f);
            var p = new Phonation
            {
                Pitch = t => creak * (1f + 0.15f * drift(t)),
                Loudness = t => 0.15f * inEnvelope(t) + rattle * outEnvelope(t),
                Breath = t => 1.8f * inEnvelope(t) + 1.2f * outEnvelope(t),
                Doubling = t => 0.5f,
                Jitter = 0.08f,
                Shimmer = 0.3f,
                OpenQuotient = 0.5f,
            };
            Func<float, Vowel> vowel = t => t < start ? Vowel.Lerp(Eh, Ah, Smooth(inhale, start, t)) : Vowel.Lerp(Ah, Uh, Smooth(start, seconds, t));
            Func<float, float> envelope = t => 0.75f * inEnvelope(t) + outEnvelope(t);
            return Voice(rng, seconds, p, vowel, Range(rng, 0.88f, 0.95f), 1.8f, t => 0.2f, Range(rng, 20f, 30f), 1.5f,
                Range(rng, 20f, 50f), envelope);
        }

        // "Hnh?": a caught breath then a short grunt rising like a question.
        public static float[] Alert(Random rng)
        {
            var seconds = Range(rng, 0.45f, 0.7f);
            var gasp = Range(rng, 0.07f, 0.11f);
            var f0 = Range(rng, 95f, 120f);
            var rise = Range(rng, 0.3f, 0.5f);
            Func<float, float> voiced = t => Smooth(gasp, gasp + 0.03f, t) * (1f - Smooth(seconds * 0.7f, seconds, t));
            Func<float, float> inhale = t => Smooth(0f, gasp * 0.6f, t) * (1f - Smooth(gasp * 0.6f, gasp + 0.02f, t));
            var p = new Phonation
            {
                Pitch = t => f0 * (1f + rise * Smooth(gasp, seconds * 0.85f, t)),
                Loudness = voiced,
                Breath = t => 1.6f * inhale(t) + 0.2f * voiced(t),
                Doubling = t => 0.4f,
                Jitter = 0.025f,
                Shimmer = 0.15f,
                OpenQuotient = 0.6f,
            };
            var (from, to) = rng.NextDouble() < 0.5 ? (Uh, Ah) : (Eh, Ae);
            Func<float, Vowel> vowel = t => t < gasp ? Vowel.Lerp(Eh, from, Smooth(gasp - 0.03f, gasp, t)) : Vowel.Lerp(from, to, Smooth(gasp, seconds, t));
            return Voice(rng, seconds, p, vowel, Range(rng, 0.88f, 0.94f), 1.6f, t => 0.35f, Range(rng, 30f, 42f), 2.5f, 0f,
                t => 0.7f * inhale(t) + voiced(t));
        }

        // Two to four wet sniffs at the air, often closed by a low "hm".
        public static float[] Sniff(Random rng)
        {
            var count = rng.Next(2, 5);
            var lengths = new float[count];
            var gaps = new float[count];
            var t = Range(rng, 0.01f, 0.03f);
            for (var i = 0; i < count; i++)
            {
                lengths[i] = Range(rng, 0.07f, 0.12f) * (i == count - 1 ? 1.4f : 1f);
                gaps[i] = Range(rng, 0.05f, 0.1f);
            }
            var gruntLength = rng.NextDouble() < 0.65 ? Range(rng, 0.3f, 0.45f) : 0f;
            var sniffing = t;
            for (var i = 0; i < count; i++) sniffing += lengths[i] + gaps[i];
            var b = Synth.Buffer(sniffing + gruntLength + 0.02f);
            for (var i = 0; i < count; i++)
            {
                var layer = Synth.Buffer(lengths[i]);
                Synth.White(layer, rng);
                var high = (float[])layer.Clone();
                Synth.BandPass(layer, Range(rng, 2600f, 3800f), 1.4f);
                Synth.HighPass(high, 1500f);
                for (var k = 0; k < layer.Length; k++) layer[k] += 0.35f * high[k];
                Bubbles(layer, rng, _ => 60f, 0.15f, 900f, 2500f);
                Synth.Envelope(layer, 0.015f, lengths[i] * 0.4f, lengths[i] * 0.5f);
                Synth.Mix(b, layer, t, Range(rng, 0.6f, 1f));
                t += lengths[i] + gaps[i];
            }
            if (gruntLength > 0f)
            {
                var f0 = Range(rng, 80f, 100f);
                Func<float, float> envelope = u => Smooth(0f, 0.04f, u) * (1f - Smooth(gruntLength * 0.5f, gruntLength, u));
                var p = new Phonation { Pitch = u => f0 * (1f - 0.25f * u / gruntLength), Loudness = envelope, Breath = u => 0.3f * envelope(u), Doubling = _ => 0.4f, Jitter = 0.035f, Shimmer = 0.15f, OpenQuotient = 0.6f };
                var grunt = Voice(rng, gruntLength, p, u => Vowel.Lerp(Mm, Uh, Smooth(0f, gruntLength, u)), 0.9f, 1.8f, _ => 0.3f, 32f, 2f, 0f, envelope);
                Synth.Mix(b, grunt, t - gaps[count - 1] * 0.5f, Range(rng, 0.5f, 0.7f));
            }
            Synth.HighPass(b, 80f);
            Declick(b);
            Synth.Normalize(b);
            return b;
        }

        // Chase growl: one to three rough "hrr" pulses an octave below their pitch, teeth bared.
        public static float[] Growl(Random rng)
        {
            var count = rng.Next(1, 4);
            var pulses = new (float start, float length, float level)[count];
            var t = 0.01f;
            for (var i = 0; i < count; i++)
            {
                var length = count == 1 ? Range(rng, 0.6f, 0.9f) : Range(rng, 0.22f, 0.4f);
                pulses[i] = (t, length, Range(rng, 0.7f, 1f));
                t += length + Range(rng, 0.04f, 0.1f);
            }
            var seconds = t + 0.05f;
            Func<float, float> envelope = u =>
            {
                var g = 0f;
                foreach (var (start, length, level) in pulses)
                    g = Math.Max(g, level * Smooth(start, start + 0.06f, u) * (1f - Smooth(start + length * 0.6f, start + length, u)));
                return g;
            };
            var f0 = Range(rng, 65f, 90f);
            var drift = Wander(rng, seconds, 0.1f);
            var doubling = Range(rng, 0.5f, 0.8f);
            var rough = Range(rng, 0.45f, 0.6f);
            var p = new Phonation
            {
                Pitch = u => f0 * (1f + 0.08f * drift(u) + 0.15f * envelope(u)),
                Loudness = envelope,
                Breath = u => 0.5f * envelope(u),
                Doubling = _ => doubling,
                Jitter = 0.04f,
                Shimmer = 0.2f,
                OpenQuotient = 0.65f,
            };
            var snarl = rng.NextDouble() < 0.5 ? Ae : Ah;
            return Voice(rng, seconds, p, u => Vowel.Lerp(Uh, snarl, envelope(u)), Range(rng, 0.86f, 0.92f), 1.7f,
                _ => rough, Range(rng, 35f, 50f), Range(rng, 3f, 4f), Range(rng, 0f, 30f), envelope);
        }

        // The call that sets off the zombies around: a rising, opening roar, the loudest voice they have.
        public static float[] Call(Random rng)
        {
            var seconds = Range(rng, 1.2f, 1.6f);
            var f0 = Range(rng, 95f, 115f);
            var peak = Range(rng, 170f, 215f);
            var crest = Range(rng, 0.35f, 0.5f);
            var drift = Wander(rng, seconds, 0.12f);
            var doubling = Range(rng, 0.25f, 0.45f);
            var rough = Range(rng, 0.35f, 0.5f);
            Func<float, float> envelope = t => Smooth(0f, 0.12f, t) * (1f - 0.3f * Smooth(crest, 0.8f * seconds, t)) * (1f - Smooth(0.7f * seconds, seconds, t));
            var p = new Phonation
            {
                Pitch = t => (f0 + (peak - f0) * Smooth(0f, crest * seconds, t) - (peak - 0.9f * f0) * Smooth(0.6f * seconds, seconds, t)) * (1f + 0.03f * drift(t)),
                Loudness = envelope,
                Breath = t => 0.2f * envelope(t),
                Doubling = _ => doubling,
                Jitter = 0.02f,
                Shimmer = 0.15f,
                OpenQuotient = 0.6f,
            };
            Func<float, Vowel> vowel = t =>
            {
                var u = t / seconds;
                if (u < 0.35f) return Vowel.Lerp(Aw, Ah, Smooth(0f, 0.35f, u));
                return u < 0.6f ? Vowel.Lerp(Ah, Ae, Smooth(0.35f, 0.6f, u)) : Vowel.Lerp(Ae, Uh, Smooth(0.6f, 1f, u));
            };
            return Loud(Voice(rng, seconds, p, vowel, Range(rng, 0.9f, 0.95f), 1.4f, _ => rough,
                Range(rng, 30f, 45f), 3f, 0f, envelope));
        }

        // The 1 s attack telegraph: a sharp intake, then a snarl that climbs in pitch, brightness and loudness to
        // its peak at 1 s, when the lunge starts. It starts loud already: the player must hear it from its onset.
        public static float[] Windup(Random rng)
        {
            const float seconds = 1.1f;
            const float lunge = 1f;
            var gasp = Range(rng, 0.13f, 0.17f);
            var f0 = Range(rng, 80f, 95f);
            var top = Range(rng, 1.65f, 1.85f);
            Func<float, float> build = t => Math.Clamp((t - gasp) / (lunge - gasp), 0f, 1f);
            Func<float, float> voiced = t => Smooth(gasp, gasp + 0.05f, t) * (1f - Smooth(lunge + 0.02f, seconds, t));
            Func<float, float> inhale = t => Smooth(0f, gasp * 0.6f, t) * (1f - Smooth(gasp * 0.6f, gasp + 0.03f, t));
            var p = new Phonation
            {
                Pitch = t => f0 * (1f + (top - 1f) * (float)Math.Pow(build(t), 1.5)),
                Loudness = voiced,
                Breath = t => 1.6f * inhale(t) + (0.15f + 0.2f * build(t)) * voiced(t),
                Doubling = t => 0.5f - 0.25f * build(t),
                Jitter = 0.02f,
                Shimmer = 0.15f,
                OpenQuotient = 0.6f,
            };
            Func<float, Vowel> vowel = t =>
            {
                if (t < gasp) return Vowel.Lerp(Eh, Uh, Smooth(gasp - 0.04f, gasp, t));
                var u = build(t);
                return u < 0.75f ? Vowel.Lerp(Uh, Ae, Smooth(0f, 0.75f, u)) : Vowel.Lerp(Ae, Ah, Smooth(0.75f, 1f, u));
            };
            var b = Voice(rng, seconds, p, vowel, Range(rng, 0.9f, 0.94f), 1.5f, t => 0.5f - 0.2f * build(t),
                Range(rng, 28f, 34f), 3f, 0f, t => 0.9f * inhale(t) + voiced(t) * (0.8f + 0.2f * build(t)));
            return Loud(b);
        }

        // Lost the target: an exhaled huff, then a groan falling into creak.
        public static float[] Frustrated(Random rng)
        {
            var seconds = Range(rng, 1.3f, 1.7f);
            var huff = Range(rng, 0.08f, 0.13f);
            var f0 = Range(rng, 108f, 128f);
            var floor = Range(rng, 48f, 58f);
            Func<float, float> voiced = t => Smooth(huff * 0.6f, huff + 0.06f, t) * (1f - 0.55f * Smooth(huff, seconds * 0.8f, t)) * (1f - Smooth(seconds * 0.85f, seconds, t));
            Func<float, float> puff = t => Smooth(0f, 0.02f, t) * (1f - Smooth(0.02f, huff + 0.04f, t));
            var p = new Phonation
            {
                Pitch = t => floor + (f0 - floor) * (1f - Smooth(huff, seconds * 0.9f, t)),
                Loudness = voiced,
                Breath = t => 1.4f * puff(t) + (0.25f + 0.3f * t / seconds) * voiced(t),
                Doubling = t => 0.2f + 0.5f * t / seconds,
                Jitter = 0.03f,
                Shimmer = 0.18f,
                OpenQuotient = 0.62f,
            };
            return Voice(rng, seconds, p, Path(Ah, Uh, Oo, seconds), Range(rng, 0.87f, 0.92f), 1.9f, _ => 0.3f,
                Range(rng, 26f, 36f), 2.5f, Range(rng, 20f, 40f), t => 0.8f * puff(t) + voiced(t));
        }

        // Source, roughness, rasp, tract, saliva, then the final envelope: rasp before the tract like the false folds
        // that make a growl, envelope last so the rasp does not flatten the dynamics. The cascade alone leaves little
        // above 3 kHz, so some of the rasped source bypasses it high-passed: the hiss of a torn throat.
        static float[] Voice(Random rng, float seconds, in Phonation p, Func<float, Vowel> vowel, float tract, float width,
            Func<float, float> roughDepth, float roughHz, float drive, float wetRate, Func<float, float> envelope)
        {
            var b = Synth.Buffer(seconds);
            Glottis(b, rng, p);
            Roughen(b, rng, _ => roughHz, roughDepth);
            Rasp(b, drive);
            var air = (float[])b.Clone();
            Synth.HighPass(air, AirHz);
            Synth.HighPass(air, AirHz);
            Synth.Normalize(air, Air);
            Formants(b, vowel, tract, width);
            Synth.Normalize(b, 1f);
            Synth.Mix(b, air);
            if (wetRate > 0f) Bubbles(b, rng, t => wetRate * envelope(t), 0.12f, 350f, 1400f);
            Synth.HighPass(b, 75f);
            Synth.LowPass(b, 7000f);
            Synth.Shape(b, envelope);
            Declick(b);
            Synth.Normalize(b);
            return b;
        }

        // Squashes the peaks so the whole sound sits louder: for the cues the player must not miss.
        static float[] Loud(float[] b)
        {
            Rasp(b, 1.8f);
            Synth.HighPass(b, 60f);
            Synth.Normalize(b);
            return b;
        }

        static Func<float, Vowel> Path(Vowel a, Vowel b, Vowel c, float seconds) => t =>
        {
            var u = t / seconds;
            return u < 0.5f ? Vowel.Lerp(a, b, Smooth(0.1f, 0.5f, u)) : Vowel.Lerp(b, c, Smooth(0.5f, 0.9f, u));
        };

        static T Pick<T>(Random rng, params T[] options) => options[rng.Next(options.Length)];
    }
}
