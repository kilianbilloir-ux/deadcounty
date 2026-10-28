using System;
using static DeadCounty.EditorTools.StepRecipes;
using static DeadCounty.EditorTools.Synth;

namespace DeadCounty.EditorTools
{
    // What the survivor handles: searched furniture, items, the flashlight, beds and doors. Built from the same
    // impact pieces as the footsteps: a transient, the resonances of the struck object, a tail.
    public static class ActionRecipes
    {
        // Hands digging through a drawer, phase-locked to the animator's 2.2 Hz rummage so the loop stays in rhythm:
        // 12 cycles, with the loop splice at 6.
        public const float RummageHz = 2.2f;
        const int RummageCycles = 12;

        public static float[] Rummage(Random rng)
        {
            var seconds = RummageCycles / RummageHz;
            var b = Buffer(seconds);
            var bed = Buffer(seconds);
            White(bed, rng);
            BandPass(bed, 2500f, 0.5f);
            var wobble = Drift(rng, 3f, seconds);
            Shape(bed, x => (0.6f + 0.4f * (float)Math.Cos(2.0 * Math.PI * RummageHz * x)) * (0.7f + 0.3f * wobble(x)));
            Mix(b, bed, 0f, 0.08f);
            var t = 0f;
            while (true)
            {
                t += R(rng, 0.03f, 0.14f);
                if (t > seconds - 0.05f) break;
                if (rng.NextDouble() > 0.55 + 0.45 * Math.Cos(2.0 * Math.PI * RummageHz * t)) continue;
                var pick = rng.NextDouble();
                if (pick < 0.45)
                    Filtered(b, Burst(rng, R(rng, 0.01f, 0.04f), R(rng, 0.08f, 0.25f)), t, R(rng, 0.2f, 0.5f), f => BandPass(f, R(rng, 1500f, 4000f), 0.6f));
                else if (pick < 0.65)
                    Filtered(b, Grains(rng, R(rng, 0.1f, 0.25f), 25, 2000f, 8000f), t, R(rng, 0.3f, 0.6f), f => HighPass(f, 1500f));
                else if (pick < 0.85)
                    Knock(b, rng, t, R(rng, 600f, 1400f), R(rng, 0.25f, 0.45f));
                else
                    Clink(b, rng, t, R(rng, 0.15f, 0.3f));
            }
            HighPass(b, 120f);
            Loopable(b, 0.25f);
            Normalize(b, 0.7f);
            return b;
        }

        // Stuffed into the pack: a rustle and the item knocking against the others.
        public static float[] Pickup(Random rng)
        {
            var b = Buffer(0.4f);
            Filtered(b, Burst(rng, 0.02f, 0.15f), 0f, 0.4f, f => BandPass(f, R(rng, 2000f, 3000f), 0.6f));
            Knock(b, rng, R(rng, 0.06f, 0.1f), R(rng, 500f, 1100f), 0.6f);
            HighPass(b, 80f);
            Normalize(b, 0.8f);
            return b;
        }

        // Four chews of something crunchy, then a swallow.
        public static float[] Eat(Random rng)
        {
            var b = Buffer(1.4f);
            var t = 0.08f;
            for (var chew = 0; chew < 4; chew++)
            {
                Filtered(b, Grains(rng, 0.06f, 30, 800f, 4000f), t, R(rng, 0.4f, 0.6f), f => LowPass(f, 3500f));
                Filtered(b, Burst(rng, 0.003f, 0.04f), t + 0.01f, 0.3f, f => BandPass(f, R(rng, 1200f, 1800f), 3f));
                Ring(b, t, R(rng, 110f, 130f), 0.04f, 0.3f);
                t += R(rng, 0.22f, 0.28f);
            }
            Swallow(b, rng, t + 0.05f, 0.7f);
            HighPass(b, 60f);
            Normalize(b, 0.75f);
            return b;
        }

        // Three gulps and a short sigh.
        public static float[] Drink(Random rng)
        {
            var b = Buffer(1.5f);
            var t = 0.12f;
            for (var gulp = 0; gulp < 3; gulp++)
            {
                Swallow(b, rng, t, R(rng, 0.7f, 1f));
                t += R(rng, 0.35f, 0.42f);
            }
            var sigh = BodyRecipes.Breath(rng, 0.3f, 700f, 1200f, 500f);
            Envelope(sigh, 0.03f, 0.05f, 0.22f);
            Mix(b, sigh, t, 0.25f);
            HighPass(b, 60f);
            Normalize(b, 0.75f);
            return b;
        }

