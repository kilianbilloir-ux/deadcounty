using UnityEngine;

namespace DeadCounty.Perception
{
    // Fixed-size ring of recent noises. Ids start at 1 and only increase; listeners remember the last id they
    // processed and poll CopySince. Events older than the capacity are overwritten and silently missed.
    public class NoiseBuffer
    {
        readonly NoiseEvent[] events;
        // Ids keep increasing across Clear so a listener holding a last seen id never skips new events.
        int firstValidId = 1;

        public NoiseBuffer(int capacity = 64) => events = new NoiseEvent[capacity];

        public int LatestId { get; private set; }

        public int Emit(Vector3 position, float radius, NoiseKind kind, float time, int sourceId)
        {
            var id = ++LatestId;
            events[id % events.Length] = new NoiseEvent(id, position, radius, kind, time, sourceId);
            return id;
        }

        // Oldest first, so a caller whose destination fills up can resume from the last copied Id.
        public int CopySince(int lastId, NoiseEvent[] destination)
        {
            var first = Mathf.Max(lastId + 1, LatestId - events.Length + 1, firstValidId);
            var count = Mathf.Clamp(LatestId - first + 1, 0, destination.Length);
            for (var i = 0; i < count; i++) destination[i] = events[(first + i) % events.Length];
            return count;
        }

        public void Clear() => firstValidId = LatestId + 1;
    }
}
