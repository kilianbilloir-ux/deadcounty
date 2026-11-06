using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace DeadCounty.Player
{
    // Read from the project-wide action bindings so prompts follow any rebinding; they show the device last pressed.
    public static class InputPrompts
    {
        const string KeyboardGroup = "Keyboard&Mouse";
        const string GamepadGroup = "Gamepad";
        const InputBinding.DisplayStringOptions Options = InputBinding.DisplayStringOptions.DontIncludeInteractions;

        static IDisposable listener;

        public static bool GamepadActive { get; private set; }

        // Device switch or rebinding (KeyBindings): prompts must be redrawn.
        public static event Action KeysChanged;

        // UI Submit and Cancel are generic bindings with no readable key name.
        public static string SubmitKey => GamepadActive ? "A" : "Entrée";
        public static string CancelKey => GamepadActive ? "B" : "Échap";

        // "E" on keyboard, "Y" on gamepad; null without project-wide actions.
        public static string Key(string actionPath)
        {
            var action = InputSystem.actions?.FindAction(actionPath);
            return action == null ? null : French(action.GetBindingDisplayString(Options, GamepadActive ? GamepadGroup : KeyboardGroup));
        }

        // The key of one binding, for the controls options.
        public static string Key(InputAction action, int bindingIndex) => French(action.GetBindingDisplayString(bindingIndex, Options));

        internal static void RaiseKeysChanged() => KeysChanged?.Invoke();

        // The Input System names keys in English.
        static string French(string key) => key switch
        {
            "Space" => "Espace",
            "Esc" or "Escape" => "Échap",
            "Enter" => "Entrée",
            "Shift" or "Left Shift" => "Maj",
            "Right Shift" => "Maj droite",
            "Backspace" => "Retour",
            "Delete" => "Suppr",
            "Left Alt" or "Alt" => "Alt",
            "Left Ctrl" or "Ctrl" => "Ctrl",
            _ => key,
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen() => listener = InputSystem.onAnyButtonPress.Call(control =>
        {
            var gamepad = control.device is Gamepad;
            if (gamepad == GamepadActive) return;
            GamepadActive = gamepad;
            KeysChanged?.Invoke();
        });

        // Domain reload is disabled on entering Play mode: the previous session's listener must be dropped by hand.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            listener?.Dispose();
            listener = null;
            GamepadActive = false;
            KeysChanged = null;
        }
    }
}
