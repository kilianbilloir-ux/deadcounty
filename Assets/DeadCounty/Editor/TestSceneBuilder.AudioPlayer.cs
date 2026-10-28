using System;
using System.Collections.Generic;
using System.Linq;
using DeadCounty.Audio;
using DeadCounty.Player;
using UnityEditor;
using UnityEngine;
using Catalog = DeadCounty.EditorTools.PlayerSoundCatalog;

namespace DeadCounty.EditorTools
{
    public static partial class TestSceneBuilder
    {
        static readonly Dictionary<string, SoundCue> PlayerCues = new();

        static partial void EnsurePlayerSounds()
        {
            if (!Catalog.Surfaces.SequenceEqual(Enum.GetNames(typeof(Surface))))
                throw new InvalidOperationException("PlayerSoundCatalog.Surfaces must list the Surface enum in order");
            PlayerCues.Clear();
            foreach (var e in Catalog.Entries())
                PlayerCues[e.Name] = CueAsset(e.Name, SoundChannel.Effects, e.Variants, e.Render, e.Volume, e.PitchJitter, e.MinDistance, e.MaxDistance);
        }

        static SoundCue PlayerCue(string name) => PlayerCues.TryGetValue(name, out var cue)
            ? cue
            : throw new InvalidOperationException($"Sound cue {name} missing: EnsureSounds must run before the player and the doors are built");

        // The player's audio components; cues already assigned (Inspector tweaks) are kept. True when anything changed.
        static bool EnsurePlayerAudio(GameObject rig)
        {
            var player = rig.GetComponentInChildren<ThirdPersonController>(true).gameObject;
            var changed = AddMissing<PlayerFootstepAudio>(player);
            changed |= AddMissing<PlayerBodyAudio>(player);
            changed |= AddMissing<PlayerActionAudio>(player);

            var steps = new SerializedObject(player.GetComponent<PlayerFootstepAudio>());
            changed |= AssignCues(steps.FindProperty("steps"), Catalog.Step);
            changed |= AssignCues(steps.FindProperty("runSteps"), Catalog.Run);
            changed |= AssignCues(steps, ("jump", Catalog.Jump), ("land", Catalog.Land));
            changed |= AssignCues(new SerializedObject(player.GetComponent<PlayerBodyAudio>()),
                ("breathIn", Catalog.BreathIn), ("breathOut", Catalog.BreathOut), ("pantOut", Catalog.PantOut),
                ("hurt", Catalog.Hurt), ("death", Catalog.Death), ("heartbeat", Catalog.Heartbeat));
            changed |= AssignCues(new SerializedObject(player.GetComponent<PlayerActionAudio>()),
                ("rummage", Catalog.Rummage), ("pickup", Catalog.Pickup), ("eat", Catalog.Eat), ("drink", Catalog.Drink),
                ("bandage", Catalog.Bandage), ("drop", Catalog.Drop), ("flashlightClick", Catalog.Flashlight), ("bed", Catalog.Bed));
            return changed;
        }

        // Doors are rebuilt with the scene, so their cues are always assigned.
        static void AddDoorAudio(GameObject hinge)
        {
            AssignCues(new SerializedObject(hinge.AddComponent<DoorAudio>()),
                ("latch", Catalog.DoorLatch), ("creak", Catalog.DoorCreak), ("shut", Catalog.DoorShut),
                ("slam", Catalog.DoorSlam), ("burst", Catalog.DoorBurst));
        }

        static bool AssignCues(SerializedObject so, params (string Field, string Cue)[] cues)
        {
            var changed = false;
            foreach (var (field, cue) in cues)
            {
                var property = so.FindProperty(field) ?? throw new InvalidOperationException($"{so.targetObject.GetType().Name} has no field {field}");
                if (property.objectReferenceValue != null) continue;
                property.objectReferenceValue = PlayerCue(cue);
                changed = true;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }

        // One cue per Surface, in the enum's order.
        static bool AssignCues(SerializedProperty array, Func<int, string> cueOf)
        {
            var count = Enum.GetValues(typeof(Surface)).Length;
            var changed = array.arraySize != count;
            array.arraySize = count;
            for (var i = 0; i < count; i++)
            {
                var element = array.GetArrayElementAtIndex(i);
                if (element.objectReferenceValue != null) continue;
                element.objectReferenceValue = PlayerCue(cueOf(i));
                changed = true;
            }
            array.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }
    }
}
