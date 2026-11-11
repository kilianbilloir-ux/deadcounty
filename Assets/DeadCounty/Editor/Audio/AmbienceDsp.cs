using System;

namespace DeadCounty.EditorTools
{
    // Time-varying filters, space and small generators the ambience and interface recipes need on top of Synth.
    // System only, like Synth, so the recipes can be rendered and measured outside Unity.
    static class AmbienceDsp
    {
        const int CoefficientStride = 16;

        public enum Mode { Low, Band, High }

        // Zavalishin's TPT state-variable filter: stays stable while the cutoff moves, up to near Nyquist.
        public static void Sweep(float[] b, Mode mode, Func<float, float> hzAt, Func<float, float> qAt)
        {
            double ic1 = 0, ic2 = 0, a1 = 0, a2 = 0, a3 = 0, k = 0;
            for (var i = 0; i < b.Length; i++)
            {
                if (i % CoefficientStride == 0)
                {
                    var t = i / (float)Synth.Rate;
                    var hz = Math.Clamp(hzAt(t), 10f, Synth.Rate * 0.45f);
                    var g = Math.Tan(Math.PI * hz / Synth.Rate);
                    k = 1.0 / Math.Max(0.05f, qAt(t));
                    a1 = 1.0 / (1.0 + g * (g + k));
                    a2 = g * a1;
                    a3 = g * a2;
                }
                var x = b[i];
                var v3 = x - ic2;
                var v1 = a1 * ic1 + a2 * v3;
                var v2 = ic2 + a2 * ic1 + a3 * v3;
                ic1 = 2 * v1 - ic1;
                ic2 = 2 * v2 - ic2;
                b[i] = (float)(mode switch
                {
                    Mode.Low => v2,
                    Mode.Band => v1 * k,
                    _ => x - k * v1 - v2,
                });
            }
        }

        public static void Sweep(float[] b, Mode mode, Func<float, float> hzAt, float q) => Sweep(b, mode, hzAt, _ => q);

        // Freeverb-style tail: damped parallel combs then series allpasses. Mixed in place, so the buffer must
        // already hold the room the tail needs.
        public static void Reverb(float[] b, float decaySeconds, float dampHz, float wet, float size = 1f)
        {
            int[] combs = { 1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617 };
            int[] allpasses = { 556, 441, 341, 225 };
            var scale = Synth.Rate / 44100f * size;
            var tail = new float[b.Length];
            var damp = (float)Math.Exp(-2 * Math.PI * dampHz / Synth.Rate);
            foreach (var c in combs)
            {
                var length = Math.Max(1, (int)(c * scale));
                var feedback = (float)Math.Pow(10, -3.0 * length / Synth.Rate / decaySeconds);
                var line = new float[length];
                var index = 0;
                var store = 0f;
                for (var i = 0; i < b.Length; i++)
                {
                    var output = line[index];
                    store = output * (1f - damp) + store * damp;
                    line[index] = b[i] * 0.015f + store * feedback;
                    tail[i] += output;
                    if (++index == length) index = 0;
                }
            }
            foreach (var a in allpasses)
            {
                var length = Math.Max(1, (int)(a * scale));
                var line = new float[length];
                var index = 0;
                for (var i = 0; i < tail.Length; i++)
                {
                    var delayed = line[index];
                    var input = tail[i];
                    line[index] = input + delayed * 0.5f;
                    tail[i] = delayed - input;
                    if (++index == length) index = 0;
                }
            }
            for (var i = 0; i < b.Length; i++) b[i] = b[i] * (1f - wet) + tail[i] * wet * 4f;
        }

        // Discrete echoes, each darker than the last: a burst thrown back by the far side of a valley or a street.
        public static void Echo(float[] b, float delaySeconds, float feedback, float dampHz, int repeats)
        {
            var delay = (int)(delaySeconds * Synth.Rate);
            var source = (float[])b.Clone();
            var gain = 1f;
            for (var r = 1; r <= repeats; r++)
            {
                Synth.LowPass(source, dampHz);
                gain *= feedback;
                var offset = delay * r;
                for (var i = 0; i + offset < b.Length; i++) b[i + offset] += source[i] * gain;
            }
        }

