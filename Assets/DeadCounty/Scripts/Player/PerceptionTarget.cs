using System;
using DeadCounty.AI;
using UnityEngine;

namespace DeadCounty.Player
{
    // The player as the zombies perceive it: sight points that follow the crouch height, motion and visibility
    // flags, cached once per frame right after the controller has moved.
    [DefaultExecutionOrder(-90)]
    [RequireComponent(typeof(CharacterController), typeof(ThirdPersonController), typeof(PlayerHealth))]
    public class PerceptionTarget : MonoBehaviour, IPerceivable
    {
        const float HeadHeightRatio = 0.92f;
        const float ChestHeightRatio = 0.65f;

        [SerializeField] bool flashlightOn;

        CharacterController body;
        ThirdPersonController controller;
        PlayerHealth health;

        public static PerceptionTarget Player { get; private set; }

        public Vector3 Position { get; private set; }
        public Vector3 HeadPoint { get; private set; }
        public Vector3 ChestPoint { get; private set; }
        public Vector3 Velocity { get; private set; }
        public float Speed { get; private set; }
        public bool IsCrouching { get; private set; }
        public bool FlashlightOn
        {
            get => flashlightOn;
            set => flashlightOn = value;
        }
        // False in ghost mode: zombies neither see nor hear the player.
        public bool Visible { get; set; } = true;
        public bool IsAlive => health.IsAlive;

        public bool ApplyHit(float damage, Vector3 source) => health.ApplyHit(damage, source);

        void Awake()
        {
            body = GetComponent<CharacterController>();
            controller = GetComponent<ThirdPersonController>();
            health = GetComponent<PlayerHealth>();
        }

        void OnEnable()
        {
            if (Player != null && Player != this)
                throw new InvalidOperationException($"Second PerceptionTarget on '{name}', '{Player.name}' is already the player");
            Player = this;
            Cache();
        }

        void OnDisable()
        {
            if (Player == this) Player = null;
        }

        void Update() => Cache();

        void Cache()
        {
            var feet = transform.position;
            Position = feet;
            var height = body.height;
            HeadPoint = feet + Vector3.up * (HeadHeightRatio * height);
            ChestPoint = feet + Vector3.up * (ChestHeightRatio * height);
            Velocity = controller.Velocity;
            Speed = controller.CurrentSpeed;
            IsCrouching = controller.IsCrouching;
        }

        // Domain reload is disabled on entering Play mode, so the previous session's player must be dropped by hand.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Player = null;
    }
}
