using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeadCounty.Player
{
    // Death and respawn loop: on death the body collapses, the screen fades to black with the survival time,
    // and Space respawns the player at the pose recorded in Start with full health and stamina.
    // Lives on the rig root so it keeps running if the Player object gets disabled.
    public class PlayerDeathFlow : MonoBehaviour
    {
        [Header("Timing (s)")]
        [SerializeField] float collapseTime = 0.6f;
        [SerializeField] float fadeDelay = 1.2f;
        [SerializeField] float fadeTime = 1.5f;
        [SerializeField] float restartDelay = 2.5f;
        [SerializeField] float fadeOutTime = 0.6f;

        [Header("Collapse")]
        [Tooltip("Forward tilt of the body once on the ground (deg).")]
        [SerializeField] float lyingAngle = 88f;
        [Tooltip("Lift keeping the chest above the ground once lying (m).")]
        [SerializeField] float lyingLift = 0.12f;

        ThirdPersonController controller;
        PlayerHealth health;
        PlayerStamina stamina;
        ProceduralHumanoidAnimator animator;
        Transform model;
        Vector3 spawnPosition, collapseStartPosition;
        Quaternion spawnRotation, collapseStartRotation;
        float spawnTime, deathTime, fadeOutStart = float.NegativeInfinity, fadeOutFrom;
        bool dead;
        string survivedText;
        GUIStyle titleStyle, lineStyle;

        public static event Action PlayerRespawned;
        public event Action Respawned;

        void Awake()
        {
            controller = Require<ThirdPersonController>();
            health = Require<PlayerHealth>();
            stamina = Require<PlayerStamina>();
            animator = Require<ProceduralHumanoidAnimator>();
            model = controller.transform.Find("Model");
            if (model == null) throw new InvalidOperationException($"'{controller.name}' has no 'Model' child to collapse");
        }

        T Require<T>() where T : Component
        {
            var component = GetComponentInChildren<T>(true);
            if (component == null) throw new InvalidOperationException($"PlayerDeathFlow on '{name}' needs a {typeof(T).Name} in its children");
            return component;
        }

        void OnEnable() => health.Died += OnDied;

        void OnDisable() => health.Died -= OnDied;

        void Start()
        {
            spawnPosition = controller.transform.position;
            spawnRotation = controller.transform.rotation;
            spawnTime = Time.time;
        }

        void OnDied(PlayerHealth _)
        {
            dead = true;
            deathTime = Time.time;
            var survived = Mathf.FloorToInt(deathTime - spawnTime);
            survivedText = $"Temps survécu : {survived / 60:00}:{survived % 60:00}";
            controller.InputEnabled = false;
            // The animator rewrites the Model pose every LateUpdate, which would undo the collapse.
            animator.enabled = false;
            collapseStartPosition = model.localPosition;
            collapseStartRotation = model.localRotation;
        }

        void Update()
        {
            if (!dead) return;
            var elapsed = Time.time - deathTime;
            // Ease-in: the body tips slowly, then drops.
            var t = Mathf.Clamp01(elapsed / collapseTime);
            t *= t;
            model.SetLocalPositionAndRotation(
                Vector3.Lerp(collapseStartPosition, Vector3.up * lyingLift, t),
                Quaternion.Slerp(collapseStartRotation, Quaternion.Euler(lyingAngle, 0f, 0f), t));

            if (elapsed >= restartDelay && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) Respawn();
        }

        void Respawn()
        {
            fadeOutFrom = DeathFade();
            fadeOutStart = Time.time;
            dead = false;
            controller.StandUp();
            controller.Teleport(spawnPosition, spawnRotation);
            health.Restore();
            stamina.ResetFull();
            model.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            animator.enabled = true;
            controller.InputEnabled = true;
            spawnTime = Time.time;
            Respawned?.Invoke();
            PlayerRespawned?.Invoke();
        }

        float DeathFade() => Mathf.Clamp01((Time.time - deathTime - fadeDelay) / fadeTime);

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            var alpha = dead ? DeathFade() : fadeOutFrom * (1f - Mathf.Clamp01((Time.time - fadeOutStart) / fadeOutTime));
            if (alpha <= 0f) return;

            var color = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, alpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            if (dead) DrawDeathText(alpha);
            GUI.color = color;
        }

        void DrawDeathText(float alpha)
        {
            titleStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 42, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.75f, 0.1f, 0.08f) } };
            lineStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 20, normal = { textColor = new Color(0.85f, 0.85f, 0.85f) } };
            var centerY = Screen.height * 0.45f;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(new Rect(0f, centerY - 50f, Screen.width, 60f), "Vous êtes mort", titleStyle);
            GUI.Label(new Rect(0f, centerY + 10f, Screen.width, 30f), survivedText, lineStyle);
            if (Time.time - deathTime >= restartDelay)
                GUI.Label(new Rect(0f, centerY + 60f, Screen.width, 30f), "[Espace] Recommencer", lineStyle);
        }

        // Domain reload is disabled on entering Play mode, so subscribers from the previous session must be dropped by hand.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => PlayerRespawned = null;
    }
}
