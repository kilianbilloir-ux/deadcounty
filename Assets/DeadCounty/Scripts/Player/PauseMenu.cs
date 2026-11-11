using System;
using System.Collections.Generic;
using DeadCounty.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace DeadCounty.Player
{
    // Pause menu (GDD 8.11): Cancel freezes the game behind Reprendre, Options and Quitter. Options holds the mouse
    // sensitivity, the vertical look inversion, full screen, the interface size, the one-time tips and reduced
    // flashes, kept in PlayerPrefs (the last three through GameSettings). Navigate moves the selection
    // and changes an option sideways, Submit activates, Cancel goes back; the mouse can hover and click. Runs before
    // the inventory screen, which closes itself on Cancel: an open inventory is closed, not paused.
    [DefaultExecutionOrder(-10)]
    [RequireComponent(typeof(UIDocument))]
    public class PauseMenu : MonoBehaviour
    {
        const float NavigateThreshold = 0.5f;
        const float RepeatDelay = 0.35f;
        const float RepeatInterval = 0.12f;
        const float MinSensitivity = 0.03f;
        const float MaxSensitivity = 0.4f;
        const float SensitivityStep = 0.01f;
        const float UiScaleStep = 0.1f;
        const string SensitivityKey = "dc.mouseSensitivity";
        const string InvertKey = "dc.invertLook";

        sealed class Item
        {
            public VisualElement Element;
            public Action Activate;
            public Action<int> Adjust;
        }

        ThirdPersonController controller;
        PlayerHealth health;
        PlayerInventoryScreen inventoryScreen;
        InputAction cancel, submit, navigate;
        VisualElement screen, mainPage, settingsPage, sensitivityBar, sensitivityFill;
        Label invertValue, fullscreenValue, uiScaleValue, tipsValue, flashesValue;
        KeyHint confirmHint, backHint;
        List<Item> mainItems, settingsItems, items;
        int selected;
        Vector2Int held;
        float repeatAt, timeScaleBefore = 1f;

        public bool Paused { get; private set; }
        public bool InSettings => items == settingsItems;

        void Awake()
        {
            controller = Require<ThirdPersonController>();
            health = Require<PlayerHealth>();
            inventoryScreen = GetComponent<PlayerInventoryScreen>();
            if (inventoryScreen == null) throw new InvalidOperationException($"PauseMenu on '{name}' needs the PlayerInventoryScreen next to it");
            var actions = InputSystem.actions;
            cancel = actions.FindAction("UI/Cancel", true);
            submit = actions.FindAction("UI/Submit", true);
            navigate = actions.FindAction("UI/Navigate", true);
            controller.MouseSensitivity = PlayerPrefs.GetFloat(SensitivityKey, controller.MouseSensitivity);
            controller.InvertLook = PlayerPrefs.GetInt(InvertKey, 0) == 1;
        }

        T Require<T>() where T : Component
        {
            var component = GetComponentInChildren<T>(true);
            if (component == null) throw new InvalidOperationException($"PauseMenu on '{name}' needs a {typeof(T).Name} in its children");
            return component;
        }

        // Not in Awake: PlayerHudController loads the layout into the document in its own Awake.
        void Start()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            screen = Find<VisualElement>(root, "pause");
            mainPage = Find<VisualElement>(root, "pause-main");
            settingsPage = Find<VisualElement>(root, "pause-settings");
            sensitivityFill = Find<VisualElement>(root, "option-sensitivity-fill");
            sensitivityBar = sensitivityFill.parent;
            invertValue = Find<Label>(root, "option-invert-value");
            fullscreenValue = Find<Label>(root, "option-fullscreen-value");
            uiScaleValue = Find<Label>(root, "option-ui-scale-value");
            tipsValue = Find<Label>(root, "option-tips-value");
            flashesValue = Find<Label>(root, "option-flashes-value");

            mainItems = new List<Item>
            {
                Entry(root, "pause-resume", Resume),
                Entry(root, "pause-options", () => ShowPage(settingsItems)),
                Entry(root, "pause-quit", Quit),
            };
            settingsItems = new List<Item>
            {
                Entry(root, "option-sensitivity", null, step => SetSensitivity(controller.MouseSensitivity + step * SensitivityStep)),
                Entry(root, "option-invert", ToggleInvert, _ => ToggleInvert()),
                Entry(root, "option-fullscreen", ToggleFullScreen, _ => ToggleFullScreen()),
                Entry(root, "option-ui-scale", CycleUiScale, StepUiScale),
                Entry(root, "option-tips", ToggleTips, _ => ToggleTips()),
                Entry(root, "option-flashes", ToggleFlashes, _ => ToggleFlashes()),
                Entry(root, "option-back", () => ShowPage(mainItems)),
            };
            // Clicking along the bar sets the sensitivity there.
            sensitivityBar.pickingMode = PickingMode.Position;
            sensitivityBar.RegisterCallback<ClickEvent>(e =>
                SetSensitivity(Mathf.Lerp(MinSensitivity, MaxSensitivity, e.localPosition.x / sensitivityBar.resolvedStyle.width)));

            var hints = Find<VisualElement>(root, "pause-hints");
            confirmHint = new KeyHint(hints, "Valider");
            backHint = new KeyHint(hints, "Retour");
            ShowKeys();
            InputPrompts.DeviceChanged += ShowKeys;
            screen.style.display = DisplayStyle.None;
        }

        void OnDestroy() => InputPrompts.DeviceChanged -= ShowKeys;

        void OnDisable()
        {
            if (Paused) Time.timeScale = timeScaleBefore;
        }

        T Find<T>(VisualElement root, string elementName) where T : VisualElement =>
            root.Q<T>(elementName) ?? throw new InvalidOperationException($"PauseMenu on '{name}': the HUD has no '{elementName}' {typeof(T).Name}");

        Item Entry(VisualElement root, string elementName, Action activate, Action<int> adjust = null)
        {
            var item = new Item { Element = Find<VisualElement>(root, elementName), Activate = activate, Adjust = adjust };
            item.Element.RegisterCallback<PointerEnterEvent>(_ => Select(items.IndexOf(item)));
            item.Element.RegisterCallback<ClickEvent>(_ => item.Activate?.Invoke());
            return item;
        }

        // Submit and Cancel are generic bindings with no readable key name.
        void ShowKeys()
        {
            confirmHint.SetKey(InputPrompts.GamepadActive ? "A" : "Entrée");
            backHint.SetKey(InputPrompts.GamepadActive ? "B" : "Échap");
        }

        void Update()
        {
            if (!Paused)
            {
                if (cancel.WasPressedThisFrame() && health.IsAlive && controller.InputEnabled && !inventoryScreen.IsOpen) Open();
                return;
            }
            if (cancel.WasPressedThisFrame())
            {
                if (InSettings) ShowPage(mainItems);
                else Resume();
                return;
            }
            Navigate();
            if (submit.WasPressedThisFrame()) items[selected].Activate?.Invoke();
        }

        public void Open()
        {
            Paused = true;
            timeScaleBefore = Time.timeScale;
            Time.timeScale = 0f;
            controller.MenuOpen = true;
            screen.style.display = DisplayStyle.Flex;
            ShowPage(mainItems);
        }

        public void Resume()
        {
            Paused = false;
            Time.timeScale = timeScaleBefore;
            controller.MenuOpen = false;
            screen.style.display = DisplayStyle.None;
            PlayerPrefs.Save();
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void ShowPage(List<Item> page)
        {
            items = page;
            mainPage.style.display = page == mainItems ? DisplayStyle.Flex : DisplayStyle.None;
            settingsPage.style.display = page == settingsItems ? DisplayStyle.Flex : DisplayStyle.None;
            held = Vector2Int.zero;
            Select(0);
            ShowValues();
        }

        void Select(int index)
        {
            selected = Mathf.Clamp(index, 0, items.Count - 1);
            for (var i = 0; i < items.Count; i++) items[i].Element.EnableInClassList("menu-item--selected", i == selected);
        }

        // Up and down move the selection, left and right change the selected option; both repeat while held.
        // Real time: the game clock is stopped.
        void Navigate()
        {
            var input = navigate.ReadValue<Vector2>();
            var step = new Vector2Int(
                input.x > NavigateThreshold ? 1 : input.x < -NavigateThreshold ? -1 : 0,
                input.y > NavigateThreshold ? -1 : input.y < -NavigateThreshold ? 1 : 0);
            if (step == Vector2Int.zero)
            {
                held = step;
                return;
            }
            if (step == held && Time.unscaledTime < repeatAt) return;
            repeatAt = Time.unscaledTime + (step == held ? RepeatInterval : RepeatDelay);
            held = step;
            if (step.y != 0) Select(selected + step.y);
            else items[selected].Adjust?.Invoke(step.x);
        }

        void SetSensitivity(float value)
        {
            controller.MouseSensitivity = Mathf.Clamp(value, MinSensitivity, MaxSensitivity);
            PlayerPrefs.SetFloat(SensitivityKey, controller.MouseSensitivity);
            ShowValues();
        }

        void ToggleInvert()
        {
            controller.InvertLook = !controller.InvertLook;
            PlayerPrefs.SetInt(InvertKey, controller.InvertLook ? 1 : 0);
            ShowValues();
        }

        void StepUiScale(int step)
        {
            // Rounded so repeated steps land on whole tenths.
            GameSettings.UiScale = Mathf.Round((GameSettings.UiScale + step * UiScaleStep) * 10f) / 10f;
            ShowValues();
        }

        // A click has no direction: it goes up and wraps around to the smallest size.
        void CycleUiScale()
        {
            if (GameSettings.UiScale < GameSettings.MaxUiScale - 0.01f)
            {
                StepUiScale(1);
                return;
            }
            GameSettings.UiScale = GameSettings.MinUiScale;
            ShowValues();
        }

        void ToggleTips()
        {
            GameSettings.Tips = !GameSettings.Tips;
            ShowValues();
        }

        void ToggleFlashes()
        {
            GameSettings.ReduceFlashes = !GameSettings.ReduceFlashes;
            ShowValues();
        }

        // Screen.fullScreen only changes on the next frame: show the value asked for.
        void ToggleFullScreen()
        {
            var fullScreen = !Screen.fullScreen;
            Screen.fullScreen = fullScreen;
            fullscreenValue.text = fullScreen ? "Oui" : "Non";
        }

        void ShowValues()
        {
            sensitivityFill.style.width = Length.Percent(Mathf.InverseLerp(MinSensitivity, MaxSensitivity, controller.MouseSensitivity) * 100f);
            invertValue.text = controller.InvertLook ? "Oui" : "Non";
            fullscreenValue.text = Screen.fullScreen ? "Oui" : "Non";
            uiScaleValue.text = Mathf.RoundToInt(GameSettings.UiScale * 100f) + "\u00A0%";
            tipsValue.text = GameSettings.Tips ? "Oui" : "Non";
            flashesValue.text = GameSettings.ReduceFlashes ? "Oui" : "Non";
        }
    }
}
