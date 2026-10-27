using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeadCounty.Player
{
    // Keyboard keys the player can change from the options (GDD 8.11): one binding per row, the first of the
    // keyboard group, so W A S D move while the arrows stay. A key taken from another row swaps with it, so no
    // action is left without a key. Overrides are kept in PlayerPrefs and loaded before the first scene; the
    // prompts read the bindings, so they follow.
    public static class KeyBindings
    {
        const string PrefsKey = "dc.bindings";
        static readonly InputBinding KeyboardMask = InputBinding.MaskByGroup("Keyboard&Mouse");

        public readonly struct Row
        {
            public readonly string Label;
            public readonly string Action;
            // Composite part (up, down, left, right) for Move, null otherwise.
            public readonly string Part;

            public Row(string label, string action, string part = null)
            {
                Label = label;
                Action = action;
                Part = part;
            }
        }

        public static readonly IReadOnlyList<Row> Rows = new Row[]
        {
            new("Avancer", "Player/Move", "up"),
            new("Reculer", "Player/Move", "down"),
            new("Aller à gauche", "Player/Move", "left"),
            new("Aller à droite", "Player/Move", "right"),
            new("Courir", "Player/Run"),
            new("Sprinter", "Player/Sprint"),
            new("S'accroupir", "Player/Crouch"),
            new("Sauter", "Player/Jump"),
            new("Interagir", "Player/Interact"),
            new("Lampe torche", "Player/Flashlight"),
            new("Inventaire", "Player/Inventory"),
            new("Jeter un objet", "Player/Drop"),
            new("Changer d'épaule", "Player/SwitchShoulder"),
        };

        static InputActionRebindingExtensions.RebindingOperation operation;
        static int endFrame = -1;

        // Waiting for a key; menus ignore their input meanwhile.
        public static bool Rebinding => operation != null;
        // The key that ended the wait was pressed this frame: menus must not read it too.
        public static bool EndedThisFrame => endFrame == Time.frameCount;

        public static string Key(Row row)
        {
            var (action, index) = Find(row);
            return InputPrompts.Key(action, index);
        }

        // Waits for a key, Escape cancels; done runs in both cases.
        public static void Rebind(Row row, Action done)
        {
            if (operation != null) throw new InvalidOperationException("A key is already being rebound");
            var (action, index) = Find(row);
            var previous = action.bindings[index].effectivePath;
            // An enabled action cannot be rebound.
            action.Disable();
            operation = action.PerformInteractiveRebinding(index)
                // A key press has a single candidate: no need to wait the default 50 ms for a better one.
                .OnMatchWaitForAnother(0f)
                .WithControlsHavingToMatchPath("<Keyboard>")
                .WithControlsExcluding("<Keyboard>/anyKey")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnComplete(_ =>
                {
                    Finish(action);
                    SwapWithTakenKey(action, index, previous);
                    Save();
                    done();
                })
                .OnCancel(_ =>
                {
                    Finish(action);
                    done();
                })
                .Start();
        }

        public static void Cancel() => operation?.Cancel();

        public static void ResetAll()
        {
            InputSystem.actions.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(PrefsKey);
            InputPrompts.RaiseKeysChanged();
        }

        static void Finish(InputAction action)
        {
            operation.Dispose();
            operation = null;
            endFrame = Time.frameCount;
            action.Enable();
        }

        // The row that had the new key takes the old one.
        static void SwapWithTakenKey(InputAction changed, int changedIndex, string previous)
        {
            var path = changed.bindings[changedIndex].effectivePath;
            foreach (var row in Rows)
            {
                var (action, index) = Find(row);
                if (action == changed && index == changedIndex) continue;
                if (action.bindings[index].effectivePath == path) action.ApplyBindingOverride(index, previous);
            }
        }

        static void Save()
        {
            PlayerPrefs.SetString(PrefsKey, InputSystem.actions.SaveBindingOverridesAsJson());
            InputPrompts.RaiseKeysChanged();
        }

        static (InputAction action, int index) Find(Row row)
        {
            var action = InputSystem.actions.FindAction(row.Action, true);
            var bindings = action.bindings;
            for (var i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding.isComposite || binding.isPartOfComposite != (row.Part != null)) continue;
                if (row.Part != null && !string.Equals(binding.name, row.Part, StringComparison.OrdinalIgnoreCase)) continue;
                if (KeyboardMask.Matches(binding)) return (action, i);
            }
            throw new InvalidOperationException($"{row.Action} has no keyboard binding{(row.Part != null ? $" for {row.Part}" : "")}");
        }

        // Domain reload is disabled on entering Play mode and the project-wide actions keep the last session's
        // overrides: start again from the saved ones.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Load()
        {
            var actions = InputSystem.actions;
            actions.RemoveAllBindingOverrides();
            var json = PlayerPrefs.GetString(PrefsKey, "");
            if (json.Length > 0) actions.LoadBindingOverridesFromJson(json);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            operation?.Dispose();
            operation = null;
            endFrame = -1;
        }
    }
}
