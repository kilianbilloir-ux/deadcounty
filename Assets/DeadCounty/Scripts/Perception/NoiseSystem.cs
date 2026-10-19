using UnityEngine;

namespace DeadCounty.Perception
{
    // Global noise channel: anything that makes a sound emits here and every hearing agent polls the same buffer.
    public static class NoiseSystem
    {
        public static NoiseBuffer Buffer { get; } = new();

        public static int Emit(Vector3 position, float radius, NoiseKind kind, int sourceId) => Buffer.Emit(position, radius, kind, Time.time, sourceId);

        // Domain reload is disabled on entering Play mode, so noises from the previous session must be dropped by hand.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetStatics() => Buffer.Clear();
    }
}
