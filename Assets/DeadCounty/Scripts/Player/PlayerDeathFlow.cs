using System;
using DeadCounty.Animation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeadCounty.Player
{
    // Death and respawn loop: on death the body collapses and the screen fades to black (the HUD draws the fade,
    // the survival time and the prompt from the state exposed here), then Jump respawns the player at the pose
    // recorded in Start with full health, stamina and needs and the starting items. Lives on the rig root so it
    // keeps running if the Player object gets disabled.
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
        PlayerNeeds needs;
        PlayerInventory inventory;
        ProceduralHumanoidAnimator animator;
        InputAction jump;
        Transform model;
        Vector3 spawnPosition, collapseStartPosition;
        Quaternion spawnRotation, collapseStartRotation;
        float spawnTime, deathTime, fadeOutStart = float.NegativeInfinity, fadeOutFrom;
        bool dead;

        public static event Action PlayerRespawned;

        public bool IsDead => dead;
        public float SurvivedSeconds { get; private set; }
        public bool CanRespawn => dead && Time.time - deathTime >= restartDelay;
        // Opacity of the black screen: fades in after the collapse, back out after the respawn.
        public float ScreenFade => dead ? DeathFade() : fadeOutFrom * (1f - Mathf.Clamp01((Time.time - fadeOutStart) / fadeOutTime));

        void Awake()
        {
            controller = Require<ThirdPersonController>();
            health = Require<PlayerHealth>();
            stamina = Require<PlayerStamina>();
            needs = Require<PlayerNeeds>();
            inventory = Require<PlayerInventory>();
            animator = Require<ProceduralHumanoidAnimator>();
            jump = InputSystem.actions.FindAction("Player/Jump", true);
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
            SurvivedSeconds = deathTime - spawnTime;
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

            if (CanRespawn && jump.WasPressedThisFrame()) Respawn();
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
            needs.ResetFull();
            inventory.ResetFull();
            model.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            animator.enabled = true;
            controller.InputEnabled = true;
            spawnTime = Time.time;
            PlayerRespawned?.Invoke();
        }

        float DeathFade() => Mathf.Clamp01((Time.time - deathTime - fadeDelay) / fadeTime);

        // Domain reload is disabled on entering Play mode, so subscribers from the previous session must be dropped by hand.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => PlayerRespawned = null;
    }
}
