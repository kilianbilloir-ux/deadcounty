using System;
using System.Text;
using DeadCounty.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace DeadCounty.Player
{
    // UI Toolkit HUD, nearly absent while all is well (GDD 8.11): the health bar shows while not full or just
    // after a hit and takes the danger colour when low, the stamina bar shows while not full and blinks when
    // exhausted, the flashlight and clock readouts show for a few seconds when they change. Also draws the hit
    // flash and the game over screen from PlayerDeathFlow. The only allocations are the clock and survival time
    // strings when they change.
    public class PlayerHudController : MonoBehaviour
    {
        const float LowHealth = 0.4f;
        const float HealthShowTime = 3f;
        const float ReadoutShowTime = 4f;
        const float FadeSpeed = 3f;
        const float ExhaustedBlinkRate = 3f;

        [SerializeField] PanelSettings panelSettings;
        [SerializeField] VisualTreeAsset hudLayout;
        [SerializeField] StyleSheet hudStyles;
        [SerializeField] float smoothTime = 0.12f;
        [SerializeField] float hitFlashTime = 0.3f;
        [SerializeField] float hitFlashAlpha = 0.35f;

        PlayerHealth health;
        PlayerStamina stamina;
        PlayerFlashlight flashlight;
        PlayerDeathFlow deathFlow;
        DayNightDirector director;
        VisualElement hud, healthRow, staminaRow, readouts, healthFill, staminaFill, flash, gameOver;
        Label flashlightLabel, time, gameOverTitle, gameOverTime, gameOverPrompt;
        StringBuilder text;
        float healthValue, healthVelocity, staminaValue, staminaVelocity, flashValue;
        float healthOpacity, staminaOpacity, readoutOpacity, readoutsUntil;
        int day = -1, hour = -1, minute = -1, phase = -1;
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
            flashlight = GetComponentInChildren<PlayerFlashlight>(true);
            deathFlow = GetComponent<PlayerDeathFlow>();
            director = FindAnyObjectByType<DayNightDirector>();
            if (health == null || stamina == null) throw new InvalidOperationException($"PlayerHudController on '{name}' needs a PlayerHealth and a PlayerStamina in its children");

            var document = GetComponent<UIDocument>();
            if (document == null) throw new InvalidOperationException($"PlayerHudController on '{name}' needs a UIDocument");
            if (panelSettings != null) document.panelSettings = panelSettings;
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
            flashlightLabel = Find<Label>(root, "flashlight");
            time = Find<Label>(root, "time");
            gameOverTitle = Find<Label>(root, "game-over-title");
            gameOverTime = Find<Label>(root, "game-over-time");
            gameOverPrompt = Find<Label>(root, "game-over-prompt");
            text = new StringBuilder(32);
            gameOverPrompt.text = RestartPrompt();
            time.style.display = director != null ? DisplayStyle.Flex : DisplayStyle.None;
        }

        T Find<T>(VisualElement root, string elementName) where T : VisualElement =>
            root.Q<T>(elementName) ?? throw new InvalidOperationException($"PlayerHudController on '{name}': the HUD has no '{elementName}' {typeof(T).Name}");

        // Respawn is bound to Jump; the prompt names the keyboard and gamepad buttons from the action itself.
        static string RestartPrompt()
        {
            var jump = InputSystem.actions?.FindAction("Player/Jump");
            if (jump == null) return "Recommencer";
            return $"[{jump.GetBindingDisplayString(group: "Keyboard&Mouse")} / {jump.GetBindingDisplayString(group: "Gamepad")}] Recommencer";
        }

        // Not in Awake: the pools are created in the health and stamina Awakes, which may run after this one.
        void Start()
        {
            healthValue = health.Pool.Normalized;
            staminaValue = stamina.Model.Normalized;
            ShowReadouts();
        }

        void OnEnable() => health.Damaged += OnDamaged;

        void OnDisable() => health.Damaged -= OnDamaged;

        void OnDamaged(PlayerHealth _) => flashValue = 1f;

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
            staminaFill.EnableInClassList("stamina-fill--spent", model.Exhausted && Mathf.Repeat(now * ExhaustedBlinkRate, 1f) < 0.5f);

            var lit = flashlight != null && flashlight.IsOn;
            if (lit != flashlightWasOn)
            {
                flashlightWasOn = lit;
                ShowReadouts();
            }
            flashlightLabel.EnableInClassList("flashlight-readout--on", lit);
            UpdateClock();

            var showHealth = healthRatio < 1f || now - health.LastHitTime < HealthShowTime;
            var showStamina = model.Normalized < 1f || staminaValue < 0.995f;
            Fade(healthRow, ref healthOpacity, alive && showHealth, dt);
            Fade(staminaRow, ref staminaOpacity, alive && showStamina, dt);
            Fade(readouts, ref readoutOpacity, alive && now < readoutsUntil, dt);

            flashValue = Mathf.Max(flashValue - dt / hitFlashTime, 0f);
            flash.style.opacity = flashValue * hitFlashAlpha;
            hud.style.display = alive ? DisplayStyle.Flex : DisplayStyle.None;
            UpdateGameOver();
        }

        static void Fade(VisualElement element, ref float opacity, bool visible, float dt)
        {
            opacity = Mathf.MoveTowards(opacity, visible ? 1f : 0f, FadeSpeed * dt);
            element.style.opacity = opacity;
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
            gameOverPrompt.style.display = dead && deathFlow.CanRespawn ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
