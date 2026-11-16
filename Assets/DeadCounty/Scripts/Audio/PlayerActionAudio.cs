using System;
using System.Collections.Generic;
using DeadCounty.Items;
using DeadCounty.Player;
using DeadCounty.UI;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DeadCounty.Audio
{
    // Weapons carry their own swing and blow (ItemDefinition), bare hands shove, a blow at the ground stomps, and a
    // blow that kills cracks on top of its own sound.
    [RequireComponent(typeof(PlayerInteractor), typeof(PlayerInventory))]
    public class PlayerActionAudio : MonoBehaviour
    {
        const float HandsHeight = 1.1f;
        const float HeadHeight = 1.6f;
        const float DropDistance = 0.6f;
        const float StrikeDistance = 1f;
        const float GroundHeight = 0.2f;
        // Stamina cost of a swing that always draws a grunt; lighter swings draw one less often.
        const float EffortCost = 14f;
        // Progress per second above which a search is hurried (1 s instead of 2.5 s by default).
        const float HurriedProgressRate = 0.7f;
        const float RummageFadeOut = 8f;

        [SerializeField] SoundCue rummage;
        [SerializeField] SoundCue pickup;
        [SerializeField] SoundCue eat;
        [SerializeField] SoundCue drink;
        [SerializeField] SoundCue bandage;
        [SerializeField] SoundCue drop;
        [SerializeField] SoundCue flashlightClick;
        [SerializeField] SoundCue bed;
        [Tooltip("A weapon's swing without its own, and the shove.")]
        [SerializeField] SoundCue swing;
        [Tooltip("A weapon's blow without its own.")]
        [SerializeField] SoundCue strike;
        [SerializeField] SoundCue shove;
        [SerializeField] SoundCue stomp;
        [SerializeField] SoundCue kill;
        [SerializeField] SoundCue effort;

        PlayerInteractor interactor;
        PlayerInventory inventory;
        PlayerFlashlight flashlight;
        PlayerSleep sleep;
        PlayerCombat combat;
        AudioSource rummageSource;
        float rummageVolume, lastProgress;
        bool flashlightWasOn, wasAsleep;

        // Every combat cue played, with where: lets tests hear the fight without an AudioListener.
        public event Action<SoundCue, Vector3> Played;

        void Awake()
        {
            interactor = GetComponent<PlayerInteractor>();
            inventory = GetComponent<PlayerInventory>();
            flashlight = GetComponent<PlayerFlashlight>();
            sleep = GetComponent<PlayerSleep>();
            combat = GetComponent<PlayerCombat>();
            var holder = new GameObject("RummageAudio");
            holder.transform.SetParent(transform, false);
            rummageSource = holder.AddComponent<AudioSource>();
            rummageSource.playOnAwake = false;
            rummageSource.loop = true;
            ConfigureRummage();
        }

        void OnEnable()
        {
            inventory.Received += OnReceived;
            inventory.Used += OnUsed;
            inventory.Dropped += OnDropped;
            GameSettings.Changed += ConfigureRummage;
            if (combat == null) return;
            combat.Swung += OnSwung;
            combat.Struck += OnStruck;
        }

        void OnDisable()
        {
            inventory.Received -= OnReceived;
            inventory.Used -= OnUsed;
            inventory.Dropped -= OnDropped;
            GameSettings.Changed -= ConfigureRummage;
            if (combat != null)
            {
                combat.Swung -= OnSwung;
                combat.Struck -= OnStruck;
            }
            // Already destroyed with its holder when the scene unloads.
            if (rummageSource != null) rummageSource.Stop();
        }

        void Start()
        {
            flashlightWasOn = flashlight != null && flashlight.IsOn;
            wasAsleep = sleep != null && sleep.Asleep;
        }

        void Update()
        {
            UpdateRummage();
            if (flashlight != null && flashlight.IsOn != flashlightWasOn)
            {
                flashlightWasOn = flashlight.IsOn;
                Sfx.Play(flashlightClick, transform.position + Vector3.up * HeadHeight, 1f, flashlightWasOn ? 1.08f : 0.94f);
            }
            if (sleep != null && sleep.Asleep != wasAsleep)
            {
                wasAsleep = sleep.Asleep;
                Sfx.Play(bed, transform.position + Vector3.up * 0.5f, wasAsleep ? 1f : 0.6f);
            }
        }

        void UpdateRummage()
        {
            var container = interactor.Busy ? interactor.Current as LootContainer : null;
            var progress = interactor.UseProgress;
            var rate = Time.deltaTime > 0f ? (progress - lastProgress) / Time.deltaTime : 0f;
            lastProgress = progress;
            if (container == null)
            {
                if (!rummageSource.isPlaying) return;
                rummageSource.volume = Mathf.MoveTowards(rummageSource.volume, 0f, RummageFadeOut * rummageVolume * Time.unscaledDeltaTime);
                if (rummageSource.volume <= 0f) rummageSource.Stop();
                return;
            }
            if (rummage == null) return;
            var hurried = rate > HurriedProgressRate;
            if (!rummageSource.isPlaying)
            {
                rummageSource.clip = rummage.Pick();
                rummageSource.transform.position = container.PromptPoint + Vector3.down * 0.4f;
                // A random start, so two searches in a row do not begin with the same handful.
                rummageSource.time = Random.Range(0f, rummageSource.clip.length * 0.9f);
                rummageSource.Play();
            }
            rummageSource.pitch = hurried ? 1.2f : 1f;
            rummageSource.volume = rummageVolume * (hurried ? 1.3f : 1f);
        }

        void ConfigureRummage()
        {
            if (rummage == null) return;
            Sfx.Configure(rummageSource, rummage);
            rummageVolume = rummageSource.volume;
        }

        // Empty when an empty piece of furniture was searched: nothing to put away.
        void OnReceived(IReadOnlyList<ItemStack> items)
        {
            if (items.Count > 0) Sfx.Play(pickup, transform.position + Vector3.up * HandsHeight);
        }

        void OnUsed(ItemDefinition item)
        {
            var cue = item.Category switch
            {
                ItemCategory.Food => eat,
                ItemCategory.Drink => drink,
                ItemCategory.Medical => bandage,
                _ => pickup,
            };
            Sfx.Play(cue, transform.position + Vector3.up * HeadHeight);
        }

        // A shove cuts less air than a weapon, a stomp none. Heavier swings draw a grunt more often, a stomp always.
        void OnSwung(ItemDefinition weapon)
        {
            var stomping = weapon == null && combat.Ground;
            var hands = transform.position + Vector3.up * HandsHeight + transform.forward * 0.4f;
            if (weapon != null) Play(Either(weapon.SwingSound, swing), hands);
            else if (!stomping) Play(swing, hands, 0.5f, 0.8f);
            if (stomping || Random.value < combat.Swing.Stats.StaminaCost / EffortCost)
                Play(effort, transform.position + Vector3.up * HeadHeight);
        }

        void OnStruck(int hits, int kills)
        {
            if (hits == 0) return;
            var weapon = combat.Weapon;
            var at = transform.position + transform.forward * StrikeDistance + Vector3.up * (combat.Ground ? GroundHeight : HandsHeight);
            Play(weapon != null ? Either(weapon.HitSound, strike) : combat.Ground ? stomp : shove, at);
            if (kills > 0) Play(kill, at);
        }

        void Play(SoundCue cue, Vector3 at, float volume = 1f, float pitch = 1f)
        {
            if (cue == null) return;
            Sfx.Play(cue, at, volume, pitch);
            Played?.Invoke(cue, at);
        }

        static SoundCue Either(SoundCue own, SoundCue fallback) => own != null ? own : fallback;

        void OnDropped(ItemDefinition item) => Sfx.Play(drop, transform.position + transform.forward * DropDistance);
    }
}
