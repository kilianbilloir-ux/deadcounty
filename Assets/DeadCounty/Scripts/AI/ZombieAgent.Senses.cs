using DeadCounty.Core;
using DeadCounty.Perception;
using UnityEngine;

namespace DeadCounty.AI
{
    // Sight and hearing queries, run on the manager's time slice. Sight casts from the eyes to the target's head
    // and chest (glass is see-through); hearing reads the new NoiseSystem events and halves a noise's reach per
    // Wall or Glass piece between it and the ears. Results are cached in the snapshot until the next Sense.
    public partial class ZombieAgent
    {
        const int MaxRayHits = 16;
        const float NoiseMaxAge = 2f;
        // Noise events lie at ground level; their sound travels from about waist height.
        const float NoiseHeight = 1f;
        const float ChestHeightRatio = 0.75f;

        // Shared by every agent: sensing runs one agent at a time on the main thread.
        static readonly RaycastHit[] Hits = new RaycastHit[MaxRayHits];
        static readonly float[] WallDistances = new float[MaxRayHits];
        static readonly NoiseEvent[] Noises = new NoiseEvent[64];

        int lastNoiseId;
        bool noisePending;
        float noiseMargin;
        Vector3 noisePosition;
        NoiseKind noiseKind;

        public int SenseCount { get; private set; }
        public int RaysLastSense { get; private set; }
        // Walls between this zombie and the last noise it heard.
        public int LastNoiseWalls { get; private set; }

        public void Sense(IPerceivable perceived, float now)
        {
            target = perceived;
            SenseCount++;
            RaysLastSense = 0;
            SenseTarget(perceived);
            SenseNoises(now);
        }

        void SenseTarget(IPerceivable t)
        {
            snapshot.TargetVisible = false;
            snapshot.VisibleFraction = 0f;
            snapshot.ConeFactor = 0f;
            snapshot.InProximity = false;
            snapshot.SightRange = PerceptionMath.SightRange(tuning.DayRange, tuning.NightRange, tuning.FlashlightRange, WorldLighting.NightFactor,
                t != null && t.FlashlightOn, t != null && t.IsCrouching, tuning.CrouchRangeFactor);
            if (t == null || !t.Visible || !t.IsAlive) return;

            var position = motor.Position;
            var toTarget = t.Position - position;
            toTarget.y = 0f;
            var distance = toTarget.magnitude;
            if (distance < tuning.ProximityRadius)
                snapshot.InProximity = LineOfSight(position + Vector3.up * (tuning.EyeHeight * ChestHeightRatio), t.ChestPoint);

            // Far zombies only listen; no sight range reaches that far anyway.
            if (Lod == ZombieLod.Far || distance > snapshot.SightRange) return;
            var forward = motor.Heading;
            if (!PerceptionMath.InCone(forward, toTarget, tuning.FieldOfView)) return;
            var eye = position + Vector3.up * tuning.EyeHeight;
            var points = (LineOfSight(eye, t.HeadPoint) ? 1 : 0) + (LineOfSight(eye, t.ChestPoint) ? 1 : 0);
            snapshot.VisibleFraction = points * 0.5f;
            snapshot.TargetVisible = points > 0;
            snapshot.ConeFactor = PerceptionMath.ConeEdgeFactor(forward, toTarget, tuning.FieldOfView);
        }

        bool LineOfSight(Vector3 from, Vector3 to)
        {
            var offset = to - from;
            var length = offset.magnitude;
            if (length < 1e-4f) return true;
            RaysLastSense++;
            var count = Physics.RaycastNonAlloc(from, offset / length, Hits, length, GameLayers.EnvironmentMask, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
                if (!Hits[i].collider.CompareTag(GameLayers.GlassTag)) return false;
            return true;
        }

        void SenseNoises(float now)
        {
            var ear = motor.Position + Vector3.up * tuning.EyeHeight;
            var multiplier = brain.HearingMultiplier;
            int count;
            do
            {
                count = NoiseSystem.Buffer.CopySince(lastNoiseId, Noises);
                for (var i = 0; i < count; i++)
                {
                    ref readonly var noise = ref Noises[i];
                    lastNoiseId = noise.Id;
                    if (noise.SourceId == ownId || now - noise.Time > NoiseMaxAge) continue;
                    Listen(noise, ear, multiplier);
                }
            } while (count == Noises.Length);
        }

        // Keeps the noise heard with the widest margin until the next Think.
        void Listen(in NoiseEvent noise, Vector3 ear, float multiplier)
        {
            var origin = noise.Position + Vector3.up * NoiseHeight;
            var offset = ear - origin;
            var distance = offset.magnitude;
            if (distance > noise.Radius * multiplier) return;
            var walls = distance > 1e-4f ? CountWalls(origin, offset / distance, distance) : 0;
            var margin = PerceptionMath.EffectiveHearingRadius(noise.Radius, multiplier, walls) - distance;
            if (margin < 0f || (noisePending && margin <= noiseMargin)) return;
            noisePending = true;
            noiseMargin = margin;
            noisePosition = noise.Position;
            noiseKind = noise.Kind;
            LastNoiseWalls = walls;
        }

        int CountWalls(Vector3 origin, Vector3 direction, float distance)
        {
            RaysLastSense++;
            var count = Physics.RaycastNonAlloc(origin, direction, Hits, distance, GameLayers.EnvironmentMask, QueryTriggerInteraction.Ignore);
            var walls = 0;
            for (var i = 0; i < count; i++)
            {
                var hitCollider = Hits[i].collider;
                if (hitCollider.CompareTag(GameLayers.WallTag) || hitCollider.CompareTag(GameLayers.GlassTag)) WallDistances[walls++] = Hits[i].distance;
            }
            PerceptionMath.SortAscending(WallDistances, walls);
            return PerceptionMath.CountWalls(WallDistances, walls);
        }

        void ClearPerception()
        {
            snapshot = default;
            noisePending = false;
            lastNoiseId = NoiseSystem.Buffer.LatestId;
            LastNoiseWalls = 0;
        }
    }
}
