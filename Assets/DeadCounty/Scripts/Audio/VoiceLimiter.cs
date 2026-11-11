using System;

namespace DeadCounty.Audio
{
    // Caps how many owners speak at once. A request takes a free slot, otherwise it evicts the lowest-scored voice
    // when it scores higher. An owner holds one slot at most and replaces its own voice only with one scoring at
    // least as high. A slot frees itself at its end time, on the caller's clock.
    public sealed class VoiceLimiter<TOwner> where TOwner : class
    {
        readonly TOwner[] owners;
        readonly float[] scores;
        readonly float[] ends;

        public VoiceLimiter(int capacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "A limiter needs at least one voice");
            owners = new TOwner[capacity];
            scores = new float[capacity];
            ends = new float[capacity];
        }

        public int Capacity => owners.Length;

        // True when owner may speak until end; evicted is then the owner whose voice must stop, or null.
        public bool TryAcquire(TOwner owner, float score, float now, float end, out TOwner evicted)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            evicted = null;
            Expire(now);
            var slot = IndexOf(owner);
            if (slot >= 0)
            {
                if (score < scores[slot]) return false;
            }
            else
            {
                slot = IndexOf(null);
                if (slot < 0)
                {
                    slot = Weakest();
                    if (score <= scores[slot]) return false;
                    evicted = owners[slot];
                }
            }
            owners[slot] = owner;
            scores[slot] = score;
            ends[slot] = end;
            return true;
        }

        public void Release(TOwner owner)
        {
            if (owner == null) return;
            var slot = IndexOf(owner);
            if (slot >= 0) owners[slot] = null;
        }

        public bool IsSpeaking(TOwner owner, float now)
        {
            if (owner == null) return false;
            var slot = IndexOf(owner);
            return slot >= 0 && ends[slot] > now;
        }

        public int ActiveCount(float now)
        {
            var count = 0;
            for (var i = 0; i < owners.Length; i++)
                if (owners[i] != null && ends[i] > now) count++;
            return count;
        }

        public void Clear() => Array.Clear(owners, 0, owners.Length);

        void Expire(float now)
        {
            for (var i = 0; i < owners.Length; i++)
                if (ends[i] <= now) owners[i] = null;
        }

        // Reference comparison on purpose: a destroyed Unity owner still matches its own slot.
        int IndexOf(TOwner owner)
        {
            for (var i = 0; i < owners.Length; i++)
                if (ReferenceEquals(owners[i], owner)) return i;
            return -1;
        }

        int Weakest()
        {
            var weakest = 0;
            for (var i = 1; i < owners.Length; i++)
                if (scores[i] < scores[weakest]) weakest = i;
            return weakest;
        }
    }
}
