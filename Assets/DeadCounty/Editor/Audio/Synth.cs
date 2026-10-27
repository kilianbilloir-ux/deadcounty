using System;

namespace DeadCounty.EditorTools
{
    // Offline DSP on mono float buffers for the generated sounds. Depends on System only, so recipes can be compiled
    // outside Unity, rendered and analysed. Times are in seconds, frequencies in hertz.
    public static class Synth
    {
        public const int Rate = 32000;
        const float Declick = 0.002f;

        public enum Wave { Sine, Saw, Square, Triangle }

        public static float[] Buffer(float seconds) => new float[Math.Max(1, (int)Math.Round(seconds * Rate))];

        public static float Seconds(float[] b) => b.Length / (float)Rate;

        public static void White(float[] b, Random rng, float gain = 1f)
        {
            for (var i = 0; i < b.Length; i++) b[i] += gain * (float)(rng.NextDouble() * 2.0 - 1.0);
        }

        // Leaky integral of white noise (-6 dB/octave), centred, peaks around ±0.7.
        public static void Brown(float[] b, Random rng, float gain = 1f)
        {
            var y = 0.0;
            for (var i = 0; i < b.Length; i++)
            {
                y = (y + 0.02 * (rng.NextDouble() * 2.0 - 1.0)) / 1.02;
                b[i] += gain * (float)(y * 3.5);
            }
        }

        // Band-limited (polyBLEP) oscillator; its edges get a 2 ms ramp so a gate inside the buffer does not click.
        public static void Tone(float[] b, Func<float, float> hz, Wave wave, float gain = 1f, float start = 0f, float end = -1f)
        {
            var from = Math.Max(0, (int)(start * Rate));
            var to = end < 0f ? b.Length : Math.Min(b.Length, (int)(end * Rate));
            var ramp = Math.Max(1, (int)(Declick * Rate));
            var phase = 0.0;
            for (var i = from; i < to; i++)
            {
                var dt = Math.Abs(hz(i / (float)Rate)) / Rate;
                var edge = Math.Min(1f, Math.Min(i - from + 1, to - i) / (float)ramp);
                b[i] += gain * edge * Oscillator(wave, phase, dt);
                phase += dt;
                phase -= Math.Floor(phase);
            }
        }

        static float Oscillator(Wave wave, double p, double dt)
        {
            switch (wave)
            {
                case Wave.Sine: return (float)Math.Sin(2.0 * Math.PI * p);
                case Wave.Saw: return (float)(2.0 * p - 1.0 - PolyBlep(p, dt));
                case Wave.Square:
                    var square = p < 0.5 ? 1.0 : -1.0;
                    return (float)(square + PolyBlep(p, dt) - PolyBlep((p + 0.5) % 1.0, dt));
                default: return (float)(1.0 - 4.0 * Math.Abs(p - 0.5));
            }
        }

        static double PolyBlep(double t, double dt)
        {
            if (dt <= 0.0) return 0.0;
            if (t < dt)
            {
                t /= dt;
                return t + t - t * t - 1.0;
            }
            if (t > 1.0 - dt)
            {
                t = (t - 1.0) / dt;
                return t * t + t + t + 1.0;
            }
            return 0.0;
        }

        // RBJ cookbook biquads, in place.
        public static void LowPass(float[] b, float hz, float q = 0.707f)
        {
            var (cos, alpha) = Coefficients(hz, q);
            Biquad(b, (1.0 - cos) / 2.0, 1.0 - cos, (1.0 - cos) / 2.0, 1.0 + alpha, -2.0 * cos, 1.0 - alpha);
        }

        public static void HighPass(float[] b, float hz, float q = 0.707f)
        {
            var (cos, alpha) = Coefficients(hz, q);
            Biquad(b, (1.0 + cos) / 2.0, -(1.0 + cos), (1.0 + cos) / 2.0, 1.0 + alpha, -2.0 * cos, 1.0 - alpha);
        }

        // Unity gain at the centre frequency.
        public static void BandPass(float[] b, float hz, float q = 1f)
        {
            var (cos, alpha) = Coefficients(hz, q);
            Biquad(b, alpha, 0.0, -alpha, 1.0 + alpha, -2.0 * cos, 1.0 - alpha);
        }

