using System;
using System.Collections.Generic;
using DeadCounty.Core;
using DeadCounty.Perception;
using UnityEngine;

namespace DeadCounty.World
{
    // Hinged door in a wall opening (GDD 8.2): Interact opens it away from the player or closes it, a sprinting
    // player slams it, zombies force it open after 5 s of pushing, several pushing together. This transform is
    // the hinge: local +Z runs along the opening, a positive angle swings the leaf toward local +X. Closed doors
    // are left out of the NavMesh, so paths cross them and ZombieAgent stops in front of a shut one.
    public class Door : Interactable
    {
        // Past this angle zombies walk through and the door no longer blocks them.
        const float ShutAngle = 45f;
        const float PromptHeight = 1.3f;
        const float RattleAngle = 1.5f;
        const float RattleFrequency = 40f;
        const float RattleTime = 0.2f;
        // The doorway must be clear this far on each side for the door to close.
        const float ClearanceDepth = 0.35f;
        const int OccupantMask = (1 << GameLayers.Player) | (1 << GameLayers.Zombie);

        static readonly List<Door> all = new();

        [SerializeField] Transform leaf;
        [SerializeField] float width = 1f;
        [SerializeField] float height = 2.1f;
        [Tooltip("Angle at load: 0 closed, positive toward local +X (deg).")]
        [SerializeField] float startAngle;

        [Header("Swing")]
        [SerializeField] float openAngle = 95f;
        [SerializeField] float swingTime = 0.5f;
        [SerializeField] float slamTime = 0.15f;
        [SerializeField] float forcedSwingTime = 0.3f;

        [Header("Zombies")]
        [Tooltip("Push time that forces the door open; the pushes of several zombies add up (s).")]
        [SerializeField] float forceTime = 5f;
        [Tooltip("Push time lost per second while no zombie pushes.")]
        [SerializeField] float forceRecovery = 1f;

        [Header("Noise radius (m)")]
        [SerializeField] float useNoise = 3f;
        [SerializeField] float slamNoise = 12f;
        [SerializeField] float forcedNoise = 6f;

        float angle, targetAngle, swingSpeed, push, pushSide;
        float lastPushTime = float.NegativeInfinity;

        public Doorway Doorway { get; private set; }
        // Source id of the door's noises.
        public int SourceId { get; private set; }
        public float Angle => angle;
        public bool IsShut => Mathf.Abs(angle) < ShutAngle;
        // Share of the push time already spent forcing the door.
        public float ForceProgress => push / forceTime;

        public override string DisplayName => "Porte";
        public override string Action => targetAngle == 0f ? "Ouvrir" : "Fermer";
        public override Vector3 PromptPoint => Doorway.Center + Vector3.up * PromptHeight;

        void Awake()
        {
            if (leaf == null) throw new InvalidOperationException($"Door '{name}' has no leaf");
            Doorway = new Doorway(transform.position + transform.forward * (width * 0.5f), transform.right, transform.forward, width * 0.5f);
            SourceId = gameObject.GetEntityId().GetHashCode();
            angle = targetAngle = startAngle;
            leaf.localRotation = Quaternion.Euler(0f, angle, 0f);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            all.Add(this);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            all.Remove(this);
        }

        public override void Interact(Transform user, bool hurried)
        {
            var time = hurried ? slamTime : swingTime;
            var noise = hurried ? slamNoise : useNoise;
            if (targetAngle == 0f) Swing(-Doorway.Side(user.position) * openAngle, time, noise);
            else if (DoorwayClear()) Swing(0f, time, noise);
        }

        // Called every frame by each zombie pushing from `from`.
        public void Push(Vector3 from, float dt)
        {
            if (targetAngle != 0f) return;
            pushSide = Doorway.Side(from);
            lastPushTime = Time.time;
            push += dt;
            if (push < forceTime) return;
            push = 0f;
            Swing(-pushSide * openAngle, forcedSwingTime, forcedNoise);
        }

        // The first shut door crossed by the ground segment from → to, or null.
        public static Door FindBlocking(Vector3 from, Vector3 to)
        {
            for (var i = 0; i < all.Count; i++)
            {
                var door = all[i];
                if (door.IsShut && door.Doorway.Crosses(from, to)) return door;
            }
            return null;
        }

        void Swing(float target, float time, float noise)
        {
            swingSpeed = Mathf.Abs(target - angle) / time;
            targetAngle = target;
            NoiseSystem.Emit(Doorway.Center, noise, NoiseKind.Door, SourceId);
        }

        bool DoorwayClear()
        {
            var center = Doorway.Center + Vector3.up * (height * 0.5f);
            var halfExtents = new Vector3(ClearanceDepth, height * 0.5f, width * 0.5f);
            return !Physics.CheckBox(center, halfExtents, transform.rotation, OccupantMask, QueryTriggerInteraction.Ignore);
        }

        void Update()
        {
            var dt = Time.deltaTime;
            angle = Mathf.MoveTowards(angle, targetAngle, swingSpeed * dt);
            var pushed = Time.time - lastPushTime < RattleTime;
            if (!pushed) push = Mathf.Max(push - forceRecovery * dt, 0f);
            // Pushed, the leaf knocks in its frame away from the zombies.
            var rattle = pushed ? -pushSide * RattleAngle * Mathf.Abs(Mathf.Sin(Time.time * RattleFrequency)) : 0f;
            leaf.localRotation = Quaternion.Euler(0f, angle + rattle, 0f);
        }

        // Domain reload is disabled on entering Play mode: doors of the previous session must be dropped by hand.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => all.Clear();
    }
}
