using System;
using DeadCounty.AI;
using DeadCounty.Core;
using DeadCounty.Perception;
using UnityEngine;

namespace DeadCounty.Player
{
    // Sleeping in a bed (GDD 8.4), only once fatigue is under half: time runs 20 times faster until fatigue is full
    // or morning comes, hunger and thirst drain at half rate and health comes back while nothing hurts. The zombies
    // keep wandering meanwhile: one coming close, a noise nearby or a hit wakes the player.
    [RequireComponent(typeof(ThirdPersonController), typeof(PlayerHealth), typeof(PlayerNeeds))]
    [RequireComponent(typeof(PlayerNoiseEmitter))]
    public class PlayerSleep : MonoBehaviour
    {
        const int NoiseBatch = 16;

        [SerializeField] float timeScale = 20f;
        [Tooltip("Fatigue under which the player may sleep (GDD: under 50 %, so a night cannot be skipped at will).")]
        [SerializeField, Range(0f, 1f)] float sleepBelow = 0.5f;
        [Tooltip("Health regained per second of game time while asleep and unhurt.")]
        [SerializeField] float healPerSecond = 0.2f;
        [Tooltip("Hour at which a sleeper wakes up whatever the fatigue.")]
        [SerializeField] float wakeHour = 7f;
        [Header("Waking")]
        [SerializeField] float wakeNoiseRadius = 10f;
        [Tooltip("A zombie this close wakes the player, roughly when it enters the room.")]
        [SerializeField] float wakeZombieDistance = 6f;

        readonly NoiseEvent[] noises = new NoiseEvent[NoiseBatch];
        ThirdPersonController controller;
        PlayerHealth health;
        PlayerNeeds needs;
        PlayerNoiseEmitter noiseEmitter;
        DayNightDirector director;
        int lastNoiseId;
        float lastHour;

        public bool Asleep { get; private set; }
        public bool CanSleep => !Asleep && health.IsAlive && needs.Model.Fatigue < sleepBelow;

        // Why the player woke up, for the HUD.
        public event Action<string> WokeUp;

        void Awake()
        {
            controller = GetComponent<ThirdPersonController>();
            health = GetComponent<PlayerHealth>();
            needs = GetComponent<PlayerNeeds>();
            noiseEmitter = GetComponent<PlayerNoiseEmitter>();
            director = FindAnyObjectByType<DayNightDirector>();
        }

        void OnEnable()
        {
            health.Damaged += OnDamaged;
            health.Died += OnDamaged;
        }

        void OnDisable()
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDamaged;
            if (Asleep) Wake(null);
        }

        void OnDamaged(PlayerHealth _)
        {
            if (Asleep) Wake("Réveillé en sursaut");
        }

        public bool TrySleep()
        {
            if (!CanSleep) return false;
            Asleep = true;
            needs.Asleep = true;
            controller.Resting = true;
            Time.timeScale = timeScale;
            lastNoiseId = NoiseSystem.Buffer.LatestId;
            lastHour = director != null ? director.Hour : 0f;
            return true;
        }

        void Wake(string reason)
        {
            Asleep = false;
            needs.Asleep = false;
            controller.Resting = false;
            Time.timeScale = 1f;
            if (reason != null) WokeUp?.Invoke(reason);
        }

        void Update()
        {
            if (!Asleep) return;
            var model = needs.Model;
            if (!health.Bleeding && model.Hunger > 0f && model.Thirst > 0f) health.Heal(healPerSecond * Time.deltaTime);

            if (model.Fatigue >= 1f) Wake("Reposé");
            else if (MorningCame()) Wake("Le jour se lève");
            else if (HeardNoise()) Wake("Réveillé par un bruit");
            else if (ZombieNearby()) Wake("Un zombie est tout près");
        }

        bool MorningCame()
        {
            if (director == null) return false;
            var hour = director.Hour;
            var crossed = lastHour <= hour ? lastHour < wakeHour && wakeHour <= hour : lastHour < wakeHour || wakeHour <= hour;
            lastHour = hour;
            return crossed;
        }

        bool HeardNoise()
        {
            var own = noiseEmitter.SourceId;
            var position = transform.position;
            int count;
            do
            {
                count = NoiseSystem.Buffer.CopySince(lastNoiseId, noises);
                for (var i = 0; i < count; i++)
                {
                    lastNoiseId = noises[i].Id;
                    if (noises[i].SourceId == own) continue;
                    if ((noises[i].Position - position).sqrMagnitude <= wakeNoiseRadius * wakeNoiseRadius) return true;
                }
            } while (count == noises.Length);
            return false;
        }

        bool ZombieNearby()
        {
            var manager = ZombieManager.Instance;
            if (manager == null) return false;
            var position = transform.position;
            var agents = manager.Agents;
            for (var i = 0; i < agents.Count; i++)
            {
                var agent = agents[i];
                if (agent.IsDead) continue;
                var offset = agent.Position - position;
                offset.y = 0f;
                if (offset.sqrMagnitude <= wakeZombieDistance * wakeZombieDistance) return true;
            }
            return false;
        }
    }
}
