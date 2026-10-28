using DeadCounty.Player;
using UnityEngine;

namespace DeadCounty.Audio
{
    // The survivor's body: breathing that follows the effort (spent stamina, sprinting, exhaustion) and keeps heaving
    // for a while after it, a heartbeat below a quarter of health, pain on hits and the death. Breathing and
    // heartbeat run on unscaled time so the sleep fast-forward does not race them; they stop while asleep or dead.
    // Presentation only: the panting zombies hear comes from PlayerNoiseEmitter.
    [RequireComponent(typeof(ThirdPersonController), typeof(PlayerStamina), typeof(PlayerHealth))]
    public class PlayerBodyAudio : MonoBehaviour
    {
        const float HeadHeight = 1.6f;
        const float AudibleEffort = 0.2f;
        const float EffortRise = 0.6f;
        const float EffortFall = 0.12f;

        [SerializeField] SoundCue breathIn;
        [SerializeField] SoundCue breathOut;
        [Tooltip("Exhale with a groan in it, while exhausted.")]
        [SerializeField] SoundCue pantOut;
        [SerializeField] SoundCue hurt;
        [SerializeField] SoundCue death;
        [SerializeField] SoundCue heartbeat;

        [Header("Breathing")]
        [Tooltip("Breath cycle (s) at the lowest audible effort and while exhausted.")]
        [SerializeField] float calmCycle = 3f;
        [SerializeField] float pantCycle = 0.75f;

        [Header("Heartbeat")]
        [SerializeField, Range(0f, 1f)] float heartbeatBelow = 0.25f;
        [Tooltip("Beats per minute at the threshold and near death.")]
        [SerializeField] float slowBpm = 75f;
        [SerializeField] float fastBpm = 130f;

        ThirdPersonController controller;
        PlayerStamina stamina;
        PlayerHealth health;
        float effort, nextBreath, nextBeat;
        bool inhaleNext = true;

        // 0 calm to 1 exhausted, smoothed; read by tests.
        public float Effort => effort;

        void Awake()
        {
            controller = GetComponent<ThirdPersonController>();
            stamina = GetComponent<PlayerStamina>();
            health = GetComponent<PlayerHealth>();
        }

        void OnEnable()
        {
            health.Damaged += OnDamaged;
            health.Died += OnDied;
            PlayerDeathFlow.PlayerRespawned += Calm;
        }

        void OnDisable()
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
            PlayerDeathFlow.PlayerRespawned -= Calm;
        }

        void Update()
        {
            if (Time.timeScale == 0f || !health.IsAlive || controller.Resting) return;
            var dt = Time.unscaledDeltaTime;
            Breathe(dt);
            Heartbeat(dt);
        }

        void Breathe(float dt)
        {
            var model = stamina.Model;
            var target = model.Exhausted ? 1f : Mathf.Clamp01(Mathf.InverseLerp(0.3f, 1f, 1f - model.Normalized) + (controller.IsSprinting ? 0.35f : 0f));
            effort = Mathf.MoveTowards(effort, target, dt * (target > effort ? EffortRise : EffortFall));
            if (effort < AudibleEffort)
            {
                nextBreath = 0f;
                inhaleNext = true;
                return;
            }
            nextBreath -= dt;
            if (nextBreath > 0f) return;

            var cycle = Mathf.Lerp(calmCycle, pantCycle, effort);
            var volume = Mathf.Lerp(0.25f, 1f, effort);
            var head = transform.position + Vector3.up * HeadHeight;
            if (inhaleNext)
            {
                Sfx.Play(breathIn, head, volume);
                nextBreath = cycle * 0.42f;
            }
            else
            {
                Sfx.Play(model.Exhausted ? pantOut : breathOut, head, volume);
                nextBreath = cycle * 0.58f;
            }
            inhaleNext = !inhaleNext;
        }

        void Heartbeat(float dt)
        {
            var share = health.Pool.Normalized;
            if (share >= heartbeatBelow)
            {
                nextBeat = 0f;
                return;
            }
            nextBeat -= dt;
            if (nextBeat > 0f) return;
            var danger = 1f - share / heartbeatBelow;
            Sfx.Play2D(heartbeat, Mathf.Lerp(0.4f, 1f, danger));
            nextBeat = 60f / Mathf.Lerp(slowBpm, fastBpm, danger);
        }

        // A killing hit raises Damaged then Died: only the death is heard.
        void OnDamaged(PlayerHealth _)
        {
            if (health.IsAlive) Sfx.Play(hurt, transform.position + Vector3.up * HeadHeight);
        }

        void OnDied(PlayerHealth _)
        {
            Sfx.Play(death, transform.position + Vector3.up * HeadHeight);
            Calm();
        }

        void Calm()
        {
            effort = 0f;
            nextBreath = nextBeat = 0f;
            inhaleNext = true;
        }
    }
}
