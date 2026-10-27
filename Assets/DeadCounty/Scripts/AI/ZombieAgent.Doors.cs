using DeadCounty.Perception;
using DeadCounty.World;
using UnityEngine;

namespace DeadCounty.AI
{
    // Closed doors are left out of the NavMesh, so paths cross them: the zombie stops in front of a shut door on
    // its way. Hunting, it pushes it until the pushes force it open; calm, it takes the door as the end of its
    // walk and the brain picks something else to do. The bang of a door it forces is not news to the zombie.
    public partial class ZombieAgent
    {
        const float DoorProbe = 0.5f;
        const float MinDoorSteer = 0.01f;
        // A forced door bangs on the frame of the last push.
        const float OwnDoorNoiseWindow = 0.1f;

        int pushedDoorSource;
        float lastDoorPushTime = float.NegativeInfinity;

        // Calm and stopped by a door last frame: reported to the brain as arrived.
        bool calmAtDoor;

        // The shut door this zombie pushed on its last Move, or null.
        public Door PushedDoor { get; private set; }

        void StopAtShutDoor(float dt)
        {
            calmAtDoor = false;
            PushedDoor = null;
            if (!commands.HasDestination || commands.Stop || commands.AttackPhase != AttackPhase.None) return;
            var desired = navAgent.desiredVelocity;
            desired.y = 0f;
            if (desired.sqrMagnitude < MinDoorSteer * MinDoorSteer) return;
            var from = motor.Position;
            var door = Door.FindBlocking(from, from + desired.normalized * DoorProbe);
            if (door == null) return;
            commands.Stop = true;
            commands.HasLookPoint = true;
            commands.LookPoint = door.Doorway.Center + Vector3.up * tuning.EyeHeight;
            if (brain.State is not (ZombieState.Chase or ZombieState.Investigate or ZombieState.Search))
            {
                calmAtDoor = true;
                return;
            }
            commands.Pose.ArmsForward = 1f;
            pushedDoorSource = door.SourceId;
            lastDoorPushTime = Time.time;
            PushedDoor = door;
            door.Push(from, dt);
        }

        bool IsOwnDoorNoise(in NoiseEvent noise) => noise.SourceId == pushedDoorSource && noise.Time - lastDoorPushTime <= OwnDoorNoiseWindow;
    }
}
