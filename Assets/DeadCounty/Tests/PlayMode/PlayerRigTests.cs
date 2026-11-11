#if UNITY_EDITOR
using System.Collections;
using DeadCounty.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace DeadCounty.Tests
{
    // The generated player rig prefab on a bare floor, destroyed after each test so no second player leaks into
    // the next one. Virtual devices drive the project-wide actions; keyboard events reach the game even while
    // the Game view is unfocused.
    public abstract class PlayerRigTests
    {
        const string RigPath = "Assets/DeadCounty/Prefabs/PlayerRig.prefab";

        GameObject ground;
        InputSettings.EditorInputBehaviorInPlayMode savedEditorInput;

        private protected GameObject Rig;
        private protected PlayerHealth Health;
        private protected PlayerStamina Stamina;
        private protected Keyboard Keyboard;
        private protected Gamepad Gamepad;

        [UnitySetUp]
        public IEnumerator SetUpRig()
        {
            savedEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Keyboard = InputSystem.AddDevice<Keyboard>("TestKeyboard");
            Gamepad = InputSystem.AddDevice<Gamepad>("TestGamepad");

            ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = new Vector3(4f, 1f, 4f);
            Physics.SyncTransforms();
            Rig = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RigPath));
            Health = Rig.GetComponentInChildren<PlayerHealth>();
            Stamina = Rig.GetComponentInChildren<PlayerStamina>();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDownRig()
        {
            Object.Destroy(Rig);
            Object.Destroy(ground);
            InputSystem.RemoveDevice(Keyboard);
            InputSystem.RemoveDevice(Gamepad);
            InputSystem.settings.editorInputBehaviorInPlayMode = savedEditorInput;
            yield return null;
        }

        // Down for one frame, then up: the action sees exactly one press.
        private protected static IEnumerator Tap<TState>(InputDevice device, TState pressed, TState released) where TState : struct, IInputStateTypeInfo
        {
            InputSystem.QueueStateEvent(device, pressed);
            yield return null;
            InputSystem.QueueStateEvent(device, released);
            yield return null;
        }
    }
}
#endif
