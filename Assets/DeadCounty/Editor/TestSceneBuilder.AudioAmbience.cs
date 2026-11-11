using System;
using DeadCounty.Audio;
using DeadCounty.UI;
using UnityEditor;
using UnityEngine;
using Activity = DeadCounty.Audio.AmbienceDirector.Activity;
using Object = UnityEngine.Object;

namespace DeadCounty.EditorTools
{
    // The soundscape, the title music and the interface sounds: their cues (AmbienceRecipes, DistantRecipes,
    // InterfaceRecipes), the AmbienceDirector of the scene, the TitleMusic on the title and the rig's UiSounds.
    public static partial class TestSceneBuilder
    {
        // Calls are placed nearer than their cue's minDistance, so the rolloff never touches them: their distance is
        // in the clip. Distant events average one every five minutes between the three of them.
        static readonly (string cue, Activity activity, float interval, Vector2 distance, Vector2 height, float quietStart)[] AmbienceCalls =
        {
            ("Ambience/Bird", Activity.Birds, 8f, new(12f, 35f), new(3f, 9f), 2f),
            ("Ambience/Crow", Activity.Day, 50f, new(30f, 55f), new(6f, 14f), 20f),
            ("Ambience/Owl", Activity.Night, 45f, new(25f, 50f), new(5f, 10f), 10f),
            ("Ambience/Dog", Activity.MostlyNight, 90f, new(40f, 60f), new(0f, 2f), 30f),
            ("Ambience/DistantScream", Activity.Always, 900f, new(40f, 60f), new(0f, 4f), 120f),
            ("Ambience/DistantAlarm", Activity.Always, 1000f, new(40f, 60f), new(0f, 2f), 150f),
            ("Ambience/DistantShot", Activity.Always, 700f, new(40f, 60f), new(0f, 3f), 90f),
        };

        static readonly (string field, string cue)[] InterfaceCues =
        {
            ("move", "Interface/Move"), ("confirm", "Interface/Confirm"), ("back", "Interface/Back"),
            ("step", "Interface/Step"), ("toggle", "Interface/Toggle"), ("menuOpen", "Interface/Open"),
            ("menuClose", "Interface/Close"), ("bagOpen", "Interface/BagOpen"), ("bagClose", "Interface/BagClose"),
        };

        static partial void EnsureAmbienceSounds()
        {
            const SoundChannel ambience = SoundChannel.Ambience;
            CueAsset("Ambience/Wind", ambience, 1, AmbienceRecipes.Wind, 0.4f, 0f);
            CueAsset("Ambience/Night", ambience, 1, AmbienceRecipes.Night, 0.3f, 0f);
            CueAsset("Ambience/RoomTone", ambience, 1, AmbienceRecipes.RoomTone, 0.3f, 0f);
            CueAsset("Ambience/Bird", ambience, 6, Variants(AmbienceRecipes.Bird), 0.35f, 0.06f, 60f, 300f);
            CueAsset("Ambience/Crow", ambience, 2, AmbienceRecipes.Crow, 0.3f, 0.05f, 80f, 300f);
            CueAsset("Ambience/Owl", ambience, 2, Variants(AmbienceRecipes.Owl), 0.35f, 0.03f, 80f, 300f);
            CueAsset("Ambience/Dog", ambience, 2, Variants(AmbienceRecipes.Dog), 0.3f, 0.05f, 150f, 400f);
            CueAsset("Ambience/DistantScream", ambience, 2, Variants(DistantRecipes.Scream), 0.3f, 0.04f, 200f, 500f);
            CueAsset("Ambience/DistantAlarm", ambience, 2, Variants(DistantRecipes.CarAlarm), 0.22f, 0.02f, 200f, 500f);
            CueAsset("Ambience/DistantShot", ambience, 2, Variants(DistantRecipes.Gunshot), 0.4f, 0.04f, 200f, 500f);

            // Kept in tune: TitleMusic pitches the bell to the drone's notes itself.
            CueAsset("Music/TitleDrone", SoundChannel.Music, 1, InterfaceRecipes.TitleDrone, 0.5f, 0f);
            CueAsset("Music/TitleToll", SoundChannel.Music, 1, InterfaceRecipes.Toll, 0.35f, 0f);

            const SoundChannel ui = SoundChannel.Interface;
            CueAsset("Interface/Move", ui, 3, InterfaceRecipes.Move, 0.22f, 0.04f);
            CueAsset("Interface/Step", ui, 2, InterfaceRecipes.Step, 0.25f, 0.03f);
            CueAsset("Interface/Confirm", ui, 2, InterfaceRecipes.Confirm, 0.4f, 0.02f);
            CueAsset("Interface/Back", ui, 2, InterfaceRecipes.Back, 0.35f, 0.02f);
            CueAsset("Interface/Toggle", ui, 2, InterfaceRecipes.Toggle, 0.35f, 0.03f);
            CueAsset("Interface/Open", ui, 1, InterfaceRecipes.Open, 0.4f, 0.02f);
            CueAsset("Interface/Close", ui, 1, InterfaceRecipes.Close, 0.35f, 0.02f);
            CueAsset("Interface/BagOpen", ui, 2, rng => InterfaceRecipes.Bag(rng, true), 0.45f, 0.04f);
            CueAsset("Interface/BagClose", ui, 2, rng => InterfaceRecipes.Bag(rng, false), 0.4f, 0.04f);
        }

