using System.Collections.Generic;
using DeadCounty.Items;
using DeadCounty.Player;
using DeadCounty.UI;
using UnityEngine;

namespace DeadCounty.Audio
{
    // What the survivor handles: the rummage loop while searching furniture (faster and louder when hurried), items
    // picked up, used (eaten, drunk, bandaged) and dropped, the flashlight switch and lying down on a bed.
    [RequireComponent(typeof(PlayerInteractor), typeof(PlayerInventory))]
    public class PlayerActionAudio : MonoBehaviour
    {
        const float HandsHeight = 1.1f;
        const float HeadHeight = 1.6f;
        const float DropDistance = 0.6f;
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

        PlayerInteractor interactor;
        PlayerInventory inventory;
        PlayerFlashlight flashlight;
        PlayerSleep sleep;
        AudioSource rummageSource;
        float rummageVolume, lastProgress;
        bool flashlightWasOn, wasAsleep;

        public bool Rummaging => rummageSource != null && rummageSource.isPlaying;

        void Awake()
        {
            interactor = GetComponent<PlayerInteractor>();
            inventory = GetComponent<PlayerInventory>();
            flashlight = GetComponent<PlayerFlashlight>();
            sleep = GetComponent<PlayerSleep>();
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
        }

        void OnDisable()
        {
            inventory.Received -= OnReceived;
            inventory.Used -= OnUsed;
            inventory.Dropped -= OnDropped;
            GameSettings.Changed -= ConfigureRummage;
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

        void OnDropped(ItemDefinition item) => Sfx.Play(drop, transform.position + transform.forward * DropDistance);
    }
}
