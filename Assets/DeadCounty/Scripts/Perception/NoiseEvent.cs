using UnityEngine;

namespace DeadCounty.Perception
{
    public enum NoiseKind { Footstep, Landing, Panting, ZombieCall, Door, Glass, Impact, Debug }

    // A sound heard within Radius metres of Position. SourceId lets listeners ignore their own noises.
    public readonly struct NoiseEvent
    {
        public readonly int Id;
        public readonly Vector3 Position;
        public readonly float Radius;
        public readonly NoiseKind Kind;
        public readonly float Time;
        public readonly int SourceId;

        public NoiseEvent(int id, Vector3 position, float radius, NoiseKind kind, float time, int sourceId)
        {
            Id = id;
            Position = position;
            Radius = radius;
            Kind = kind;
            Time = time;
            SourceId = sourceId;
        }
    }
}