        // Band-pass whose centre follows hz(t): vocal formants, creaks, wind gusts.
        public static void SweepBandPass(float[] b, Func<float, float> hz, float q = 1f)
        {
            double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
            for (var i = 0; i < b.Length; i++)
            {
                var (cos, alpha) = Coefficients(hz(i / (float)Rate), q);
                var a0 = 1.0 + alpha;
                var x = (double)b[i];
                var y = (alpha * x - alpha * x2 + 2.0 * cos * y1 - (1.0 - alpha) * y2) / a0;
                x2 = x1;
                x1 = x;
                y2 = y1;
                y1 = y;
                b[i] = (float)y;
            }
        }

        static (double cos, double alpha) Coefficients(float hz, float q)
        {
            var w0 = 2.0 * Math.PI * Math.Clamp(hz, 1f, Rate * 0.49f) / Rate;
            return (Math.Cos(w0), Math.Sin(w0) / (2.0 * Math.Max(0.05f, q)));
        }

        static void Biquad(float[] b, double b0, double b1, double b2, double a0, double a1, double a2)
        {
            b0 /= a0;
            b1 /= a0;
            b2 /= a0;
            a1 /= a0;
            a2 /= a0;
            double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
            for (var i = 0; i < b.Length; i++)
            {
                var x = (double)b[i];
                var y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1;
                x1 = x;
                y2 = y1;
                y1 = y;
                b[i] = (float)y;
            }
        }

        // Linear attack, flat hold, then an exponential decay that reaches -60 dB after `decay` and silence beyond.
        public static void Envelope(float[] b, float attack, float hold, float decay)
        {
            for (var i = 0; i < b.Length; i++)
            {
                var t = i / (float)Rate;
                float g;
                if (t < attack) g = t / attack;
                else if (t < attack + hold) g = 1f;
                else
                {
                    var u = t - attack - hold;
                    g = decay > 0f && u < decay ? (float)Math.Exp(-6.9078 * u / decay) : 0f;
                }
                b[i] *= g;
            }
        }

        public static void Shape(float[] b, Func<float, float> gainAt)
        {
            for (var i = 0; i < b.Length; i++) b[i] *= gainAt(i / (float)Rate);
        }

        public static void Mix(float[] into, float[] from, float offsetSeconds = 0f, float gain = 1f)
        {
            var offset = (int)Math.Round(offsetSeconds * Rate);
            for (var i = Math.Max(0, -offset); i < from.Length; i++)
            {
                var j = offset + i;
                if (j >= into.Length) break;
                into[j] += gain * from[i];
            }
        }

        // Soft clipping (tanh), level-compensated so small signals keep their gain: grit for impacts and voices.
        public static void Saturate(float[] b, float drive)
        {
            if (drive <= 0f) return;
            for (var i = 0; i < b.Length; i++) b[i] = (float)Math.Tanh(b[i] * drive) / drive;
        }

        public static float Peak(float[] b)
        {
            var peak = 0f;
            foreach (var s in b) peak = Math.Max(peak, Math.Abs(s));
            return peak;
        }

        public static void Normalize(float[] b, float peak = 0.9f)
        {
            var max = Peak(b);
            if (max <= 0f) return;
            var scale = peak / max;
            for (var i = 0; i < b.Length; i++) b[i] *= scale;
        }

        // Splices a copy of the middle of the buffer across the seam: the tail crossfades (equal power) into the
        // material just before the middle, whose continuation crossfades into the head. The length is kept, so the
        // copied 2 x crossfade of material is heard twice per loop.
        public static void Loopable(float[] b, float crossfadeSeconds)
        {
            var n = Math.Min((int)(crossfadeSeconds * Rate), b.Length / 4);
            if (n < 2) return;
            var middle = b.Length / 2;
            var tail = b.Length - n;
            for (var i = 0; i < n; i++)
            {
                var u = i / (double)(n - 1) * Math.PI / 2.0;
                var fadeIn = (float)Math.Sin(u);
                var fadeOut = (float)Math.Cos(u);
                b[tail + i] = b[tail + i] * fadeOut + b[middle - n + i] * fadeIn;
                b[i] = b[middle + i] * fadeOut + b[i] * fadeIn;
            }
        }
    }
}
