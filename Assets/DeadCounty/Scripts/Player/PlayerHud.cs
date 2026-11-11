using System;
using UnityEngine;

namespace DeadCounty.Player
{
    // Prototype IMGUI HUD: health bar bottom-left, stamina bar under it only while not full (blinking when
    // exhausted), and a short red flash when hit. Hidden while dead, PlayerDeathFlow owns the screen then.
    public class PlayerHud : MonoBehaviour
    {
        [SerializeField] float margin = 20f;
        [SerializeField] float healthWidth = 220f;
        [SerializeField] float healthHeight = 12f;
        [SerializeField] float staminaWidth = 140f;
        [SerializeField] float staminaHeight = 5f;
        [SerializeField] float hitFlashTime = 0.3f;
        [SerializeField] float hitFlashAlpha = 0.35f;

        static readonly Color Backdrop = new(0f, 0f, 0f, 0.55f);
        static readonly Color HealthColor = new(0.72f, 0.12f, 0.1f, 0.95f);
        static readonly Color StaminaColor = new(0.85f, 0.8f, 0.55f, 0.9f);
        static readonly Color ExhaustedColor = new(0.95f, 0.45f, 0.15f, 0.9f);

        PlayerHealth health;
        PlayerStamina stamina;
        float lastHitTime = float.NegativeInfinity;

        void Awake()
        {
            health = GetComponentInChildren<PlayerHealth>(true);
            stamina = GetComponentInChildren<PlayerStamina>(true);
            if (health == null || stamina == null) throw new InvalidOperationException($"PlayerHud on '{name}' needs a PlayerHealth and a PlayerStamina in its children");
        }

        void OnEnable() => health.Damaged += OnDamaged;

        void OnDisable() => health.Damaged -= OnDamaged;

        void OnDamaged(PlayerHealth _) => lastHitTime = Time.time;

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || !health.IsAlive) return;
            var color = GUI.color;

            var flash = 1f - (Time.time - lastHitTime) / hitFlashTime;
            if (flash > 0f) Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.6f, 0f, 0f, hitFlashAlpha * flash));

            var healthRect = new Rect(margin, Screen.height - margin - healthHeight - staminaHeight - 6f, healthWidth, healthHeight);
            Bar(healthRect, health.Pool.Normalized, HealthColor);

            var model = stamina.Model;
            if (model.Normalized < 1f)
            {
                var staminaRect = new Rect(margin, healthRect.yMax + 6f, staminaWidth, staminaHeight);
                var fill = model.Exhausted ? ExhaustedColor : StaminaColor;
                if (model.Exhausted) fill.a *= 0.35f + 0.65f * Mathf.PingPong(Time.time * 3f, 1f);
                Bar(staminaRect, model.Normalized, fill);
            }
            GUI.color = color;
        }

        static void Bar(Rect rect, float fraction, Color fill)
        {
            Fill(new Rect(rect.x - 2f, rect.y - 2f, rect.width + 4f, rect.height + 4f), Backdrop);
            Fill(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fraction), rect.height), fill);
        }

        static void Fill(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
        }
    }
}
