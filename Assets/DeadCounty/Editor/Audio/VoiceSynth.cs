using System;

namespace DeadCounty.EditorTools
{
    // Source-filter synthesis of the zombie voices, plus the percussive layers of their bodies. The source is a
    // glottal flow pulse train perturbed pulse by pulse (jitter, shimmer, period doubling, creak at low pitch)
    // with aspiration noise gated by the open glottis; a cascade of formant resonators plays the vocal tract.
    // Rough voices owe their character to subharmonics and irregular periods more than to noise, so those carry
    // most of the zombie timbre. System-only, like Synth.
    public static class VoiceSynth
    {
        const int FormantUpdate = 16;
        const float Fade = 0.005f;

        public readonly struct Vowel
        {
            public readonly float F1, F2, F3, F4, F5;

            public Vowel(float f1, float f2, float f3, float f4 = 3350f, float f5 = 4150f)
            {
                F1 = f1;
                F2 = f2;
                F3 = f3;
                F4 = f4;
                F5 = f5;
            }

            public static Vowel Lerp(in Vowel a, in Vowel b, float t) => new(
                a.F1 + (b.F1 - a.F1) * t, a.F2 + (b.F2 - a.F2) * t, a.F3 + (b.F3 - a.F3) * t,
                a.F4 + (b.F4 - a.F4) * t, a.F5 + (b.F5 - a.F5) * t);
        }

        // Adult male formants (Peterson and Barney); the recipes scale them down for a longer, slacker tract.
        public static readonly Vowel Oo = new(310f, 870f, 2240f);
        public static readonly Vowel Uh = new(640f, 1190f, 2390f);
        public static readonly Vowel Ah = new(730f, 1090f, 2440f);
        public static readonly Vowel Aw = new(570f, 840f, 2410f);
        public static readonly Vowel Ae = new(660f, 1720f, 2410f);
        public static readonly Vowel Eh = new(530f, 1840f, 2480f);
        public static readonly Vowel Mm = new(250f, 1000f, 2200f);

        // Base bandwidths (Hz) of the five formants, widened by the recipes for a damp, damaged tract.
        static readonly float[] Bandwidths = { 80f, 100f, 150f, 230f, 300f };

        public struct Phonation
        {
            // Fundamental (Hz) over time (s).
            public Func<float, float> Pitch;
            // Pulse amplitude over time, 0 for a silent glottis.
            public Func<float, float> Loudness;
            // Aspiration noise level over time, relative to the voiced pulses.
            public Func<float, float> Breath;
            // Period doubling over time, 0 to 1: every other pulse weaker and later, an octave-down growl.
            public Func<float, float> Doubling;
            // Standard deviation of the period and of the pulse amplitude, relative.
            public float Jitter;
            public float Shimmer;
            // Share of the period the glottis is open.
            public float OpenQuotient;
        }

        public static float Gauss(Random rng)
        {
            var u = 1.0 - rng.NextDouble();
            return (float)(Math.Sqrt(-2.0 * Math.Log(u)) * Math.Cos(2.0 * Math.PI * rng.NextDouble()));
        }

        public static float Range(Random rng, float min, float max) => min + (max - min) * (float)rng.NextDouble();

