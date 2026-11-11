using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace DeadCounty.Player
{
    // On-screen key prompts read from the project-wide action bindings, so they follow any rebinding. Prompts show
    // the key of the device last pressed: the keyboard until a gamepad button is pressed, and back.
    public static class InputPrompts
    {
        const string KeyboardGroup = "Keyboard&Mouse";
        const string GamepadGroup = "Gamepad";
        const InputBinding.DisplayStringOptions Options = InputBinding.DisplayStringOptions.DontIncludeInteractions;

        static IDisposable listener;

        public static bool GamepadActive { get; private set; }

        // Raised when the player switches between keyboard and gamepad, so prompts can be redrawn.
        public static event Action DeviceChanged;

        // "E" on keyboard, "Y" on gamepad; null without project-wide actions.
        public static string Key(string actionPath)
        {
            var action = InputSystem.actions?.FindAction(actionPath);
            return action == null ? null : French(action.GetBindingDisplayString(Options, GamepadActive ? GamepadGroup : KeyboardGroup));
        }

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
            DeviceChanged?.Invoke();
        });

        // Domain reload is disabled on entering Play mode: the previous session's listener must be dropped by hand.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            listener?.Dispose();
            listener = null;
            GamepadActive = false;
            DeviceChanged = null;
        }
    }
}
