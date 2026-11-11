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

        // Struck mode: a damped sine starting at `at` that reaches -60 dB after `decay`. glide starts it that much
        // higher in pitch and settles within an eighth of the decay, which gives thumps their punch.
        public static void Ring(float[] b, float at, float hz, float decay, float gain, float glide = 0f)
        {
            var start = (int)Math.Round(at * Rate);
            var count = (int)(decay * Rate);
            var attack = Math.Max(1, Math.Min(Rate / 1000, count / 16));
            var settle = Math.Max(1e-4, decay / 8.0);
            var phase = 0.0;
            for (var i = 0; i < count; i++)
            {
                var j = start + i;
                if (j >= b.Length) break;
                var t = i / (double)Rate;
                phase += hz * (1.0 + glide * Math.Exp(-t / settle)) / Rate;
                if (j < 0) continue;
                var amplitude = Math.Exp(-6.9078 * t / decay) * Math.Min(1.0, (i + 1) / (double)attack);
                b[j] += gain * (float)(amplitude * Math.Sin(2.0 * Math.PI * phase));
            }
        }

        // White noise with a linear attack and a decay to -60 dB, as its own buffer to filter before mixing.
        public static float[] Burst(Random rng, float attack, float decay)
        {
            var b = Buffer(attack + decay);
            White(b, rng);
            Envelope(b, attack, 0f, decay);
            return b;
        }

        // `count` tiny struck particles (pebbles, dry stems, crumbs) over `seconds`, placed by density(t) in 0..1
        // (uniform when null): damped sines log-spread between lowHz and highHz, 1 to 4 ms long, mostly quiet.
        public static float[] Grains(Random rng, float seconds, int count, float lowHz, float highHz, Func<float, float> density = null)
        {
            var b = Buffer(seconds + 0.005f);
            for (var n = 0; n < count; n++)
            {
                var t = (float)rng.NextDouble() * seconds;
                for (var tries = 0; density != null && tries < 64 && rng.NextDouble() > density(t); tries++)
                    t = (float)rng.NextDouble() * seconds;
                var hz = lowHz * Math.Pow(highHz / lowHz, rng.NextDouble());
                var amplitude = rng.NextDouble() * rng.NextDouble() * (rng.NextDouble() < 0.5 ? -1.0 : 1.0);
                Ring(b, t, (float)hz, 0.001f + 0.003f * (float)rng.NextDouble(), (float)amplitude);
            }
            return b;
        }

        // Impulses at a rate following hz(t), each period jittered by up to ±jitter (fraction): the excitation of a
        // voice, a creaking hinge or a stick-slip squeak once filtered through resonances.
        public static void Pulses(float[] b, Random rng, Func<float, float> hz, float jitter, float gain = 1f, float start = 0f, float end = -1f)
        {
            var to = end < 0f ? b.Length : Math.Min(b.Length, (int)(end * Rate));
            var t = (double)start;
            while (true)
            {
                var i = (int)(t * Rate);
                if (i >= to) break;
                if (i >= 0) b[i] += gain;
                var period = 1.0 / Math.Max(1f, hz((float)t));
                t += period * (1.0 + jitter * (rng.NextDouble() * 2.0 - 1.0));
            }
        }

        // Smooth random curve in [-1, 1] that changes `hz` times a second, for pitch jitter and slow wobbles.
        public static Func<float, float> Drift(Random rng, float hz, float seconds)
        {
            var points = new float[(int)(seconds * hz) + 3];
            for (var i = 0; i < points.Length; i++) points[i] = (float)(rng.NextDouble() * 2.0 - 1.0);
            return t =>
            {
                var x = Math.Max(0f, t * hz);
                var i = Math.Min((int)x, points.Length - 2);
                var w = (1f - (float)Math.Cos(Math.Min(1f, x - i) * Math.PI)) / 2f;
                return points[i] * (1f - w) + points[i + 1] * w;
            };
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

        // Rough A-weighted loudness in dBFS: the loudest 50 ms RMS once band-limited to 400 Hz - 10 kHz, where the
        // ear is most sensitive. Peaks say little about how loud a click or a swish sounds.
        public static float Loudness(float[] b)
        {
            var w = (float[])b.Clone();
            HighPass(w, 400f, 0.6f);
            LowPass(w, 10000f, 0.6f);
            var window = Math.Min(Rate / 20, w.Length);
            double sum = 0.0, best = 0.0;
            for (var i = 0; i < w.Length; i++)
            {
                sum += w[i] * w[i];
                if (i >= window) sum -= w[i - window] * w[i - window];
                best = Math.Max(best, sum);
            }
            return (float)(20.0 * Math.Log10(Math.Max(Math.Sqrt(best / window), 1e-9)));
        }

        // Scales to the target loudness, or less if the peak would pass maxPeak.
        public static void NormalizeLoudness(float[] b, float targetDb, float maxPeak = 0.95f)
        {
            var gain = (float)Math.Pow(10.0, (targetDb - Loudness(b)) / 20.0);
            var peak = Peak(b) * gain;
            if (peak > maxPeak) gain *= maxPeak / peak;
            for (var i = 0; i < b.Length; i++) b[i] *= gain;
        }

        // Copy without the tail that stays below floorDb under the peak, ending on a 5 ms fade.
        public static float[] Trim(float[] b, float floorDb = -60f)
        {
            var floor = Peak(b) * (float)Math.Pow(10.0, floorDb / 20.0);
            var end = b.Length;
            while (end > 1 && Math.Abs(b[end - 1]) <= floor) end--;
            var fade = Rate / 200;
            end = Math.Min(b.Length, end + fade);
            var trimmed = new float[end];
            Array.Copy(b, trimmed, end);
            for (var i = 0; i < Math.Min(fade, end); i++) trimmed[end - 1 - i] *= i / (float)fade;
            return trimmed;
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