        // A strip torn off, then wrapped around the limb.
        public static float[] Bandage(Random rng)
        {
            var b = Buffer(1.3f);
            Filtered(b, Grains(rng, 0.35f, 160, 1500f, 6000f, t => 0.3f + 0.7f * t / 0.35f), 0.02f, 0.9f, f => HighPass(f, 1200f));
            var tear = Burst(rng, 0.3f, 0.06f);
            BandPass(tear, 3000f, 1f);
            Mix(b, tear, 0.02f, 0.3f);
            foreach (var at in new[] { R(rng, 0.45f, 0.55f), R(rng, 0.75f, 0.85f), R(rng, 1.0f, 1.08f) })
                Filtered(b, Burst(rng, 0.05f, 0.2f), at, R(rng, 0.25f, 0.4f), f => BandPass(f, R(rng, 2000f, 3500f), 0.7f));
            HighPass(b, 150f);
            Normalize(b, 0.7f);
            return b;
        }

        // An item hitting the ground, then a small bounce.
        public static float[] Drop(Random rng)
        {
            var b = Buffer(0.5f);
            Impact(b, rng, 0.003f, 1f);
            Impact(b, rng, R(rng, 0.1f, 0.15f), R(rng, 0.25f, 0.4f));
            HighPass(b, 50f);
            Normalize(b, 0.85f);
            return b;
        }

        // Plastic slide switch: the click of the contact, then the release.
        public static float[] FlashlightClick(Random rng)
        {
            var b = Buffer(0.12f);
            var hz = R(rng, 2200f, 3200f);
            Click(b, rng, 0.004f, hz, 0.9f);
            Click(b, rng, R(rng, 0.018f, 0.03f), hz * 1.1f, 0.5f);
            HighPass(b, 200f);
            Normalize(b, 0.8f);
            return b;
        }

        // Sitting on a sprung mattress: the body settling, springs, sheets.
        public static float[] Bed(Random rng)
        {
            var b = Buffer(1.2f);
            Filtered(b, Burst(rng, 0.01f, 0.15f), 0.02f, 0.7f, f => LowPass(f, 300f));
            Ring(b, 0.02f, R(rng, 65f, 75f), 0.15f, 0.6f, 0.5f);
            for (var spring = 0; spring < 3; spring++)
            {
                var at = R(rng, 0.05f, 0.5f);
                Creak(b, rng, at, R(rng, 0.1f, 0.2f), 0.15f);
                Ring(b, at, R(rng, 250f, 400f), 0.2f, 0.08f, -0.05f);
            }
            for (var rustle = 0; rustle < 3; rustle++)
                Filtered(b, Burst(rng, 0.04f, 0.2f), R(rng, 0.1f, 0.8f), 0.25f, f => BandPass(f, R(rng, 1800f, 3200f), 0.6f));
            HighPass(b, 40f);
            Normalize(b, 0.8f);
            return b;
        }

        // Door handle and bolt: two metallic clicks.
        public static float[] DoorLatch(Random rng)
        {
            var b = Buffer(0.18f);
            Latch(b, rng, 0.003f, 1f);
            HighPass(b, 150f);
            Normalize(b, 0.8f);
            return b;
        }

        public static float[] DoorCreak(Random rng)
        {
            var seconds = R(rng, 0.4f, 0.7f);
            var b = Buffer(seconds + 0.05f);
            Creak(b, rng, 0.01f, seconds, 1f);
            HighPass(b, 200f);
            Normalize(b, rng.NextDouble() < 0.35 ? 0.4f : 0.7f);
            return b;
        }

        // A door closing into its frame: the panel's modes, the frame's thud, then the latch catching.
        public static float[] DoorShut(Random rng) => Shut(rng, false);

        public static float[] DoorSlam(Random rng) => Shut(rng, true);

        // A forced door: the frame splinters as the latch tears out.
        public static float[] DoorBurst(Random rng)
        {
            var b = Buffer(0.9f);
            Mix(b, Grains(rng, 0.08f, 120, 800f, 6000f), 0.003f, 1f);
            Filtered(b, Burst(rng, 0.001f, 0.12f), 0.003f, 0.8f, f => BandPass(f, 1500f, 0.7f));
            Ring(b, 0.003f, R(rng, 65f, 75f), 0.25f, 0.8f, 1f);
            Filtered(b, Grains(rng, 0.4f, 25, 1000f, 5000f, t => 1f - t / 0.4f), 0.1f, 0.5f, f => HighPass(f, 800f));
            Saturate(b, 1.5f);
            HighPass(b, 40f);
            Normalize(b, 0.9f);
            return b;
        }

