using DeadCounty.AI;
using UnityEngine;

namespace DeadCounty.Audio
{
    public enum ZombieVocal { Moan, Breath, Alert, Sniff, Growl, Call, Windup, Frustrated }

    // Who wins a voice slot: the tier first, then the closer zombie within a tier.
    public enum VocalPriority { Ambient, Searching, Hunting, Reaction, Telegraph }

    public enum StepKind { Soft, Hard, Gravel }

    // A zombie's own voice, drawn from its seed so it keeps it across resets: a pitch (a bigger or smaller throat)
    // and the two neighbouring variants of every cue it favours.
    public readonly struct ZombieVoice
    {
        public const float MinPitch = 0.86f;
        public const float MaxPitch = 1.14f;
        // The attack telegraph peaks when the lunge starts: its pitch, hence its length, barely varies.
        const float TelegraphSpread = 0.05f;

        public readonly float Pitch;
        public readonly int Variant;

        ZombieVoice(float pitch, int variant)
        {
            Pitch = pitch;
            Variant = variant;
        }

        public float TelegraphPitch => Mathf.Clamp(Pitch, 1f - TelegraphSpread, 1f + TelegraphSpread);

        // Integer finalizer (lowbias32): consecutive scene seeds still get unrelated voices.
        public static ZombieVoice FromSeed(int seed)
        {
            var h = unchecked((uint)seed);
            h ^= h >> 16;
            h = unchecked(h * 0x7feb352du);
            h ^= h >> 15;
            h = unchecked(h * 0x846ca68bu);
            h ^= h >> 16;
            var fraction = (h >> 8) * (1f / (1 << 24));
            return new ZombieVoice(Mathf.Lerp(MinPitch, MaxPitch, fraction), (int)(h & 0xffu));
        }

        public int PickClip(int count, System.Random rng)
        {
            if (count <= 0) return -1;
            var index = Variant % count;
            return count > 1 && rng.NextDouble() < 0.5 ? (index + 1) % count : index;
        }
    }

    // When zombies speak unprompted and how much each sound matters; the reactions are tied to brain transitions
    // by ZombieAudio.
    public static class ZombieVocals
    {
        public const int MaxVoices = 4;

        public static VocalPriority Priority(ZombieVocal vocal) => vocal switch
        {
            ZombieVocal.Moan or ZombieVocal.Breath => VocalPriority.Ambient,
            ZombieVocal.Sniff => VocalPriority.Searching,
            ZombieVocal.Growl => VocalPriority.Hunting,
            ZombieVocal.Windup => VocalPriority.Telegraph,
            _ => VocalPriority.Reaction,
        };

        // Tier plus closeness in [0, 1): a nearer zombie only wins within its tier.
        public static float Score(ZombieVocal vocal, float distance, float maxDistance)
            => (int)Priority(vocal) + 1f - Mathf.Clamp01(distance / Mathf.Max(maxDistance, 1e-3f));

        // First unprompted vocal after waking up, spread so a crowd does not moan in chorus.
        public static float FirstDelay(System.Random rng) => (float)rng.NextDouble() * 12f;

        // Seconds until the next unprompted vocal; infinity in the states that only react.
        public static float NextDelay(ZombieState state, System.Random rng) => state switch
        {
            ZombieState.Idle or ZombieState.Wander => Range(rng, 5f, 14f),
            ZombieState.Investigate or ZombieState.Search => Range(rng, 2.5f, 6f),
            ZombieState.Chase => Range(rng, 1.8f, 4f),
            ZombieState.Feed => Range(rng, 2.5f, 5f),
            _ => float.PositiveInfinity,
        };

        // Idling and wandering, or investigating and searching, share a rhythm: switching between them keeps the
        // next vocal where it was, otherwise a zombie that keeps changing activity would never speak.
        public static bool SameCadence(ZombieState a, ZombieState b) => Cadence(a) == Cadence(b);

        static int Cadence(ZombieState state) => state switch
        {
            ZombieState.Idle or ZombieState.Wander => 0,
            ZombieState.Investigate or ZombieState.Search => 1,
            ZombieState.Chase => 2,
            ZombieState.Feed => 3,
            _ => 4,
        };

        public static bool TryPickAmbient(ZombieState state, System.Random rng, out ZombieVocal vocal)
        {
            var roll = rng.NextDouble();
            switch (state)
            {
                case ZombieState.Idle:
                case ZombieState.Wander:
                    vocal = roll < 0.65 ? ZombieVocal.Moan : ZombieVocal.Breath;
                    return true;
                case ZombieState.Investigate:
                case ZombieState.Search:
                    vocal = roll < 0.6 ? ZombieVocal.Sniff : ZombieVocal.Breath;
                    return true;
                case ZombieState.Chase:
                    vocal = ZombieVocal.Growl;
                    return true;
                case ZombieState.Feed:
                    vocal = roll < 0.6 ? ZombieVocal.Growl : ZombieVocal.Breath;
                    return true;
                default:
                    vocal = default;
                    return false;
            }
        }

        public static StepKind StepKindOf(Surface surface) => surface switch
        {
            Surface.Grass or Surface.Dirt => StepKind.Soft,
            Surface.Gravel => StepKind.Gravel,
            _ => StepKind.Hard,
        };

        // Boards sound hollower, sheet metal brighter.
        public static float StepPitch(Surface surface) => surface switch
        {
            Surface.Wood => 0.85f,
            Surface.Metal => 1.15f,
            _ => 1f,
        };

        static float Range(System.Random rng, float min, float max) => min + (max - min) * (float)rng.NextDouble();
    }
}
