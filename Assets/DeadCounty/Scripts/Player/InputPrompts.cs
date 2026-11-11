using UnityEngine.InputSystem;

namespace DeadCounty.Player
{
    // On-screen key prompts read from the project-wide action bindings, so they follow any rebinding.
    public static class InputPrompts
    {
        // "E / Y": the keyboard and gamepad buttons of an action, or null without project-wide actions.
        public static string Keys(string actionPath)
        {
            var action = InputSystem.actions?.FindAction(actionPath);
            if (action == null) return null;
            const InputBinding.DisplayStringOptions options = InputBinding.DisplayStringOptions.DontIncludeInteractions;
            return $"{action.GetBindingDisplayString(options, "Keyboard&Mouse")} / {action.GetBindingDisplayString(options, "Gamepad")}";
        }

        // "1 2": the keyboard keys of two actions, for compact hints.
        public static string KeyboardKeys(string firstAction, string secondAction)
        {
            var actions = InputSystem.actions;
            if (actions == null) return null;
            const InputBinding.DisplayStringOptions options = InputBinding.DisplayStringOptions.DontIncludeInteractions;
            var first = actions.FindAction(firstAction, true).GetBindingDisplayString(options, "Keyboard&Mouse");
            var second = actions.FindAction(secondAction, true).GetBindingDisplayString(options, "Keyboard&Mouse");
            return $"{first} {second}";
        }

        // "[E / Y] Ouvrir", or the bare action without keys.
        public static string Prompt(string keys, string action) => keys == null ? action : $"[{keys}] {action}";
    }
}
