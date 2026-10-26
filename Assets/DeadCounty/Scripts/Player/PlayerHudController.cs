using System;
using System.Text;
using DeadCounty.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeadCounty.Player
{
    // UI Toolkit HUD: health and stamina bars (stamina fades out once full), night lamp state and the
    // in-game clock, plus the red flash on hit. Updates every frame; the only allocation is the clock
    // string when the displayed minute changes.
    public class PlayerHudController : MonoBehaviour
    {
        static readonly Color Danger = new(0.65f, 0.37f, 0.23f);
        static readonly Color Sandstone = new(0.83f, 0.72f, 0.46f);
        static readonly Color Spent = new(0.35f, 0.19f, 0.1f);

        [SerializeField] PanelSettings panelSettings;
        [SerializeField] VisualTreeAsset hudLayout;
        [SerializeField] StyleSheet hudStyles;
        [SerializeField] float smoothTime = 0.12f;
        [SerializeField] float hitFlashTime = 0.3f;
        [SerializeField] float hitFlashAlpha = 0.35f;

        PlayerHealth health;
        PlayerStamina stamina;
        PlayerFlashlight flashlight;
        DayNightDirector director;
        VisualElement hud, healthFill, staminaFill, flash;
        Label lamp, time;
        StringBuilder clock;
        float healthValue, healthVelocity, staminaValue, staminaVelocity, flashValue;
        int day = 1, hour = -1, minute = -1;

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
            director = FindFirstObjectByType<DayNightDirector>();
            if (health == null || stamina == null) throw new InvalidOperationException($"PlayerHudController on '{name}' needs a PlayerHealth and a PlayerStamina in its children");

            var document = GetComponent<UIDocument>();
            if (document == null) throw new InvalidOperationException($"PlayerHudController on '{name}' needs a UIDocument");
            if (panelSettings != null) document.panelSettings = panelSettings;
            if (hudLayout != null) document.visualTreeAsset = hudLayout;
            var root = document.rootVisualElement;
            if (root == null) throw new InvalidOperationException($"PlayerHudController on '{name}': no UI root");
            if (hudStyles != null) root.styleSheets.Add(hudStyles);
            hud = root.Q<VisualElement>("hud");
            healthFill = root.Q<VisualElement>("health-fill");
            staminaFill = root.Q<VisualElement>("stamina-fill");
            flash = root.Q<VisualElement>("hit-flash");
            lamp = root.Q<Label>("lamp");
            time = root.Q<Label>("time");
            if (hud == null || healthFill == null || staminaFill == null || flash == null || lamp == null || time == null)
                throw new InvalidOperationException($"PlayerHudController on '{name}': the HUD is missing a required element");
            clock = new StringBuilder(16);
        }

        // Not in Awake: the pools are created in the health and stamina Awakes, which may run after this one.
        void Start()
        {
            healthValue = health.Pool.Normalized;
            staminaValue = stamina.Model.Normalized;
        }

        void OnEnable() => health.Damaged += OnDamaged;

        void OnDisable() => health.Damaged -= OnDamaged;

        void OnDamaged(PlayerHealth _) => flashValue = 1f;

        void Update()
        {
            var dt = Time.deltaTime;
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

            var model = stamina.Model;
            var full = model.Normalized >= 1f;
            staminaFill.style.opacity = new StyleFloat(full ? Mathf.Clamp01((1f - staminaValue) * 8f) : 1f);
            staminaFill.style.unityBackgroundImageTintColor = new StyleColor(
                !full && model.Exhausted ? Color.Lerp(Sandstone, Spent, Mathf.PingPong(Time.time * 3f, 1f)) : Sandstone);

            // Lamp readouts follow the same rule as the night lamps, or the player's own flashlight.
            var lit = NightLamps.ShouldBeOn(false, WorldLighting.NightFactor) || (flashlight != null && flashlight.IsOn);
            lamp.EnableInClassList("lamp-readout--on", lit);
            lamp.style.opacity = new StyleFloat(lit ? 1f : 0.45f);
            UpdateClock();

            flashValue = Mathf.Max(flashValue - dt / hitFlashTime, 0f);
            flash.style.opacity = new StyleFloat(flashValue * hitFlashAlpha);
            hud.style.display = alive ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void UpdateClock()
        {
            var gameHour = director != null ? director.Hour : FallbackHour();
            var h = (int)Mathf.Floor(gameHour);
            var m = (int)Mathf.Floor((gameHour - h) * 60f);
            if (h == hour && m == minute) return;
            // The clock wraps back to 0 at midnight, which is the next day.
            if (h < hour) day++;
            hour = h;
            minute = m;
            clock.Clear();
            clock.Append("Jour ").Append(day).Append(" — ").Append(h.ToString("00")).Append(':').Append(m.ToString("00"));
            time.text = clock.ToString();
        }

        // Without a director the best guess from the darkness: night runs backwards from midnight.
        static float FallbackHour()
        {
            var night = WorldLighting.NightFactor;
            if (night <= 0f) return 12f;
            if (night >= 1f) return 0f;
            return night < 0.5f ? 6f : 20f;
        }
    }
}
