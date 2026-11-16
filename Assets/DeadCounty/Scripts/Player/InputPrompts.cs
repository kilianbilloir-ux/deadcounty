using System;
using DeadCounty.UI;
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
        public static string SubmitKey => GamepadActive ? "A" : Named("Enter");
        public static string CancelKey => GamepadActive ? "B" : Named("Esc");

        // "E" on keyboard, "Y" on gamepad; null without project-wide actions.
        public static string Key(string actionPath)
        {
            var action = InputSystem.actions?.FindAction(actionPath);
            return action == null ? null : Named(action.GetBindingDisplayString(Options, GamepadActive ? GamepadGroup : KeyboardGroup));
        }

        // The key of one binding, for the controls options.
        public static string Key(InputAction action, int bindingIndex) => Named(action.GetBindingDisplayString(bindingIndex, Options));

        internal static void RaiseKeysChanged() => KeysChanged?.Invoke();

        // The Input System names keys in English.
        static string Named(string key) => Loc.Translated ? key : French(key);

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
            "LMB" or "Left Button" => "Clic gauche",
            "RMB" or "Right Button" => "Clic droit",
            _ => key,
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen()
        {
            listener = InputSystem.onAnyButtonPress.Call(control =>
            {
                var gamepad = control.device is Gamepad;
                if (gamepad == GamepadActive) return;
                GamepadActive = gamepad;
                KeysChanged?.Invoke();
            });
            // Keys are named in the language of the game.
            GameSettings.LanguageChanged += _ => KeysChanged?.Invoke();
        }

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
