using System;

namespace DeadCounty.EditorTools
{
    // The player's and the doors' cues: name, variant count, recipe and playback settings. Kept free of Unity types
    // so the same list can be rendered and analysed outside the editor.
    public static class PlayerSoundCatalog
    {
        public readonly struct Entry
        {
            public readonly string Name;
            public readonly int Variants;
            public readonly Func<Random, float[]> Render;
            public readonly float Volume, PitchJitter, MinDistance, MaxDistance;

            public Entry(string name, int variants, Func<Random, float[]> render, float volume = 1f, float pitchJitter = 0.05f,
                float minDistance = 1f, float maxDistance = 25f)
            {
                Name = name;
                Variants = variants;
                Render = render;
                Volume = volume;
                PitchJitter = pitchJitter;
                MinDistance = minDistance;
                MaxDistance = maxDistance;
            }
        }

        // Indexed like DeadCounty.Audio.Surface.
        public static readonly string[] Surfaces = { "Grass", "Dirt", "Gravel", "Asphalt", "Concrete", "Wood", "Metal" };
        static readonly Func<Random, bool, float[]>[] StepRecipesBySurface =
        {
            StepRecipes.Grass, StepRecipes.Dirt, StepRecipes.Gravel, StepRecipes.Asphalt, StepRecipes.Concrete, StepRecipes.Wood, StepRecipes.Metal,
        };

        public static string Step(int surface) => $"Player/Step_{Surfaces[surface]}";
        public static string Run(int surface) => $"Player/Run_{Surfaces[surface]}";

        public const string Jump = "Player/Jump";
        public const string Land = "Player/Land";
        public const string BreathIn = "Player/Breath_In";
        public const string BreathOut = "Player/Breath_Out";
        public const string PantOut = "Player/Pant_Out";
        public const string Hurt = "Player/Hurt";
        public const string Death = "Player/Death";
        public const string Heartbeat = "Player/Heartbeat";
        public const string Rummage = "Player/Rummage";
        public const string Pickup = "Player/Pickup";
        public const string Eat = "Player/Eat";
        public const string Drink = "Player/Drink";
        public const string Bandage = "Player/Bandage";
        public const string Drop = "Player/Drop";
        public const string Flashlight = "Player/Flashlight";
        public const string Bed = "Player/Bed";
        public const string DoorLatch = "World/Door_Latch";
        public const string DoorCreak = "World/Door_Creak";
        public const string DoorShut = "World/Door_Shut";
        public const string DoorSlam = "World/Door_Slam";
        public const string DoorBurst = "World/Door_Burst";

        public static Entry[] Entries()
        {
            var entries = new System.Collections.Generic.List<Entry>();
            for (var s = 0; s < Surfaces.Length; s++)
            {
                var recipe = StepRecipesBySurface[s];
                entries.Add(new Entry(Step(s), 5, rng => recipe(rng, false), 0.8f, 0.06f, 1f, 20f));
                entries.Add(new Entry(Run(s), 5, rng => recipe(rng, true), 0.9f, 0.06f, 1f, 25f));
            }
            entries.Add(new Entry(Jump, 3, BodyRecipes.Jump, 0.6f, 0.06f, 1f, 15f));
            entries.Add(new Entry(Land, 4, BodyRecipes.Land, 0.8f, 0.06f, 1f, 20f));
            entries.Add(new Entry(BreathIn, 5, BodyRecipes.BreathIn, 0.5f, 0.04f));
            entries.Add(new Entry(BreathOut, 5, rng => BodyRecipes.BreathOut(rng, false), 0.55f, 0.04f));
            entries.Add(new Entry(PantOut, 4, rng => BodyRecipes.BreathOut(rng, true), 0.6f, 0.04f));
            entries.Add(new Entry(Hurt, 5, BodyRecipes.Hurt, 0.9f, 0.04f, 1f, 20f));
            entries.Add(new Entry(Death, 2, BodyRecipes.Death, 1f, 0.02f, 1f, 25f));
            entries.Add(new Entry(Heartbeat, 2, BodyRecipes.Heartbeat, 0.8f, 0.02f));
            entries.Add(new Entry(Rummage, 1, ActionRecipes.Rummage, 0.6f, 0f, 1f, 15f));
            entries.Add(new Entry(Pickup, 4, ActionRecipes.Pickup, 0.6f, 0.06f, 1f, 12f));
            entries.Add(new Entry(Eat, 2, ActionRecipes.Eat, 0.6f, 0.04f, 1f, 10f));
            entries.Add(new Entry(Drink, 2, ActionRecipes.Drink, 0.6f, 0.04f, 1f, 10f));
            entries.Add(new Entry(Bandage, 2, ActionRecipes.Bandage, 0.6f, 0.04f, 1f, 10f));
            entries.Add(new Entry(Drop, 4, ActionRecipes.Drop, 0.7f, 0.08f, 1f, 15f));
            entries.Add(new Entry(Flashlight, 3, ActionRecipes.FlashlightClick, 0.45f, 0.03f, 1f, 8f));
            entries.Add(new Entry(Bed, 2, ActionRecipes.Bed, 0.6f, 0.04f, 1f, 12f));
            entries.Add(new Entry(DoorLatch, 4, ActionRecipes.DoorLatch, 0.6f, 0.05f, 1f, 15f));
            entries.Add(new Entry(DoorCreak, 5, ActionRecipes.DoorCreak, 0.45f, 0.08f, 1f, 20f));
            entries.Add(new Entry(DoorShut, 4, ActionRecipes.DoorShut, 0.75f, 0.05f, 1.5f, 25f));
            entries.Add(new Entry(DoorSlam, 3, ActionRecipes.DoorSlam, 1f, 0.05f, 2f, 40f));
            entries.Add(new Entry(DoorBurst, 2, ActionRecipes.DoorBurst, 1f, 0.04f, 2f, 40f));
            return entries.ToArray();
        }
    }
}
