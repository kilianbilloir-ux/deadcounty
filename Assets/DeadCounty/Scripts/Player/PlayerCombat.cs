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
    // camera looks; the blow lands when it ends, on the nearest zombies in front, and may fell them, more likely
    // from behind. When the nearest zombie in front is on the ground the attack comes down on it instead, a stomp
    // with bare hands, harder and keeping it there. Every swing costs stamina, an exhausted one is weak, a blow
    // that lands is heard, and being hit breaks a wind-up.
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
        // Line of sight to a body on the ground ends this high above its torso (m).
        const float LyingHeight = 0.3f;

        [SerializeField] WeaponStats bareHands = WeaponStats.Shove;
        [SerializeField] WeaponStats stomp = WeaponStats.Stomp;
        [Tooltip("Damage factor against a zombie that has noticed nothing.")]
        [SerializeField] float surpriseFactor = 2f;
        [Tooltip("Damage factor against a zombie on the ground.")]
        [SerializeField] float groundFactor = 2f;

        readonly MeleeSwing swing = new();
        readonly Collider[] overlaps = new Collider[MaxCandidates];
        readonly ZombieHealth[] candidates = new ZombieHealth[MaxCandidates];
        readonly ZombieAgent[] agents = new ZombieAgent[MaxCandidates];
        readonly bool[] lying = new bool[MaxCandidates];
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
        // The weapon of the last swing started, null for a shove or a stomp.
        public ItemDefinition Weapon { get; private set; }
        // The last swing started comes down on a zombie on the ground.
        public bool Ground { get; private set; }
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
            Ground = NearestIsDown(stats);
            if (Ground) stats = OnTheGround(Weapon != null ? Weapon.Weapon : stomp);
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

        // The swing aims where the camera looks: the wind-up turns the body there.
        bool NearestIsDown(WeaponStats stats)
        {
            var count = Gather(stats.Reach, null);
            stats.MaxTargets = 1;
            var aim = Quaternion.Euler(0f, controller.AimYaw, 0f) * Vector3.forward;
            var down = MeleeResolver.Select(stats, transform.position, aim, positions.AsSpan(0, count), ZombieRadius, hits) > 0 && lying[hits[0]];
            Release(count);
            return down;
        }

        WeaponStats OnTheGround(WeaponStats stats)
        {
            stats.Damage *= groundFactor;
            stats.MaxTargets = 1;
            stats.Stagger = 0f;
            stats.Knockback = 0f;
            stats.Knockdown = 0f;
            return stats;
        }

        void Strike()
        {
            var stats = swing.Stats;
            var origin = transform.position;
            // A swing across passes over a body on the ground, a blow at the ground misses whoever stands.
            var count = Gather(stats.Reach, Ground);
            var landed = MeleeResolver.Select(stats, origin, transform.forward, positions.AsSpan(0, count), ZombieRadius, hits);
            var killed = 0;
            for (var i = 0; i < landed; i++)
            {
                var zombie = candidates[hits[i]];
                var agent = agents[hits[i]];
                if (Ground) zombie.ApplyDamage(stats.Damage, origin, knockdown: true);
                else
                {
                    var unaware = agent != null && agent.State is ZombieState.Idle or ZombieState.Wander or ZombieState.Feed;
                    var fells = agent != null && MeleeResolver.Fells(stats.Knockdown, MeleeResolver.FromBehind(origin, positions[hits[i]], agent.Heading), UnityEngine.Random.value);
                    zombie.ApplyDamage(stats.Damage * (unaware ? surpriseFactor : 1f), origin, stats.Stagger, stats.Knockback, fells);
                }
                if (!zombie.IsAlive) killed++;
            }
            Release(count);
            if (landed > 0) NoiseSystem.Emit(origin, stats.NoiseRadius, NoiseKind.Impact, noise.SourceId);
            Struck?.Invoke(landed, killed);
        }

        // Living zombies within `reach` and in sight, only those on the ground or only those standing unless
        // `onGround` is null; a body on the ground is aimed at its torso.
        int Gather(float reach, bool? onGround)
        {
            var origin = transform.position;
            var chest = origin + Vector3.up * ChestHeight;
            var found = Physics.OverlapCapsuleNonAlloc(origin, origin + Vector3.up * BodyHeight, reach + ZombieRadius, overlaps, 1 << GameLayers.Zombie, QueryTriggerInteraction.Ignore);
            var count = 0;
            for (var i = 0; i < found; i++)
            {
                var zombie = overlaps[i].GetComponentInParent<ZombieHealth>();
                if (zombie == null || !zombie.IsAlive || Array.IndexOf(candidates, zombie, 0, count) >= 0) continue;
                zombie.TryGetComponent<ZombieAgent>(out var agent);
                var down = agent != null && agent.IsDown;
                if (onGround.HasValue && down != onGround.Value) continue;
                var position = down ? agent.BodyCenter : zombie.transform.position;
                // Nothing lands through a wall or a shut door.
                if (Physics.Linecast(chest, position + Vector3.up * (down ? LyingHeight : ChestHeight), GameLayers.EnvironmentMask, QueryTriggerInteraction.Ignore)) continue;
                candidates[count] = zombie;
                agents[count] = agent;
                lying[count] = down;
                positions[count++] = position;
            }
            return count;
        }

        void Release(int count)
        {
            Array.Clear(candidates, 0, count);
            Array.Clear(agents, 0, count);
        }
    }
}