        public static float Smooth(float edge0, float edge1, float x)
        {
            var t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        // A smooth random curve around 0 (about ±1), one knot every `step` seconds, cosine-interpolated: pitch drift.
        public static Func<float, float> Wander(Random rng, float seconds, float step)
        {
            var knots = new float[(int)(seconds / step) + 3];
            for (var i = 0; i < knots.Length; i++) knots[i] = (float)(rng.NextDouble() * 2.0 - 1.0);
            return t =>
            {
                var x = Math.Max(0f, t) / step;
                var i = Math.Min((int)x, knots.Length - 2);
                var f = (1f - (float)Math.Cos(Math.PI * Math.Min(1f, x - i))) * 0.5f;
                return knots[i] + (knots[i + 1] - knots[i]) * f;
            };
        }

        // Adds the glottal source: the flow pulses are differentiated (lip radiation) and normalized to ±1 before the
        // aspiration noise is added, so Breath is a level relative to the voice.
        public static void Glottis(float[] b, Random rng, in Phonation p)
        {
            var flow = new float[b.Length + 1];
            var seconds = b.Length / (float)Synth.Rate;
            var t = 0.0;
            var odd = false;
            while (t < seconds)
            {
                var time = (float)t;
                var f0 = Math.Max(18f, p.Pitch(time));
                // Below about 70 Hz the voice slips into creak: irregular, short, sparse pulses.
                var creak = Math.Clamp((70f - f0) / 40f, 0f, 1f);
                var doubling = p.Doubling?.Invoke(time) ?? 0f;
                var period = 1.0 / f0 * (1.0 + Math.Clamp(Gauss(rng) * p.Jitter * (1f + 2f * creak), -0.4f, 0.4f));
                if (odd) period *= 1.0 + 0.12 * doubling;
                var amplitude = Math.Max(0f, (p.Loudness?.Invoke(time) ?? 1f) * (1f + Gauss(rng) * p.Shimmer * (1f + creak)));
                if (odd) amplitude *= 1f - 0.65f * doubling;
                var open = period * p.OpenQuotient * (1f - 0.5f * creak);
                var rise = open * 0.7;
                var fall = open - rise;
                var first = (int)Math.Ceiling(t * Synth.Rate);
                var last = Math.Min(b.Length, (int)((t + open) * Synth.Rate));
                for (var i = first; i < last; i++)
                {
                    var local = i / (double)Synth.Rate - t;
                    var g = local < rise ? 0.5 * (1.0 - Math.Cos(Math.PI * local / rise)) : Math.Cos(0.5 * Math.PI * (local - rise) / fall);
                    flow[i] = (float)(amplitude * g);
                }
                t += period;
                odd = !odd;
            }

            var voiced = new float[b.Length];
            var peak = 0f;
            var flowPeak = 0f;
            for (var i = 0; i < b.Length; i++)
            {
                voiced[i] = flow[i] - (i > 0 ? flow[i - 1] : 0f);
                peak = Math.Max(peak, Math.Abs(voiced[i]));
                flowPeak = Math.Max(flowPeak, flow[i]);
            }
            var scale = peak > 0f ? 1f / peak : 0f;
            var flowScale = flowPeak > 0f ? 1f / flowPeak : 0f;
            for (var i = 0; i < b.Length; i++)
            {
                var time = i / (float)Synth.Rate;
                var breath = p.Breath?.Invoke(time) ?? 0f;
                var gate = 0.35f + 0.65f * flow[i] * flowScale;
                b[i] += voiced[i] * scale + breath * gate * (float)(rng.NextDouble() * 2.0 - 1.0);
            }
        }

        // Amplitude roughness at tens of hertz with a wandering rate: the false vocal folds flapping in a growl.
        public static void Roughen(float[] b, Random rng, Func<float, float> hz, Func<float, float> depth)
        {
            var drift = Wander(rng, b.Length / (float)Synth.Rate, 0.07f);
            var phase = rng.NextDouble();
            for (var i = 0; i < b.Length; i++)
            {
                var time = i / (float)Synth.Rate;
                phase += hz(time) * (1f + 0.25f * drift(time)) / Synth.Rate;
                var wave = 0.5 + 0.5 * Math.Sin(2.0 * Math.PI * phase);
                b[i] *= 1f - depth(time) * (float)wave;
            }
        }

        // Cascade of five resonators (Klatt): each has unity gain at 0 Hz, so the peaks build on each other like
        // a real tract. `tract` scales every formant (below 1: a longer tract), `width` every bandwidth.
        public static void Formants(float[] b, Func<float, Vowel> vowel, float tract = 1f, float width = 1f)
        {
            var y1 = new double[5];
            var y2 = new double[5];
            var ca = new double[5];
            var cb = new double[5];
            var cc = new double[5];
            for (var i = 0; i < b.Length; i++)
            {
                if (i % FormantUpdate == 0)
                {
                    var v = vowel(i / (float)Synth.Rate);
                    Resonator(0, v.F1 * tract, Bandwidths[0] * width, ca, cb, cc);
                    Resonator(1, v.F2 * tract, Bandwidths[1] * width, ca, cb, cc);
                    Resonator(2, v.F3 * tract, Bandwidths[2] * width, ca, cb, cc);
                    Resonator(3, v.F4 * tract, Bandwidths[3] * width, ca, cb, cc);
                    Resonator(4, v.F5 * tract, Bandwidths[4] * width, ca, cb, cc);
                }
                double x = b[i];
                for (var k = 0; k < 5; k++)
                {
                    var y = ca[k] * x + cb[k] * y1[k] + cc[k] * y2[k];
                    y2[k] = y1[k];
                    y1[k] = y;
                    x = y;
                }
                b[i] = (float)x;
            }
        }

        static void Resonator(int k, float hz, float bandwidth, double[] a, double[] b, double[] c)
        {
            var f = Math.Min(hz, Synth.Rate * 0.45f);
            var r = Math.Exp(-Math.PI * bandwidth / Synth.Rate);
            c[k] = -r * r;
            b[k] = 2.0 * r * Math.Cos(2.0 * Math.PI * f / Synth.Rate);
            a[k] = 1.0 - b[k] - c[k];
        }

        // Peak-normalized soft clip: the drive sets how much of the wave reaches the knee.
        public static void Rasp(float[] b, float drive)
        {
            Synth.Normalize(b, 1f);
            var norm = (float)Math.Tanh(drive);
            for (var i = 0; i < b.Length; i++) b[i] = (float)Math.Tanh(b[i] * drive) / norm;
        }

        // Saliva and mucus: tiny bubbles, each a sine rising in pitch as it shrinks and dying in a few ms
        // (van den Doel's bubble model, as in Farnell's water chapters). `rate` is in bubbles per second.
        public static void Bubbles(float[] b, Random rng, Func<float, float> rate, float gain, float minHz, float maxHz)
        {
            var seconds = b.Length / (float)Synth.Rate;
            var t = 0f;
            while (true)
            {
                var r = Math.Max(0.01f, rate(t));
                t += (float)(-Math.Log(1.0 - rng.NextDouble()) / r);
                if (t >= seconds) return;
                if (rate(t) <= 0f) continue;
                var hz = minHz * (float)Math.Pow(maxHz / minHz, rng.NextDouble());
                var life = Range(rng, 0.004f, 0.016f);
                var level = gain * Range(rng, 0.3f, 1f);
                var start = (int)(t * Synth.Rate);
                var length = Math.Min(b.Length - start, (int)(life * Synth.Rate));
                var phase = 0.0;
                for (var i = 0; i < length; i++)
                {
                    var u = i / (float)Synth.Rate;
                    phase += hz * (1f + 1.5f * u / life) / Synth.Rate;
                    b[start + i] += level * (float)(Math.Exp(-4.0 * u / life) * Math.Sin(2.0 * Math.PI * phase));
                }
            }
        }

        // Sparse random impulses (grit, gravel, latch rattle) between start and end, `rate` per second; filter after.
        public static void Crackle(float[] b, Random rng, float start, float end, Func<float, float> rate, float gain)
        {
            var t = start;
            var stop = Math.Min(end, b.Length / (float)Synth.Rate);
            while (true)
            {
                t += (float)(-Math.Log(1.0 - rng.NextDouble()) / Math.Max(1f, rate(t)));
                if (t >= stop) return;
                var i = (int)(t * Synth.Rate);
                var level = gain * Range(rng, 0.2f, 1f) * (rng.NextDouble() < 0.5 ? -1f : 1f);
                b[i] += level;
                if (i + 1 < b.Length) b[i + 1] -= 0.5f * level;
            }
        }

        // Damped sinusoid from `start`: a resonant mode of wood, bone or teeth. `decay` reaches -60 dB.
        public static void Mode(float[] b, float start, float hz, float decay, float gain)
        {
            var first = (int)(start * Synth.Rate);
            for (var i = Math.Max(0, first); i < b.Length; i++)
            {
                var u = (i - first) / (float)Synth.Rate;
                if (u > decay) break;
                b[i] += gain * (float)(Math.Exp(-6.9078 * u / decay) * Math.Sin(2.0 * Math.PI * hz * u));
            }
        }

        // Sine thump whose pitch falls exponentially from `fromHz` to `toHz`: a body or a heel landing.
        public static void Thump(float[] b, float start, float fromHz, float toHz, float decay, float gain)
        {
            var first = (int)(start * Synth.Rate);
            var phase = 0.0;
            for (var i = Math.Max(0, first); i < b.Length; i++)
            {
                var u = (i - first) / (float)Synth.Rate;
                if (u > decay) break;
                var hz = toHz + (fromHz - toHz) * Math.Exp(-6.0 * u / decay);
                phase += hz / Synth.Rate;
                var attack = Math.Min(1f, u / 0.002f);
                b[i] += gain * attack * (float)(Math.Exp(-6.9078 * u / decay) * Math.Sin(2.0 * Math.PI * phase));
            }
        }

        // White noise shaped by a fast attack and an exponential decay, from `start`.
        public static void Burst(float[] b, Random rng, float start, float attack, float decay, float gain)
        {
            var first = (int)(start * Synth.Rate);
            for (var i = Math.Max(0, first); i < b.Length; i++)
            {
                var u = (i - first) / (float)Synth.Rate;
                if (u > attack + decay) break;
                var g = u < attack ? u / attack : (float)Math.Exp(-6.9078 * (u - attack) / decay);
                b[i] += gain * g * (float)(rng.NextDouble() * 2.0 - 1.0);
            }
        }

        // 5 ms fades at both ends so a clip never starts or stops on a click.
        public static void Declick(float[] b)
        {
            var n = Math.Min(b.Length / 2, (int)(Fade * Synth.Rate));
            for (var i = 0; i < n; i++)
            {
                var g = i / (float)n;
                b[i] *= g;
                b[b.Length - 1 - i] *= g;
            }
        }
    }
}