        // CueAsset renders the variants in order: hands each one its index (a species, a kind of call).
        static Func<System.Random, float[]> Variants(Func<System.Random, int, float[]> render)
        {
            var variant = 0;
            return rng => render(rng, variant++);
        }

        // A cue made by CueAsset, found by the same name.
        static SoundCue Cue(string name)
        {
            var slash = name.LastIndexOf('/');
            var path = $"{AudioDir}/{name.Substring(0, slash)}/SC_{name.Substring(slash + 1)}.asset";
            return AssetDatabase.LoadAssetAtPath<SoundCue>(path) ?? throw new InvalidOperationException($"No sound cue at {path}: run EnsureSounds first");
        }

        // After the title screen, which gets the title music.
        static void BuildSoundscape()
        {
            var director = new SerializedObject(new GameObject("Ambiance").AddComponent<AmbienceDirector>());
            director.FindProperty("wind").objectReferenceValue = Cue("Ambience/Wind");
            director.FindProperty("night").objectReferenceValue = Cue("Ambience/Night");
            director.FindProperty("roomTone").objectReferenceValue = Cue("Ambience/RoomTone");
            var calls = director.FindProperty("calls");
            calls.arraySize = AmbienceCalls.Length;
            for (var i = 0; i < AmbienceCalls.Length; i++)
            {
                var (cue, activity, interval, distance, height, quietStart) = AmbienceCalls[i];
                var call = calls.GetArrayElementAtIndex(i);
                call.FindPropertyRelative("cue").objectReferenceValue = Cue(cue);
                call.FindPropertyRelative("activity").enumValueIndex = (int)activity;
                call.FindPropertyRelative("interval").floatValue = interval;
                call.FindPropertyRelative("distance").vector2Value = distance;
                call.FindPropertyRelative("height").vector2Value = height;
                call.FindPropertyRelative("quietStart").floatValue = quietStart;
            }
            director.ApplyModifiedPropertiesWithoutUndo();

            var title = Object.FindAnyObjectByType<TitleScreen>() ?? throw new InvalidOperationException("BuildSoundscape runs after BuildTitleScreen");
            var music = new SerializedObject(title.gameObject.AddComponent<TitleMusic>());
            music.FindProperty("drone").objectReferenceValue = Cue("Music/TitleDrone");
            music.FindProperty("toll").objectReferenceValue = Cue("Music/TitleToll");
            music.ApplyModifiedPropertiesWithoutUndo();
        }

        // Interface sounds on the HUD's rig; cues already assigned (Inspector tweaks) are kept.
        static bool EnsureUiSounds(GameObject rig)
        {
            var added = AddMissing<UiSounds>(rig);
            var so = new SerializedObject(rig.GetComponent<UiSounds>());
            foreach (var (field, cue) in InterfaceCues)
            {
                var property = so.FindProperty(field);
                if (property.objectReferenceValue != null) continue;
                property.objectReferenceValue = Cue(cue);
                added = true;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return added;
        }
    }
}