        // Sum of sines with whole numbers of cycles over the period: a smooth random curve that loops exactly.
        public static Func<float, float> PeriodicWander(Random rng, float period, int lowestCycles, int highestCycles, int partials)
        {
            var cycles = new float[partials];
            var phases = new float[partials];
            var weights = new float[partials];
            var total = 0f;
            for (var p = 0; p < partials; p++)
            {
                cycles[p] = rng.Next(lowestCycles, highestCycles + 1);
                phases[p] = (float)(rng.NextDouble() * Math.PI * 2);
                weights[p] = 1f / cycles[p];
                total += weights[p];
            }
            return t =>
            {
                var sum = 0f;
                for (var p = 0; p < partials; p++)
                    sum += weights[p] * (float)Math.Sin(2 * Math.PI * cycles[p] * t / period + phases[p]);
                return sum / total;
            };
        }

        // Frequency rounded to a whole number of cycles over the loop, so the tone joins itself at the seam.
        public static float LoopHz(float hz, float loopSeconds) => (float)Math.Round(hz * loopSeconds) / loopSeconds;

        // Hann-windowed sine grain mixed at an offset: the syllable of crickets, ticks and bird trills.
        public static void Grain(float[] b, float at, float seconds, Func<float, float> hz, float gain, float harmonic = 0f)
        {
            var start = (int)(at * Synth.Rate);
            var length = (int)(seconds * Synth.Rate);
            var phase = 0.0;
            for (var i = 0; i < length && start + i < b.Length; i++)
            {
                if (start + i < 0) continue;
                var t = i / (float)Synth.Rate;
                phase += 2 * Math.PI * hz(t) / Synth.Rate;
                var window = 0.5f - 0.5f * (float)Math.Cos(2 * Math.PI * i / length);
                var s = Math.Sin(phase) + harmonic * Math.Sin(2 * phase);
                b[start + i] += (float)s * window * gain;
            }
        }

        // Damped sine: the body of a click or a knock.
        public static void Ping(float[] b, float at, float hz, float decaySeconds, float gain, float endHz = -1f)
        {
            var start = (int)(at * Synth.Rate);
            var length = (int)(decaySeconds * 7f * Synth.Rate);
            var phase = 0.0;
            for (var i = 0; i < length && start + i < b.Length; i++)
            {
                var t = i / (float)Synth.Rate;
                var f = endHz > 0f ? hz + (endHz - hz) * Math.Min(1f, t / (decaySeconds * 3f)) : hz;
                phase += 2 * Math.PI * f / Synth.Rate;
                b[start + i] += (float)(Math.Sin(phase) * Math.Exp(-t / decaySeconds)) * gain;
            }
        }

        // Wraps the tail over the head (equal power) and drops it: a buffer rendered crossfade seconds longer than the
        // loop joins itself, and content made periodic over the loop (LoopHz, PeriodicWander, chirp grids) stays in
        // phase across the seam, which Synth.Loopable's mid-buffer splice would break.
        public static float[] Loop(float[] b, float crossfadeSeconds)
        {
            var n = (int)(crossfadeSeconds * Synth.Rate);
            var output = new float[b.Length - n];
            Array.Copy(b, output, output.Length);
            for (var i = 0; i < n; i++)
            {
                var x = (i + 0.5f) / n;
                var fadeIn = (float)Math.Sin(x * Math.PI / 2);
                var fadeOut = (float)Math.Cos(x * Math.PI / 2);
                output[i] = b[i] * fadeIn + b[output.Length + i] * fadeOut;
            }
            return output;
        }

        // The loop seconds starting at from: content periodic over the loop wraps there without a crossfade.
        public static float[] Slice(float[] b, float from, float seconds)
        {
            var output = Synth.Buffer(seconds);
            Array.Copy(b, (int)(from * Synth.Rate), output, 0, Math.Min(output.Length, b.Length - (int)(from * Synth.Rate)));
            return output;
        }

        // Drops what follows the last sample above floorDb under the peak (a tail nobody hears in the mix), with a
        // short fade so the cut is clean.
        public static float[] Trim(float[] b, float floorDb = -55f)
        {
            var threshold = Synth.Peak(b) * (float)Math.Pow(10, floorDb / 20f);
            var last = b.Length - 1;
            while (last > 0 && Math.Abs(b[last]) < threshold) last--;
            var fade = (int)(0.03f * Synth.Rate);
            var output = new float[Math.Min(b.Length, last + fade)];
            Array.Copy(b, output, output.Length);
            for (var i = 0; i < fade && i < output.Length; i++) output[output.Length - 1 - i] *= i / (float)fade;
            return output;
        }

        public static float Smooth(float edge0, float edge1, float x)
        {
            var t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
            return t * t * (3f - 2f * t);
        }
    }
}
