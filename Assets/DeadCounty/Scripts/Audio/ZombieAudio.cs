using DeadCounty.AI;
using DeadCounty.Animation;
using DeadCounty.UI;
using UnityEngine;

namespace DeadCounty.Audio
{
    // Everything a zombie sounds like. Its voice (one AudioSource that follows it) reacts to the brain's
    // transitions and speaks now and then on its own, through a shared limiter of ZombieVocals.MaxVoices voices;
    // the attack, its feet and the doors it pushes play pooled one-shots. Silent while dormant or dead.
    // Runs after the animator's LateUpdate, which sets the feet.
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ZombieAgent), typeof(AudioSource))]
    public class ZombieAudio : MonoBehaviour
    {
        const float MinStepSpeed = 0.15f;
        // Chance that a sound leg drags too as it swings forward.
        const float ShuffleChance = 0.12f;
        const float BadFootLanding = 0.55f;
        const float ShuffleVolume = 0.5f;
        const float ChestHeight = 1.2f;
        const float StrikeReach = 0.8f;
        const float BangHeight = 1.1f;
        const float BangIntervalMin = 0.45f;
        const float BangIntervalMax = 0.9f;
        // Zombies set off by the same call answer it one after the other, not on the same frame.
        const float AnswerDelayMax = 0.4f;

        static readonly VoiceLimiter<ZombieAudio> Voices = new(ZombieVocals.MaxVoices);

        [Header("Voice")]
        [SerializeField] SoundCue moan, breath, alert, sniff, growl, call, windup, frustrated;
        [Header("Attack")]
        [SerializeField] SoundCue whoosh, hit, miss;
        [Header("Body")]
        [SerializeField] SoundCue stepSoft, stepHard, stepGravel, dragSoft, dragHard, doorBang;

        ZombieAgent agent;
        ProceduralHumanoidAnimator animator;
        AudioSource voice;
        ZombieVoice identity;
        System.Random random;
        SoundCue speaking;
        AttackPhase lastPhase;
        float nextVocal, nextBang, answerAt = float.PositiveInfinity;
        bool leftPlanted, rightPlanted;

        public ZombieVoice Identity => identity;
        public bool IsSpeaking => Voices.IsSpeaking(this, Time.time);

        void Awake()
        {
            agent = GetComponent<ZombieAgent>();
            animator = GetComponent<ProceduralHumanoidAnimator>();
            voice = GetComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.loop = false;
            identity = ZombieVoice.FromSeed(agent.Seed);
            random = new System.Random(agent.Seed * 7349 + 101);
        }

        void OnEnable()
        {
            AIEvents.Transition += OnTransition;
            AIEvents.Strike += OnStrike;
            GameSettings.Changed += OnSettingsChanged;
            nextVocal = Time.time + ZombieVocals.FirstDelay(random);
        }

        void OnDisable()
        {
            AIEvents.Transition -= OnTransition;
            AIEvents.Strike -= OnStrike;
            GameSettings.Changed -= OnSettingsChanged;
            Silence();
        }

        void LateUpdate()
        {
            if (agent.IsDead || agent.Lod == ZombieLod.Dormant)
            {
                answerAt = float.PositiveInfinity;
                if (speaking != null) Silence();
                return;
            }
            var now = Time.time;
            var distance = DistanceToListener();
            var phase = agent.Brain.AttackPhase;
            if (phase == AttackPhase.Lunge && lastPhase == AttackPhase.Windup)
                OneShot(whoosh, transform.position + Vector3.up * ChestHeight + transform.forward * 0.4f, distance);
            lastPhase = phase;
            if (now >= answerAt)
            {
                answerAt = float.PositiveInfinity;
                Speak(ZombieVocal.Alert, distance);
            }
            if (now >= nextVocal)
            {
                var state = agent.State;
                if (ZombieVocals.TryPickAmbient(state, random, out var vocal)) Speak(vocal, distance);
                nextVocal = now + ZombieVocals.NextDelay(state, random);
            }
            if (animator != null && agent.Lod == ZombieLod.Near) Footsteps(distance);
            BangOnDoor(now, distance);
        }

        void OnTransition(ZombieTransitionEvent e)
        {
            if (e.AgentId != agent.AgentId) return;
            answerAt = float.PositiveInfinity;
            if (e.Reason == TransitionReason.Reset) Silence();
            else if (agent.Lod != ZombieLod.Dormant)
            {
                var distance = DistanceToListener();
                switch (e.To)
                {
                    case ZombieState.Alert when e.Reason == TransitionReason.Called:
                        answerAt = Time.time + AnswerDelayMax * (float)random.NextDouble();
                        break;
                    case ZombieState.Alert:
                        Speak(ZombieVocal.Alert, distance);
                        break;
                    case ZombieState.Investigate:
                        Speak(ZombieVocal.Sniff, distance);
                        break;
                    // The growl that alerts the zombies around is the noise raised with this very transition.
                    case ZombieState.Chase when e.From != ZombieState.Attack:
                        Speak(agent.Commands.EmitCall ? ZombieVocal.Call : ZombieVocal.Growl, distance);
                        break;
                    case ZombieState.Attack:
                        Speak(ZombieVocal.Windup, distance);
                        break;
                    case ZombieState.Search when e.Reason == TransitionReason.LostTarget:
                        Speak(ZombieVocal.Frustrated, distance);
                        break;
                }
            }
            if (e.Reason == TransitionReason.Reset || !ZombieVocals.SameCadence(e.From, e.To))
                nextVocal = Time.time + ZombieVocals.NextDelay(e.To, random);
        }

        void OnStrike(ZombieStrikeEvent e)
        {
            if (e.AgentId != agent.AgentId) return;
            var at = e.Origin + Vector3.up * ChestHeight + (e.Hit ? e.Direction * StrikeReach : Vector3.zero);
            OneShot(e.Hit ? hit : miss, at, DistanceToListener());
        }

        void OnSettingsChanged()
        {
            if (speaking != null && voice.isPlaying) Sfx.Configure(voice, speaking);
        }

        void Speak(ZombieVocal vocal, float distance)
        {
            var cue = Cue(vocal);
            if (cue == null || cue.clips == null || cue.clips.Length == 0 || distance > cue.maxDistance) return;
            var clip = cue.clips[identity.PickClip(cue.clips.Length, random)];
            if (clip == null) return;
            var pitch = (vocal == ZombieVocal.Windup ? identity.TelegraphPitch : identity.Pitch) * cue.RandomPitch();
            var now = Time.time;
            if (!Voices.TryAcquire(this, ZombieVocals.Score(vocal, distance, cue.maxDistance), now, now + clip.length / pitch, out var evicted)) return;
            if (evicted != null) evicted.Silence();
            Sfx.Configure(voice, cue);
            voice.pitch = pitch;
            voice.clip = clip;
            voice.Play();
            speaking = cue;
        }

        void Silence()
        {
            if (voice != null && voice.isPlaying) voice.Stop();
            speaking = null;
            Voices.Release(this);
        }

        // A planted foot lands; a bad leg drags as it swings forward, a sound one now and then.
        void Footsteps(float distance)
        {
            var left = animator.LeftFootPlanted;
            var right = animator.RightFootPlanted;
            if (agent.CurrentSpeed >= MinStepSpeed && stepSoft != null && distance <= stepSoft.maxDistance)
            {
                var limp = animator.Limp;
                Foot(leftPlanted, left, limp.Enabled && limp.LeftIsBad, distance);
                Foot(rightPlanted, right, limp.Enabled && !limp.LeftIsBad, distance);
            }
            leftPlanted = left;
            rightPlanted = right;
        }

        void Foot(bool was, bool planted, bool bad, float distance)
        {
            if (was == planted) return;
            if (!planted && !bad && random.NextDouble() >= ShuffleChance) return;
            var surface = GroundSurface.At(transform.position);
            var kind = ZombieVocals.StepKindOf(surface);
            var pitch = ZombieVocals.StepPitch(surface);
            if (planted)
                OneShot(kind switch { StepKind.Soft => stepSoft, StepKind.Gravel => stepGravel, _ => stepHard }, transform.position, distance, bad ? BadFootLanding : 1f, pitch);
            else
                OneShot(kind == StepKind.Soft ? dragSoft : dragHard, transform.position, distance, bad ? 1f : ShuffleVolume, pitch);
        }

        void BangOnDoor(float now, float distance)
        {
            var door = agent.PushedDoor;
            if (door == null || now < nextBang) return;
            nextBang = now + Mathf.Lerp(BangIntervalMin, BangIntervalMax, (float)random.NextDouble());
            OneShot(doorBang, door.Doorway.Center + Vector3.up * BangHeight, distance);
        }

        static void OneShot(SoundCue cue, Vector3 position, float distance, float volume = 1f, float pitch = 1f)
        {
            if (cue != null && distance <= cue.maxDistance) Sfx.Play(cue, position, volume, pitch);
        }

        // Planar distance to the zombies' target, the player the camera follows: the same measure as the LOD tiers.
        float DistanceToListener()
        {
            var manager = ZombieManager.Instance;
            var target = manager != null ? manager.Target : null;
            if (target == null) return 0f;
            var offset = target.Position - transform.position;
            offset.y = 0f;
            return offset.magnitude;
        }

        SoundCue Cue(ZombieVocal vocal) => vocal switch
        {
            ZombieVocal.Moan => moan,
            ZombieVocal.Breath => breath,
            ZombieVocal.Alert => alert,
            ZombieVocal.Sniff => sniff,
            ZombieVocal.Growl => growl,
            ZombieVocal.Call => call,
            ZombieVocal.Windup => windup,
            _ => frustrated,
        };

        // Domain reload is disabled on entering Play mode: voices of the previous session must be dropped by hand.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Voices.Clear();
    }
}
