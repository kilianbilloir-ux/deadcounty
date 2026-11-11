using System;
using static DeadCounty.EditorTools.AmbienceDsp;

namespace DeadCounty.EditorTools
{
    // Interface sounds, one family of muted wooden taps (a damped sine body and a short noise grain) that stays
    // pleasant at the hundredth repeat: up means forward, down means back. Then the title's dark drone and the
    // far church bell that tolls over it.
    static class InterfaceRecipes
    {
        public const float DroneLoop = 15f;
        const float RootHz = 55f;
        const float Seam = 1.5f;

        public static float[] Move(Random rng) => Tap(rng, 0.06f, (0f, 1850f + 200f * (float)rng.NextDouble(), 0.006f, 1f));

        public static float[] Step(Random rng) => Tap(rng, 0.05f, (0f, 2350f + 150f * (float)rng.NextDouble(), 0.004f, 1f));

        public static float[] Confirm(Random rng) => Tap(rng, 0.18f, (0f, 1300f, 0.012f, 0.8f), (0.05f, 1950f, 0.016f, 1f));

        public static float[] Back(Random rng) => Tap(rng, 0.18f, (0f, 1650f, 0.012f, 1f), (0.05f, 1100f, 0.016f, 0.8f));

        public static float[] Toggle(Random rng) => Tap(rng, 0.1f, (0f, 2600f, 0.004f, 1f), (0.02f, 1750f, 0.006f, 0.8f));

        static float[] Tap(Random rng, float seconds, params (float at, float hz, float decay, float gain)[] taps)
        {
            var b = Synth.Buffer(seconds);
            foreach (var (at, hz, decay, gain) in taps)
            {
                var grain = Synth.Buffer(0.004f);
                Synth.White(grain, rng);
                Synth.BandPass(grain, hz * 2f, 1.5f);
                Synth.Envelope(grain, 0.0005f, 0f, 0.0035f);
                Synth.Mix(b, grain, at, 0.6f * gain);
                Ping(b, at, hz, decay, gain);
                Ping(b, at, hz * 0.5f, decay * 0.8f, 0.25f * gain);
            }
            Synth.LowPass(b, 6000f);
            Synth.Envelope(b, 0.0005f, seconds - 0.02f, 0.02f);
            Synth.Normalize(b, 0.8f);
            return b;
        }

        // The pause opening: a low thump and a swell of air rising, as if the world was pushed back.
        public static float[] Open(Random rng) => Breath(rng, 0.3f, 300f, 1300f, 0.07f, 160f);

        public static float[] Close(Random rng) => Breath(rng, 0.28f, 1300f, 280f, 0.015f, 120f);

        static float[] Breath(Random rng, float seconds, float fromHz, float toHz, float attack, float thumpHz)
        {
            var b = Synth.Buffer(seconds);
            Synth.White(b, rng);
            Sweep(b, Mode.Band, t => fromHz + (toHz - fromHz) * Smooth(0f, seconds * 0.8f, t), 0.9f);
            Synth.Envelope(b, attack, 0.03f, seconds - attack - 0.03f);
            Ping(b, 0f, thumpHz, 0.05f, 0.6f, thumpHz * 0.8f);
            Synth.Envelope(b, 0.001f, seconds - 0.03f, 0.03f);
            Synth.Normalize(b, 0.8f);
            return b;
        }

        // The backpack opened or closed: canvas rustle (a burst of crackle in a mid band) and the flap's soft thud.
        public static float[] Bag(Random rng, bool open)
        {
            var seconds = open ? 0.26f : 0.18f;
            var b = Synth.Buffer(seconds);
            var rustle = Synth.Buffer(seconds);
            var density = open ? 320f : 240f;
            for (var t = 0f; t < seconds - 0.03f; t += (float)(-Math.Log(1.0 - rng.NextDouble()) / density))
            {
                var grain = Synth.Buffer(0.004f);
                Synth.White(grain, rng);
                Synth.Envelope(grain, 0.0003f, 0f, 0.0035f);
                Synth.Mix(rustle, grain, t, 0.3f + 0.7f * (float)rng.NextDouble());
            }
            Synth.BandPass(rustle, open ? 2600f : 2200f, 0.7f);
            Synth.Shape(rustle, t => open ? Smooth(0f, 0.06f, t) * Smooth(seconds, seconds * 0.5f, t) : Smooth(seconds, 0.04f, t));
            Synth.Mix(b, rustle);
            Ping(b, open ? seconds - 0.08f : 0.01f, 110f, 0.035f, 0.5f, 85f);
            Synth.Envelope(b, 0.001f, seconds - 0.02f, 0.02f);
            Synth.Normalize(b, 0.8f);
            return b;
        }

