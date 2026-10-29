using System;
using static DeadCounty.EditorTools.VoiceSynth;

namespace DeadCounty.EditorTools
{
    // Recipes of what a zombie's body does: the lunge, the strike landing or snapping on air, fists on a door,
    // shuffled and dragged feet. One variant per call; System-only, like Synth.
    public static class ZombieBodyRecipes
    {
        // Arms swept forward on the lunge: an air band rising and falling, a flap of sleeve.
        public static float[] Whoosh(Random rng)
        {
            var seconds = Range(rng, 0.3f, 0.36f);
            var peak = Range(rng, 0.1f, 0.14f);
            var top = Range(rng, 1100f, 1600f);
            var b = Synth.Buffer(seconds);
            Synth.White(b, rng);
            Synth.SweepBandPass(b, t => 350f + top * (float)Math.Exp(-Math.Pow((t - peak) / 0.07f, 2.0)), 1.2f);
            var body = Synth.Buffer(seconds);
            Synth.Brown(body, rng);
            Synth.LowPass(body, 320f);
            Synth.Mix(b, body, 0f, 0.8f);
            var flap = Range(rng, 60f, 90f);
            Roughen(b, rng, _ => flap, _ => 0.3f);
            Synth.Shape(b, t => t < peak ? (float)Math.Pow(t / peak, 2.0) : (float)Math.Exp(-5.0 * (t - peak) / (seconds - peak)));
            Declick(b);
            Synth.Normalize(b, 0.8f);
            return b;
        }

        // The grab lands: body thump, slap of hands, cloth and a wet squelch.
        public static float[] Hit(Random rng)
        {
            var b = Synth.Buffer(0.32f);
            Thump(b, 0f, Range(rng, 110f, 135f), 55f, 0.18f, 1f);
            Thump(b, Range(rng, 0.06f, 0.09f), 90f, 60f, 0.1f, 0.5f);
            Layer(b, rng, 0f, 0.001f, 0.08f, 0.8f, l => Synth.LowPass(l, 500f));
            Layer(b, rng, 0.003f, 0.0005f, 0.03f, 0.6f, l => Synth.BandPass(l, Range(rng, 1500f, 2100f), 0.7f));
            var cloth = Synth.Buffer(0.32f);
            Crackle(cloth, rng, 0.005f, 0.12f, _ => 400f, 1f);
            Synth.BandPass(cloth, 2500f, 0.8f);
            Synth.Mix(b, cloth, 0f, 0.4f);
            var squelch = Synth.Buffer(0.32f);
            Bubbles(squelch, rng, t => t < 0.1f ? 300f : 0f, 0.6f, 200f, 900f);
            Synth.Mix(b, squelch, 0.01f, 0.5f);
            Rasp(b, 1.8f);
            Synth.HighPass(b, 40f);
            Declick(b);
            Synth.Normalize(b, 0.95f);
            return b;
        }

        // The jaws snap shut on air, clack again, and a hiss of breath follows.
        public static float[] Miss(Random rng)
        {
            var b = Synth.Buffer(Range(rng, 0.34f, 0.4f));
            Clack(b, rng, 0.01f, 1f);
            Clack(b, rng, Range(rng, 0.08f, 0.12f), Range(rng, 0.45f, 0.7f));
            Layer(b, rng, 0.03f, 0.03f, 0.2f, 0.7f, l =>
            {
                var bright = (float[])l.Clone();
                Synth.BandPass(l, 1200f, 0.7f);
                Synth.BandPass(bright, 2600f, 1f);
                for (var i = 0; i < l.Length; i++) l[i] += bright[i];
            });
            Synth.HighPass(b, 100f);
            Declick(b);
            Synth.Normalize(b);
            return b;
        }

        static void Clack(float[] b, Random rng, float start, float gain)
        {
            Layer(b, rng, start, 0.0003f, 0.004f, gain, l => Synth.HighPass(l, 1500f));
            Mode(b, start, Range(rng, 2600f, 3400f), 0.02f, 0.4f * gain);
            Mode(b, start, Range(rng, 1200f, 1600f), 0.015f, 0.3f * gain);
        }

        // Fists on a shut wooden door: panel modes, a thud, the latch rattling in its frame; sometimes two blows.
        public static float[] DoorBang(Random rng)
        {
            var second = rng.NextDouble() < 0.5 ? Range(rng, 0.16f, 0.24f) : 0f;
            var seconds = second + 0.32f;
            var b = Synth.Buffer(seconds);
            var panel = Range(rng, 85f, 115f);
            Blow(b, rng, 0f, 1f, panel);
            if (second > 0f) Blow(b, rng, second, Range(rng, 0.6f, 0.8f), panel);
            var latch = Synth.Buffer(seconds);
            var rattle = Range(rng, 30f, 50f);
            Crackle(latch, rng, 0.02f, 0.16f, _ => rattle, 1f);
            Synth.BandPass(latch, Range(rng, 1900f, 2500f), 4f);
            Synth.Mix(b, latch, 0f, 0.5f);
            Rasp(b, 1.5f);
            Synth.HighPass(b, 40f);
            Declick(b);
            Synth.Normalize(b);
            return b;
        }

