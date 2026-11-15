using System;
using DeadCounty.AI;
using DeadCounty.Combat;
using DeadCounty.Core;
using DeadCounty.Items;
using DeadCounty.Perception;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeadCounty.Player
{
    // Attack swings the weapon in hand, or shoves with bare hands. The wind-up roots the player facing where the
    // camera looks; the blow lands when it ends, on the nearest zombies in front. Every swing costs stamina, an
    // exhausted one is weak, a blow that lands is heard, and being hit breaks a wind-up.
    [RequireComponent(typeof(ThirdPersonController), typeof(PlayerHealth), typeof(PlayerStamina))]
    [RequireComponent(typeof(PlayerInventory), typeof(PlayerInteractor), typeof(PlayerNoiseEmitter))]
    public class PlayerCombat : MonoBehaviour
    {
        const float ChestHeight = 1.2f;
        const float BodyHeight = 1.8f;
        const float ZombieRadius = 0.3f;
        const int MaxCandidates = 16;
        // A press during the end of a recovery still counts once the arm is free.
        const float PressBuffer = 0.2f;
        // The player stays rooted this long past the blow, through the follow-through.
        const float RootAfterBlow = 0.12f;

        [SerializeField] WeaponStats bareHands = WeaponStats.Shove;
        [Tooltip("Damage factor against a zombie that has noticed nothing.")]
        [SerializeField] float surpriseFactor = 2f;

        readonly MeleeSwing swing = new();
        readonly Collider[] overlaps = new Collider[MaxCandidates];
        readonly ZombieHealth[] candidates = new ZombieHealth[MaxCandidates];
        readonly Vector3[] positions = new Vector3[MaxCandidates];
        readonly int[] hits = new int[MaxCandidates];

        ThirdPersonController controller;
        PlayerHealth health;
        PlayerStamina stamina;
        PlayerInventory inventory;
        PlayerInteractor interactor;
        PlayerNoiseEmitter noise;
        InputAction attack;
        CursorLockMode lastLock;
        float pressedAt = float.NegativeInfinity, swingStart;
        bool wasFree;

        public MeleeSwing Swing => swing;
        // The weapon of the last swing started, null for a shove.
        public ItemDefinition Weapon { get; private set; }
        // Wind-up started, with the weapon swung (null for a shove).
        public event Action<ItemDefinition> Swung;
        // The blow landed or cut the air: zombies hit, zombies killed.
        public event Action<int, int> Struck;

        void Awake()
        {
            controller = GetComponent<ThirdPersonController>();
            health = GetComponent<PlayerHealth>();
            stamina = GetComponent<PlayerStamina>();
            inventory = GetComponent<PlayerInventory>();
            interactor = GetComponent<PlayerInteractor>();
            noise = GetComponent<PlayerNoiseEmitter>();
            attack = InputSystem.actions.FindAction("Player/Attack", true);
            lastLock = Cursor.lockState;
        }

        void Update()
        {
            var free = health.IsAlive && controller.Controllable && !interactor.Busy;
            // The click that closes a menu or takes the cursor back is not an attack.
            var locked = Cursor.lockState;
            if (free && wasFree && locked == lastLock && attack.WasPressedThisFrame()) pressedAt = Time.time;
            wasFree = free;
            lastLock = locked;

            if (swing.Busy && (!free || (swing.Phase == SwingPhase.Windup && health.LastHitTime >= swingStart))) swing.Cancel();
            if (free && !swing.Busy && Time.time - pressedAt <= PressBuffer) Begin();
            if (swing.Tick(Time.deltaTime)) Strike();
        }

        // Starts a swing whatever the input, for tests and tools. False while one is under way.
        public bool Begin()
        {
            if (swing.Busy) return false;
            Weapon = inventory.Weapon;
            var stats = Weapon != null ? Weapon.Weapon : bareHands;
            var model = stamina.Model;
            if (model.Exhausted) stats = stats.Tired();
            model.Exert(stats.StaminaCost);
            swing.Start(stats);
            swingStart = Time.time;
            pressedAt = float.NegativeInfinity;
            controller.Commit(stats.Windup + RootAfterBlow, controller.AimYaw);
            Swung?.Invoke(Weapon);
            return true;
        }

        void Strike()
        {
            var stats = swing.Stats;
            var origin = transform.position;
            var chest = origin + Vector3.up * ChestHeight;
            var found = Physics.OverlapCapsuleNonAlloc(origin, origin + Vector3.up * BodyHeight, stats.Reach + ZombieRadius, overlaps, 1 << GameLayers.Zombie, QueryTriggerInteraction.Ignore);
            var count = 0;
            for (var i = 0; i < found; i++)
            {
                var zombie = overlaps[i].GetComponentInParent<ZombieHealth>();
                if (zombie == null || !zombie.IsAlive || Array.IndexOf(candidates, zombie, 0, count) >= 0) continue;
                var position = zombie.transform.position;
                // Nothing lands through a wall or a shut door.
                if (Physics.Linecast(chest, position + Vector3.up * ChestHeight, GameLayers.EnvironmentMask, QueryTriggerInteraction.Ignore)) continue;
                candidates[count] = zombie;
                positions[count++] = position;
            }
            var landed = MeleeResolver.Select(stats, origin, transform.forward, positions.AsSpan(0, count), ZombieRadius, hits);
            var killed = 0;
            for (var i = 0; i < landed; i++)
            {
                var zombie = candidates[hits[i]];
                var unaware = zombie.TryGetComponent<ZombieAgent>(out var agent) && agent.State is ZombieState.Idle or ZombieState.Wander or ZombieState.Feed;
                zombie.ApplyDamage(stats.Damage * (unaware ? surpriseFactor : 1f), origin, stats.Stagger, stats.Knockback);
                if (!zombie.IsAlive) killed++;
            }
            Array.Clear(candidates, 0, count);
            if (landed > 0) NoiseSystem.Emit(origin, stats.NoiseRadius, NoiseKind.Impact, noise.SourceId);
            Struck?.Invoke(landed, killed);
        }
    }
}