        // Dark ambient pad over A: a sub, a slowly opening sawtooth fifth and octave detuned to beat once per loop,
        // a flat sixth swelling once, a cold shimmer and air. Every partial and modulation has a whole number of
        // cycles in the loop and the reverb settles over a lead-in, so the tones wrap as cut; only the air is
        // crossfaded.
        public static float[] TitleDrone(Random rng)
        {
            const float leadIn = 8f;
            var seconds = leadIn + DroneLoop;
            var b = Synth.Buffer(seconds);
            float Hz(float hz) => LoopHz(hz, DroneLoop);
            float Cycle(float t, float cycles, float phase) => (float)Math.Sin(2 * Math.PI * cycles * t / DroneLoop + phase);

            Synth.Tone(b, _ => Hz(RootHz), Synth.Wave.Sine, 0.32f);
            Synth.Shape(b, t => 0.8f + 0.2f * Cycle(t, 1f, 0f));

            var pad = Synth.Buffer(seconds);
            foreach (var (hz, gain) in new[] { (110f, 0.16f), (110f + 1f / DroneLoop, 0.16f), (164.8f, 0.11f), (164.8f + 2f / DroneLoop, 0.11f), (130.8f, 0.05f), (220f, 0.05f) })
                Synth.Tone(pad, _ => Hz(hz), Synth.Wave.Saw, gain);
            Sweep(pad, Mode.Low, t => 260f + 520f * (0.5f + 0.5f * Cycle(t, 1f, 2.2f)) + 90f * Cycle(t, 3f, 0.4f), 1.1f);
            Synth.Mix(b, pad);

            var sixth = Synth.Buffer(seconds);
            Synth.Tone(sixth, _ => Hz(174.6f), Synth.Wave.Triangle, 0.07f);
            Synth.Shape(sixth, t => Math.Max(0f, Cycle(t, 1f, 4f)) * Math.Max(0f, Cycle(t, 1f, 4f)));
            Synth.Mix(b, sixth);

            var shimmer = Synth.Buffer(seconds);
            Synth.Tone(shimmer, _ => Hz(880f), Synth.Wave.Sine, 0.018f);
            Synth.Tone(shimmer, _ => Hz(1318.5f), Synth.Wave.Sine, 0.012f);
            Synth.Shape(shimmer, t => 0.5f + 0.5f * Cycle(t, 5f, 1f) * Cycle(t, 2f, 0.3f));
            Synth.Mix(b, shimmer);
            Reverb(b, 4.5f, 2500f, 0.35f, 1.6f);
            var loop = Slice(b, leadIn, DroneLoop);

            var air = Synth.Buffer(seconds + Seam);
            Synth.White(air, rng);
            Sweep(air, Mode.Band, t => 1400f + 600f * Cycle(t, 2f, 1.7f), 0.9f);
            Synth.Shape(air, t => 0.4f + 0.6f * Smooth(-0.6f, 1f, Cycle(t, 1f, 5.1f)));
            Reverb(air, 4.5f, 2500f, 0.35f, 1.6f);
            Synth.Mix(loop, Loop(Slice(air, leadIn, DroneLoop + Seam), Seam), 0f, 0.035f);
            Synth.Normalize(loop, 0.85f);
            return loop;
        }

        // A church bell a mile off: minor-third bell partials over a long hum, the strike lost in the distance.
        public static float[] Toll(Random rng)
        {
            var b = Synth.Buffer(3.8f);
            const float strike = 220f;
            foreach (var (ratio, decay, gain) in new[] { (0.5f, 0.9f, 0.8f), (1f, 0.8f, 1f), (1.2f, 0.7f, 0.6f), (1.5f, 0.5f, 0.35f), (2f, 0.45f, 0.45f), (2.52f, 0.3f, 0.2f), (3.01f, 0.22f, 0.15f) })
                Ping(b, 0.02f, strike * ratio * (1f + 0.002f * (float)rng.NextDouble()), decay, gain);
            var knock = Synth.Buffer(0.01f);
            Synth.White(knock, rng);
            Synth.Mix(b, knock, 0.02f, 0.3f);
            return AmbienceRecipes.Outdoors(b, 1600f, 2.5f, 0.5f);
        }
    }
}