        static float[] Shut(Random rng, bool slam)
        {
            var b = Buffer(slam ? 0.8f : 0.6f);
            var panel = R(rng, 170f, 210f);
            var g = slam ? 1f : 0.7f;
            Ring(b, 0.003f, R(rng, 80f, 100f), slam ? 0.28f : 0.2f, g, 0.6f);
            Ring(b, 0.003f, panel, 0.15f, g * 0.5f);
            Ring(b, 0.003f, panel * R(rng, 2.2f, 2.4f), 0.1f, g * 0.3f);
            Ring(b, 0.003f, panel * R(rng, 3.7f, 3.9f), 0.07f, g * 0.2f);
            Filtered(b, Burst(rng, 0.001f, 0.05f), 0.003f, g * 0.6f, f => LowPass(f, 1500f));
            Filtered(b, Burst(rng, 0.0005f, 0.03f), 0.003f, g * 0.5f, f => BandPass(f, R(rng, 900f, 1200f), 1f));
            if (slam)
            {
                Filtered(b, Burst(rng, 0.0005f, 0.08f), 0.003f, 0.5f, f => HighPass(f, 800f));
                Filtered(b, Grains(rng, 0.25f, 30, 1500f, 5000f, t => 1f - t / 0.25f), 0.02f, 0.4f, f => HighPass(f, 1200f));
            }
            Latch(b, rng, R(rng, 0.02f, 0.035f), slam ? 0.5f : 0.6f);
            if (slam) Saturate(b, 1.5f);
            HighPass(b, 40f);
            Normalize(b, 0.9f);
            return b;
        }

        static void Latch(float[] b, Random rng, float at, float gain)
        {
            var hz = R(rng, 2500f, 3800f);
            Filtered(b, Burst(rng, 0.0002f, 0.004f), at, gain * 0.7f, f => HighPass(f, 2000f));
            Ring(b, at, hz, 0.04f, gain * 0.35f);
            Ring(b, at, hz * 1.6f, 0.03f, gain * 0.2f);
            Filtered(b, Burst(rng, 0.005f, 0.03f), at, gain * 0.2f, f => BandPass(f, 4000f, 2f));
            var release = at + R(rng, 0.03f, 0.05f);
            Filtered(b, Burst(rng, 0.0002f, 0.004f), release, gain * 0.35f, f => HighPass(f, 2000f));
            Ring(b, release, hz * 1.1f, 0.03f, gain * 0.18f);
        }

        // A small solid object struck: wood or plastic body modes plus a click.
        static void Knock(float[] b, Random rng, float at, float hz, float gain)
        {
            Ring(b, at, hz, R(rng, 0.04f, 0.09f), gain * 0.6f);
            Ring(b, at, hz * R(rng, 2.5f, 2.9f), 0.04f, gain * 0.3f);
            Filtered(b, Burst(rng, 0.0002f, 0.004f), at, gain * 0.4f, f => HighPass(f, 2000f));
        }

        // Tins knocking together: high, longer and inharmonic.
        static void Clink(float[] b, Random rng, float at, float gain)
        {
            var hz = R(rng, 1800f, 3500f);
            Ring(b, at, hz, 0.12f, gain);
            Ring(b, at, hz * 1.47f, 0.08f, gain * 0.6f);
            Ring(b, at, hz * 2.09f, 0.05f, gain * 0.3f);
            Filtered(b, Burst(rng, 0.0002f, 0.003f), at, gain * 0.5f, f => HighPass(f, 3000f));
        }

        static void Click(float[] b, Random rng, float at, float hz, float gain)
        {
            Filtered(b, Burst(rng, 0.0001f, 0.003f), at, gain, f => HighPass(f, 2500f));
            Ring(b, at, hz, 0.012f, gain * 0.45f);
            Ring(b, at, R(rng, 500f, 700f), 0.02f, gain * 0.25f);
        }

        static void Impact(float[] b, Random rng, float at, float gain)
        {
            Ring(b, at, R(rng, 140f, 220f), 0.08f, gain * 0.7f, 0.5f);
            Filtered(b, Burst(rng, 0.0005f, 0.03f), at, gain * 0.6f, f => LowPass(f, 900f));
            Ring(b, at, R(rng, 900f, 1600f), 0.05f, gain * 0.25f);
            Filtered(b, Burst(rng, 0.0002f, 0.004f), at, gain * 0.3f, f => HighPass(f, 2500f));
        }

        // Throat thump rising in pitch, a bubble, the liquid.
        static void Swallow(float[] b, Random rng, float at, float gain)
        {
            Ring(b, at, R(rng, 130f, 170f), 0.08f, gain * 0.6f, -0.3f);
            var bubble = R(rng, 400f, 600f);
            var blip = Buffer(0.07f);
            Tone(blip, t => bubble * (1f + 1.2f * t / 0.07f), Wave.Sine);
            Envelope(blip, 0.003f, 0.01f, 0.06f);
            Mix(b, blip, at + 0.02f, gain * 0.4f);
            Filtered(b, Burst(rng, 0.005f, 0.06f), at, gain * 0.3f, f => BandPass(f, 700f, 2f));
        }
    }
}