        // A wooden panel has many inharmonic modes; the higher ones die fast and carry the knock.
        static void Blow(float[] b, Random rng, float start, float gain, float panel)
        {
            Layer(b, rng, start, 0.001f, 0.015f, gain, l => Synth.LowPass(l, 3000f));
            Mode(b, start, panel, Range(rng, 0.2f, 0.28f), 0.6f * gain);
            Mode(b, start, panel * Range(rng, 1.6f, 1.9f), 0.15f, 0.5f * gain);
            for (var i = 0; i < 6; i++)
            {
                var hz = 220f * (float)Math.Pow(5.0, (i + rng.NextDouble()) / 6.0);
                Mode(b, start, hz, Range(rng, 0.04f, 0.1f) * 300f / hz + 0.02f, Range(rng, 0.25f, 0.45f) * gain);
            }
            Thump(b, start, 75f, 50f, 0.12f, 0.5f * gain);
        }

        // Shuffled step on grass or dirt: a short slide, then the heel lands dull.
        public static float[] StepSoft(Random rng)
        {
            var b = Synth.Buffer(0.22f);
            var heel = Range(rng, 0.05f, 0.08f);
            Scuff(b, rng, heel, Range(rng, 900f, 1300f), 0.3f);
            Thump(b, heel, Range(rng, 75f, 95f), 50f, 0.07f, 0.4f);
            Layer(b, rng, heel, 0.002f, 0.06f, 0.9f, l => Synth.LowPass(l, 800f));
            Layer(b, rng, heel - 0.01f, 0.01f, 0.07f, 0.12f, l => Synth.HighPass(l, 3500f));
            return Finish(b);
        }

        // Shuffled step on asphalt, concrete or boards: gritty sole scuff, then a heel tap.
        public static float[] StepHard(Random rng)
        {
            var b = Synth.Buffer(0.2f);
            var heel = Range(rng, 0.05f, 0.08f);
            Scuff(b, rng, heel, Range(rng, 2200f, 3000f), 0.3f);
            var grit = Synth.Buffer(0.2f);
            Crackle(grit, rng, 0f, heel, _ => 500f, 1f);
            Synth.BandPass(grit, 4000f, 0.9f);
            Synth.Mix(b, grit, 0f, 0.2f);
            Layer(b, rng, heel, 0.0005f, 0.02f, 0.6f, l => Synth.HighPass(l, 900f));
            Mode(b, heel, Range(rng, 320f, 420f), 0.035f, 0.3f);
            Thump(b, heel, Range(rng, 90f, 110f), 60f, 0.05f, 0.6f);
            return Finish(b);
        }

        public static float[] StepGravel(Random rng)
        {
            var b = Synth.Buffer(0.28f);
            var heel = Range(rng, 0.04f, 0.06f);
            var crunch = Synth.Buffer(0.28f);
            Crackle(crunch, rng, heel - 0.02f, 0.24f, t => 1200f * (float)Math.Exp(-(t - heel) / 0.06f) + 150f, 1f);
            var low = (float[])crunch.Clone();
            Synth.BandPass(crunch, Range(rng, 2800f, 3600f), 0.6f);
            Synth.LowPass(low, 1200f);
            Synth.Mix(b, crunch, 0f, 0.7f);
            Synth.Mix(b, low, 0f, 0.35f);
            Thump(b, heel, 80f, 50f, 0.07f, 0.5f);
            return Finish(b);
        }

        // A bad leg dragged forward through grass or dirt: a long stick-slip scrape.
        public static float[] DragSoft(Random rng)
        {
            var b = Drag(rng, out var seconds);
            Synth.BandPass(b, Range(rng, 800f, 1100f), 0.9f);
            var blades = Synth.Buffer(seconds);
            Synth.White(blades, rng, 0.03f);
            Synth.HighPass(blades, 3500f);
            Synth.Mix(b, blades);
            return DragFinish(b, rng, seconds);
        }

        public static float[] DragHard(Random rng)
        {
            var b = Drag(rng, out var seconds);
            var rumble = (float[])b.Clone();
            Synth.BandPass(b, Range(rng, 2000f, 2500f), 1.1f);
            Synth.BandPass(rumble, 400f, 0.7f);
            Synth.Mix(b, rumble, 0f, 0.3f);
            var grit = Synth.Buffer(seconds);
            Crackle(grit, rng, 0f, seconds, _ => 350f, 1f);
            Synth.BandPass(grit, 4500f, 1f);
            Synth.Mix(b, grit, 0f, 0.2f);
            return DragFinish(b, rng, seconds);
        }

        static float[] Drag(Random rng, out float seconds)
        {
            seconds = Range(rng, 0.45f, 0.6f);
            var b = Synth.Buffer(seconds);
            Synth.White(b, rng);
            return b;
        }

        static float[] DragFinish(float[] b, Random rng, float seconds)
        {
            var slip = Range(rng, 18f, 32f);
            Roughen(b, rng, _ => slip, _ => 0.55f);
            var swell = Wander(rng, seconds, 0.08f);
            Synth.Shape(b, t => Smooth(0f, 0.1f, t) * (1f - Smooth(seconds - 0.2f, seconds, t)) * (0.75f + 0.25f * swell(t)));
            return Finish(b);
        }

        static void Scuff(float[] b, Random rng, float until, float hz, float gain)
        {
            Layer(b, rng, 0f, until, 0.03f, gain, l => Synth.BandPass(l, hz, 0.9f));
        }

        static float[] Finish(float[] b)
        {
            Synth.HighPass(b, 40f);
            Declick(b);
            Synth.Normalize(b);
            return b;
        }

        // A noise burst at `start`, filtered on its own before it joins the mix.
        static void Layer(float[] b, Random rng, float start, float attack, float decay, float gain, Action<float[]> filter)
        {
            var layer = Synth.Buffer(Math.Min(attack + decay + 0.01f, Synth.Seconds(b)));
            Burst(layer, rng, 0f, attack, decay, 1f);
            filter(layer);
            Synth.Mix(b, layer, start, gain);
        }
    }
}
