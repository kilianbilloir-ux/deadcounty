using System;
using UnityEngine;

namespace DeadCounty.AI
{
    // A felled zombie lies along its own axis, on its back or face down, so that its collider can lie with it:
    // low enough to step over, still there for the blows that finish it.
    public partial class ZombieAgent
    {
        const float LyingRadius = 0.25f;
        const float LyingLength = 1.7f;
        // From the feet to the torso of a body on the ground (m).
        const float LyingTorso = 0.85f;

        (Vector3 Center, float Height, float Radius, int Direction) standingShape;
        Vector3 fallDirection;
        bool down, rising;

        // From the fall until back on its feet.
        public bool IsDown => down;
        // Where blows aim: the torso, on the ground or standing.
        public Vector3 BodyCenter => down ? motor.Position + fallDirection * LyingTorso : motor.Position;
        // A fall starts: felled, or felled again while getting up.
        public event Action Felled;
        // The body starts to push itself up off the ground.
        public event Action GettingUp;

        // After the brain went down. A blow on a body already lying changes nothing here.
        void Fall()
        {
            if (down && !rising) return;
            if (!down)
            {
                // On its back when struck from the front, face down from behind.
                var heading = motor.Heading;
                fallDirection = Vector3.Dot(AwayFromLastHit(), heading) >= 0f ? heading : -heading;
                motor.Halt();
                Lie(true);
            }
            down = true;
            rising = false;
            if (animator != null) animator.KnockDown(fallDirection);
            Felled?.Invoke();
        }

        // After the brain's Tick.
        void FollowFall()
        {
            if (!down) return;
            if (brain.State != ZombieState.Down)
            {
                GetUp();
                return;
            }
            if (rising || !brain.Rising) return;
            rising = true;
            // In step with the brain, which started the rise up to a frame ago.
            if (animator != null) animator.StandUp(tuning.RiseTime - (brain.StateTime - brain.DownDuration));
            GettingUp?.Invoke();
        }

        void GetUp()
        {
            down = rising = false;
            Lie(false);
        }

        void Lie(bool lying)
        {
            if (body is not CapsuleCollider capsule) return;
            if (!lying)
            {
                (capsule.center, capsule.height, capsule.radius, capsule.direction) = standingShape;
                return;
            }
            standingShape = (capsule.center, capsule.height, capsule.radius, capsule.direction);
            var along = Mathf.Sign(transform.InverseTransformDirection(fallDirection).z);
            capsule.direction = 2;
            capsule.height = LyingLength;
            capsule.radius = LyingRadius;
            capsule.center = new Vector3(0f, LyingRadius, along * 0.5f * LyingLength);
        }
    }
}
