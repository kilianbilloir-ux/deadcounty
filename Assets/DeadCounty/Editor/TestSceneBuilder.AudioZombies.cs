using System;
using DeadCounty.Audio;
using UnityEditor;
using UnityEngine;

namespace DeadCounty.EditorTools
{
    public static partial class TestSceneBuilder
    {
        const string ZombieAudioDir = AudioDir + "/Zombie";

        // Voices of the throat cues, each recorded in its own takes (scripts/audio/generate_sounds.py): a zombie keeps
        // to one, so a crowd does not sound like one zombie at different pitches.
        const int ZombieVoices = 4;

        // ZombieAudio field, cue name, variants, recipe, volume, pitch jitter, min and max distance (m), voices. Voices
        // reach a little past the zombies' own hearing; the call carries farthest, feet stay close.
        static readonly (string field, string name, int variants, Func<System.Random, float[]> render, float volume, float jitter, float min, float max, int voices)[] ZombieCueSpecs =
        {
            ("moan", "Moan", 8, ZombieVoiceRecipes.Moan, 0.7f, 0.04f, 2f, 28f, ZombieVoices),
            ("breath", "Breath", 8, ZombieVoiceRecipes.Breath, 0.45f, 0.05f, 1f, 12f, ZombieVoices),
            ("alert", "Alert", 8, ZombieVoiceRecipes.Alert, 0.85f, 0.04f, 2f, 30f, ZombieVoices),
            ("sniff", "Sniff", 3, ZombieVoiceRecipes.Sniff, 0.55f, 0.05f, 1f, 15f, 1),
            ("growl", "Growl", 8, ZombieVoiceRecipes.Growl, 0.8f, 0.05f, 2f, 30f, ZombieVoices),
            ("call", "Call", 8, ZombieVoiceRecipes.Call, 1f, 0.03f, 3f, 40f, ZombieVoices),
            ("windup", "Windup", 8, ZombieVoiceRecipes.Windup, 1f, 0.02f, 2f, 30f, ZombieVoices),
            ("frustrated", "Frustrated", 8, ZombieVoiceRecipes.Frustrated, 0.8f, 0.04f, 2f, 28f, ZombieVoices),
            ("rise", "Rise", 8, ZombieVoiceRecipes.Frustrated, 0.75f, 0.05f, 2f, 22f, ZombieVoices),
            ("hurt", "Hurt", 8, ZombieVoiceRecipes.Frustrated, 0.8f, 0.06f, 2f, 25f, ZombieVoices),
            ("death", "Death", 8, ZombieVoiceRecipes.Moan, 0.9f, 0.05f, 2f, 28f, ZombieVoices),
            ("fall", "Fall", 3, BodyRecipes.Land, 0.8f, 0.08f, 1f, 18f, 1),
            ("whoosh", "Whoosh", 3, ZombieBodyRecipes.Whoosh, 0.7f, 0.08f, 1f, 12f, 1),
            ("hit", "Hit", 3, ZombieBodyRecipes.Hit, 1f, 0.06f, 1f, 15f, 1),
            ("miss", "Miss", 3, ZombieBodyRecipes.Miss, 0.8f, 0.06f, 1f, 12f, 1),
            ("doorBang", "DoorBang", 4, ZombieBodyRecipes.DoorBang, 0.9f, 0.08f, 2f, 30f, 1),
            ("stepSoft", "StepSoft", 4, ZombieBodyRecipes.StepSoft, 0.35f, 0.08f, 1f, 12f, 1),
            ("stepHard", "StepHard", 4, ZombieBodyRecipes.StepHard, 0.35f, 0.08f, 1f, 12f, 1),
            ("stepGravel", "StepGravel", 4, ZombieBodyRecipes.StepGravel, 0.35f, 0.08f, 1f, 12f, 1),
            ("dragSoft", "DragSoft", 3, ZombieBodyRecipes.DragSoft, 0.3f, 0.08f, 1f, 10f, 1),
            ("dragHard", "DragHard", 3, ZombieBodyRecipes.DragHard, 0.3f, 0.08f, 1f, 10f, 1),
            ("feed", "Feed", 4, ActionRecipes.Eat, 0.7f, 0.08f, 1f, 15f, 1),
        };

        static partial void EnsureZombieSounds()
        {
            foreach (var (_, name, variants, render, volume, jitter, min, max, voices) in ZombieCueSpecs)
                CueAsset($"Zombie/{name}", SoundChannel.Effects, variants, render, volume, jitter, min, max, voices);
        }

        // The prefab is never regenerated: its ZombieAudio is added and wired in place, saved only on a change.
        // Runs after EnsureSounds, on a new prefab as on an old one, before the scene instances are made.
        static GameObject EnsureZombieAudio(GameObject prefab)
        {
            var root = PrefabUtility.LoadPrefabContents(ZombiePrefabPath);
            try
            {
                var changed = false;
                if (!root.TryGetComponent<ZombieAudio>(out var audio))
                {
                    audio = root.AddComponent<ZombieAudio>();
                    changed = true;
                }
                var source = root.GetComponent<AudioSource>();
                if (source.playOnAwake)
                {
                    source.playOnAwake = false;
                    changed = true;
                }
                var so = new SerializedObject(audio);
                foreach (var (field, name, _, _, _, _, _, _, _) in ZombieCueSpecs)
                {
                    var path = $"{ZombieAudioDir}/SC_{name}.asset";
                    var cue = AssetDatabase.LoadAssetAtPath<SoundCue>(path) ?? throw new InvalidOperationException($"{path} is missing: EnsureSounds must run before the zombies are built");
                    var property = so.FindProperty(field) ?? throw new InvalidOperationException($"ZombieAudio has no field {field}");
                    if (property.objectReferenceValue == cue) continue;
                    property.objectReferenceValue = cue;
                    changed = true;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                return changed ? PrefabUtility.SaveAsPrefabAsset(root, ZombiePrefabPath) : prefab;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
