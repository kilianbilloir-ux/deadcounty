using System;
using System.Text;
using System.Collections.Generic;
using DeadCounty.Core;
using DeadCounty.Items;
using DeadCounty.UI;
using DeadCounty.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeadCounty.Player
{
    // UI Toolkit HUD, nearly absent while all is well (GDD 8.11): each need gauge shows once the need is half
    // empty and takes the danger colour when low, the health bar shows while not full or just after a hit and
    // takes the danger colour when low, the stamina bar shows while not full and blinks when exhausted, the
    // flashlight and clock readouts show for a few seconds when they change, with the need gauges. The
    // interaction prompt sits next to what the player can use, its name above the key and the action (GDD 8.11,
    // after Valheim), with a ring filling through a search; what was found, or why the player woke up, shows for a
    // moment below the middle of the screen. A hit flashes the screen and leaves an arc around the centre that
    // points at where it came from and follows the camera. Also draws the game over screen from PlayerDeathFlow.
    // The only allocations are texts rebuilt when they change. Afflictions and the sleep screen are
    // PlayerConditionHud's.
    public class PlayerHudController : MonoBehaviour
    {
        const float LowHealth = 0.4f;
        const float NeedShowBelow = 0.5f;
        const float LowNeed = 0.2f;
        const float HealthShowTime = 3f;
        const float ReadoutShowTime = 4f;
        const float FadeSpeed = 3f;
        const float ExhaustedBlinkRate = 3f;
        // Prompt offset from the projected interaction point, in panel pixels.
        const float PromptOffsetX = 16f;
        const float PromptOffsetY = -22f;
        const float ToastShowTime = 3f;
        const int HitMarkerCount = 3;
        const float HitMarkerHold = 0.6f;
        const float HitMarkerFade = 0.9f;

        [SerializeField] PanelSettings panelSettings;
        [SerializeField] VisualTreeAsset hudLayout;
        [SerializeField] StyleSheet hudStyles;
        [SerializeField] float smoothTime = 0.12f;
        [SerializeField] float hitFlashTime = 0.3f;
        [SerializeField] float hitFlashAlpha = 0.35f;

        readonly HitMarker[] hitMarkers = new HitMarker[HitMarkerCount];
        readonly Vector3[] hitSources = new Vector3[HitMarkerCount];
        readonly float[] hitTimes = new float[HitMarkerCount];
        PanelSettings panel;
        PlayerHealth health;
        PlayerStamina stamina;
        PlayerNeeds needs;
        PlayerFlashlight flashlight;
        PlayerDeathFlow deathFlow;
        PlayerInteractor interactor;
        PlayerInventory inventory;
        PlayerSleep sleep;
        ThirdPersonController controller;
        GameMenu menu;
        DayNightDirector director;
        VisualElement hud, healthRow, staminaRow, readouts, healthFill, staminaFill, flash, gameOver;
        VisualElement hungerNeed, thirstNeed, fatigueNeed;
        RadialGauge hungerGauge, thirstGauge, fatigueGauge;
        VisualElement interaction;
        Label interactionName, interactionAction, interactionKey, gameOverKey, gameOverMenuKey, gameOverMenu, toast;
        VisualElement interactionRow;
        RadialGauge interactionProgress;
        Interactable shownInteractable;
        string shownAction;
        Label flashlightLabel, time, gameOverTitle, gameOverTime, gameOverPrompt;
        StringBuilder text;
        float healthValue, healthVelocity, staminaValue, staminaVelocity, flashValue;
        float healthOpacity, staminaOpacity, readoutOpacity, readoutsUntil;
        float hungerOpacity, thirstOpacity, fatigueOpacity, toastOpacity, toastUntil;
        int day = -1, hour = -1, minute = -1, phase = -1;
        int nextHitMarker;
        bool flashlightWasOn, deathShown;

        // Assigned by the builder; a rig built in code passes them through Configure.
        public void Configure(PanelSettings settings, VisualTreeAsset uxml, StyleSheet uss)
        {
            panelSettings = settings;
            hudLayout = uxml;
            hudStyles = uss;
        }

        void Awake()
        {
            health = GetComponentInChildren<PlayerHealth>(true);
            stamina = GetComponentInChildren<PlayerStamina>(true);
            needs = GetComponentInChildren<PlayerNeeds>(true);
            flashlight = GetComponentInChildren<PlayerFlashlight>(true);
            deathFlow = GetComponent<PlayerDeathFlow>();
            interactor = GetComponentInChildren<PlayerInteractor>(true);
            inventory = GetComponentInChildren<PlayerInventory>(true);
            sleep = GetComponentInChildren<PlayerSleep>(true);
            controller = GetComponentInChildren<ThirdPersonController>(true);
            menu = GetComponent<GameMenu>();
            director = FindAnyObjectByType<DayNightDirector>();
            if (health == null || stamina == null || needs == null || interactor == null || inventory == null || sleep == null || controller == null || menu == null)
                throw new InvalidOperationException($"PlayerHudController on '{name}' needs a PlayerHealth, a PlayerStamina, a PlayerNeeds, a PlayerInteractor, a PlayerInventory, a PlayerSleep and a ThirdPersonController in its children and a GameMenu next to it");

            var document = GetComponent<UIDocument>();
            if (document == null) throw new InvalidOperationException($"PlayerHudController on '{name}' needs a UIDocument");
            // A copy, so the interface size option never writes into the asset.
            var settings = panelSettings != null ? panelSettings : document.panelSettings;
            if (settings != null) document.panelSettings = panel = Instantiate(settings);
            if (hudLayout != null) document.visualTreeAsset = hudLayout;
            var root = document.rootVisualElement;
            if (root == null) throw new InvalidOperationException($"PlayerHudController on '{name}': no UI root");
            if (hudStyles != null) root.styleSheets.Add(hudStyles);
            hud = Find<VisualElement>(root, "hud");
            healthRow = Find<VisualElement>(root, "health-row");
            staminaRow = Find<VisualElement>(root, "stamina-row");
            readouts = Find<VisualElement>(root, "readouts");
            healthFill = Find<VisualElement>(root, "health-fill");
            staminaFill = Find<VisualElement>(root, "stamina-fill");
            flash = Find<VisualElement>(root, "hit-flash");
            gameOver = Find<VisualElement>(root, "game-over");
            hungerNeed = Find<VisualElement>(root, "hunger");
            thirstNeed = Find<VisualElement>(root, "thirst");
            fatigueNeed = Find<VisualElement>(root, "fatigue");
            hungerGauge = Find<RadialGauge>(root, "hunger-gauge");
            thirstGauge = Find<RadialGauge>(root, "thirst-gauge");
            fatigueGauge = Find<RadialGauge>(root, "fatigue-gauge");
            interaction = Find<VisualElement>(root, "interaction");
            interactionName = Find<Label>(root, "interaction-name");
            interactionAction = Find<Label>(root, "interaction-action");
            interactionKey = Find<Label>(root, "interaction-key");
            interactionRow = Find<VisualElement>(root, "interaction-row");
            toast = Find<Label>(root, "loot-toast");
            interactionProgress = Find<RadialGauge>(root, "interaction-progress");
            flashlightLabel = Find<Label>(root, "flashlight");
            time = Find<Label>(root, "time");
            gameOverTitle = Find<Label>(root, "game-over-title");
            gameOverTime = Find<Label>(root, "game-over-time");
            gameOverPrompt = Find<Label>(root, "game-over-prompt");
            gameOverKey = Find<Label>(root, "game-over-key");
            gameOverMenu = Find<Label>(root, "game-over-menu");
            gameOverMenuKey = Find<Label>(root, "game-over-menu-key");
            var markers = Find<VisualElement>(root, "hit-markers");
            for (var i = 0; i < HitMarkerCount; i++)
            {
                markers.Add(hitMarkers[i] = new HitMarker());
                hitMarkers[i].style.opacity = 0f;
                hitTimes[i] = float.NegativeInfinity;
            }
            text = new StringBuilder(32);
            ShowKeys();
            ApplyUiScale();
            time.style.display = director != null ? DisplayStyle.Flex : DisplayStyle.None;
        }

        T Find<T>(VisualElement root, string elementName) where T : VisualElement =>
            root.Q<T>(elementName) ?? throw new InvalidOperationException($"PlayerHudController on '{name}': the HUD has no '{elementName}' {typeof(T).Name}");

        // Keys of the device in use; respawn is bound to Jump, the way back to the title to Cancel (GameMenu).
        void ShowKeys()
        {
            interactionKey.text = InputPrompts.Key("Player/Interact");
            gameOverKey.text = InputPrompts.Key("Player/Jump");
            gameOverMenuKey.text = InputPrompts.CancelKey;
        }

        // Not in Awake: the pools are created in the health and stamina Awakes, which may run after this one.
        void Start()
        {
            healthValue = health.Pool.Normalized;
            staminaValue = stamina.Model.Normalized;
            ShowReadouts();
        }

        void OnEnable()
        {
            health.Damaged += OnDamaged;
            inventory.Received += OnReceived;
            sleep.WokeUp += ShowToast;
            InputPrompts.KeysChanged += ShowKeys;
            GameSettings.Changed += ApplyUiScale;
        }

        void OnDisable()
        {
            health.Damaged -= OnDamaged;
            inventory.Received -= OnReceived;
            sleep.WokeUp -= ShowToast;
            InputPrompts.KeysChanged -= ShowKeys;
            GameSettings.Changed -= ApplyUiScale;
        }

        void OnDestroy() => Destroy(panel);

        void ApplyUiScale() => panel.scale = GameSettings.UiScale;

        void OnReceived(IReadOnlyList<ItemStack> items)
        {
            text.Clear();
            if (items.Count == 0) text.Append("Rien trouvé");
            for (var i = 0; i < items.Count; i++)
            {
                text.Append(i == 0 ? "Trouvé : " : ", ").Append(items[i].Item.DisplayName);
                if (items[i].Count > 1) text.Append(" ×").Append(items[i].Count);
            }
            ShowToast(text.ToString());
        }

        void ShowToast(string message)
        {
            toast.text = message;
            toastUntil = Time.time + ToastShowTime;
        }

        void OnDamaged(PlayerHealth _)
        {
            if (!GameSettings.ReduceFlashes) flashValue = 1f;
            hitSources[nextHitMarker] = health.LastHitSource;
            hitTimes[nextHitMarker] = Time.time;
            nextHitMarker = (nextHitMarker + 1) % HitMarkerCount;
        }

        void ShowReadouts() => readoutsUntil = Time.time + ReadoutShowTime;

        void Update()
        {
            var dt = Time.deltaTime;
            var now = Time.time;
            var alive = health.IsAlive;
            if (alive)
            {
                healthValue = Mathf.SmoothDamp(healthValue, health.Pool.Normalized, ref healthVelocity, smoothTime, Mathf.Infinity, dt);
                staminaValue = Mathf.SmoothDamp(staminaValue, stamina.Model.Normalized, ref staminaVelocity, smoothTime, Mathf.Infinity, dt);
            }
            else
            {
                healthValue = staminaValue = 0f;
            }
            healthFill.style.width = Length.Percent(Mathf.Clamp01(healthValue) * 100f);
            staminaFill.style.width = Length.Percent(Mathf.Clamp01(staminaValue) * 100f);

            var healthRatio = health.Pool.Normalized;
            healthFill.EnableInClassList("health-fill--low", healthRatio <= LowHealth);
            var model = stamina.Model;
            staminaFill.EnableInClassList("stamina-fill--spent", model.Exhausted && (GameSettings.ReduceFlashes || Mathf.Repeat(now * ExhaustedBlinkRate, 1f) < 0.5f));

            var lit = flashlight != null && flashlight.IsOn;
            if (lit != flashlightWasOn)
            {
                flashlightWasOn = lit;
                flashlightLabel.text = lit ? "Lampe allumée" : "Lampe éteinte";
                flashlightLabel.EnableInClassList("flashlight-readout--on", lit);
                ShowReadouts();
            }
            UpdateClock();

            var showHealth = healthRatio < 1f || now - health.LastHitTime < HealthShowTime;
            var showStamina = model.Normalized < 1f || staminaValue < 0.995f;
            Fade(healthRow, ref healthOpacity, alive && showHealth, dt);
            Fade(staminaRow, ref staminaOpacity, alive && showStamina, dt);
            var readoutsShown = alive && now < readoutsUntil;
            Fade(readouts, ref readoutOpacity, readoutsShown, dt);
            var needsModel = needs.Model;
            UpdateNeed(hungerNeed, hungerGauge, ref hungerOpacity, needsModel.Hunger, alive, readoutsShown, dt);
            UpdateNeed(thirstNeed, thirstGauge, ref thirstOpacity, needsModel.Thirst, alive, readoutsShown, dt);
            UpdateNeed(fatigueNeed, fatigueGauge, ref fatigueOpacity, needsModel.Fatigue, alive, readoutsShown, dt);
            Fade(toast, ref toastOpacity, alive && now < toastUntil, dt);

            flashValue = Mathf.Max(flashValue - dt / hitFlashTime, 0f);
            flash.style.opacity = flashValue * hitFlashAlpha;
            // Menus cover it: the inventory shows the same stats, the pause hides the game.
            hud.style.display = alive && !controller.MenuOpen ? DisplayStyle.Flex : DisplayStyle.None;
            // The inventory leaves the world running: a zombie can still strike, so its arc must show.
            UpdateHitMarkers(now, alive && !menu.Shown);
            UpdateInteraction(alive);
            UpdateGameOver();
        }

        static void Fade(VisualElement element, ref float opacity, bool visible, float dt)
        {
            opacity = Mathf.MoveTowards(opacity, visible ? 1f : 0f, FadeSpeed * dt);
            element.style.opacity = opacity;
        }

        static void UpdateNeed(VisualElement need, RadialGauge gauge, ref float opacity, float value, bool alive, bool readoutsShown, float dt)
        {
            gauge.Value = value;
            need.EnableInClassList("need--low", value <= LowNeed);
            Fade(need, ref opacity, alive && (value < NeedShowBelow || readoutsShown), dt);
        }

        // Measured from the camera heading on every frame: turning toward the attacker brings its arc to the top.
        void UpdateHitMarkers(float now, bool shown)
        {
            var view = Camera.main;
            var player = health.transform.position;
            for (var i = 0; i < HitMarkerCount; i++)
            {
                var opacity = shown && view != null ? 1f - Mathf.Clamp01((now - hitTimes[i] - HitMarkerHold) / HitMarkerFade) : 0f;
                hitMarkers[i].style.opacity = opacity;
                if (opacity <= 0f) continue;
                var heading = view.transform.forward;
                heading.y = 0f;
                var toSource = hitSources[i] - player;
                toSource.y = 0f;
                hitMarkers[i].style.rotate = new Rotate(Angle.Degrees(Vector3.SignedAngle(heading, toSource, Vector3.up)));
            }
        }

        // Text changes only when the target or its action does; the position follows the camera every frame.
        void UpdateInteraction(bool alive)
        {
            var current = alive ? interactor.Current : null;
            var view = Camera.main;
            if (current == null || view == null)
            {
                interaction.style.display = DisplayStyle.None;
                shownInteractable = null;
                return;
            }
            var action = current.Action;
            if (current != shownInteractable || action != shownAction)
            {
                shownInteractable = current;
                shownAction = action;
                interactionName.text = current.DisplayName;
                interactionAction.text = action ?? "";
                interactionRow.style.display = action == null ? DisplayStyle.None : DisplayStyle.Flex;
            }
            interactionProgress.style.display = interactor.Busy ? DisplayStyle.Flex : DisplayStyle.None;
            if (interactor.Busy) interactionProgress.Value = interactor.UseProgress;
            var point = RuntimePanelUtils.CameraTransformWorldToPanel(interaction.panel, current.PromptPoint, view);
            interaction.style.left = point.x + PromptOffsetX;
            interaction.style.top = point.y + PromptOffsetY;
            interaction.style.display = DisplayStyle.Flex;
        }

        void UpdateClock()
        {
            if (director == null) return;
            var gameHour = director.Hour;
            var h = (int)gameHour;
            var m = (int)((gameHour - h) * 60f);
            var p = DayPhase(gameHour);
            if (p != phase)
            {
                // Dawn, day, dusk and night each bring the clock back for a moment.
                if (phase >= 0) ShowReadouts();
                phase = p;
            }
            if (director.Day == day && h == hour && m == minute) return;
            day = director.Day;
            hour = h;
            minute = m;
            text.Clear();
            text.Append("Jour ").Append(day).Append(" — ").Append(h.ToString("00")).Append(':').Append(m.ToString("00"));
            time.text = text.ToString();
        }

        static int DayPhase(float hour) =>
            hour < DayNightModel.DawnStart ? 3
            : hour < DayNightModel.DayStart ? 0
            : hour < DayNightModel.DuskStart ? 1
            : hour < DayNightModel.NightStart ? 2
            : 3;

        void UpdateGameOver()
        {
            var fade = deathFlow != null ? deathFlow.ScreenFade : 0f;
            gameOver.style.display = fade > 0f ? DisplayStyle.Flex : DisplayStyle.None;
            gameOver.style.opacity = fade;
            var dead = deathFlow != null && deathFlow.IsDead;
            if (dead && !deathShown)
            {
                var seconds = Mathf.FloorToInt(deathFlow.SurvivedSeconds);
                text.Clear();
                text.Append("Temps survécu : ").Append((seconds / 60).ToString("00")).Append(':').Append((seconds % 60).ToString("00"));
                gameOverTime.text = text.ToString();
            }
            else if (!dead && deathShown)
            {
                ShowReadouts();
            }
            deathShown = dead;
            // Only the black screen fades back out after the respawn.
            gameOverTitle.style.display = dead ? DisplayStyle.Flex : DisplayStyle.None;
            gameOverTime.style.display = dead ? DisplayStyle.Flex : DisplayStyle.None;
            var prompt = dead && deathFlow.CanRespawn ? DisplayStyle.Flex : DisplayStyle.None;
            gameOverPrompt.style.display = prompt;
            gameOverKey.style.display = prompt;
            gameOverMenu.style.display = prompt;
            gameOverMenuKey.style.display = prompt;
        }
    }
}
