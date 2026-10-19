using System;
using UnityEngine;

namespace DeadCounty.Locomotion
{
    [Serializable]
    public struct LocomotionSettings
    {
        public float Acceleration;
        public float Deceleration;
        public float AirAcceleration;
        [Tooltip("Body turn rate when standing (deg/s); it shrinks linearly down to MovingTurnRate.")]
        public float StandingTurnRate;
        public float MovingTurnRate;
        [Tooltip("Speed (m/s) at which the turn rate reaches MovingTurnRate.")]
        public float TurnRateReferenceSpeed;
        [Tooltip("Heading changes wider than this above PivotMinSpeed brake to a stop before turning.")]
        public float PivotAngle;
        public float PivotMinSpeed;
        public float PivotTurnScale;
        [Tooltip("Fraction of the target speed kept on a 180 degree turn.")]
        public float TightTurnSpeedFactor;

        public static LocomotionSettings Player => new()
        {
            Acceleration = 6f,
            Deceleration = 9f,
            AirAcceleration = 1.5f,
            StandingTurnRate = 600f,
            MovingTurnRate = 200f,
            TurnRateReferenceSpeed = 6f,
            PivotAngle = 120f,
            PivotMinSpeed = 2.45f,
            PivotTurnScale = 0.3f,
            TightTurnSpeedFactor = 0.55f,
        };
    }

    public struct LocomotionState
    {
        // Horizontal unit vector the body faces and moves along on the ground.
        public Vector3 Heading;
        public float Speed;
        public Vector3 PlanarVelocity;
        public bool Pivoting;

        public static LocomotionState Facing(Vector3 forward)
        {
            forward.y = 0f;
            return new LocomotionState { Heading = forward.normalized };
        }
    }

    // Planar movement shared by the player and the zombies: speed builds up and drops with real acceleration,
    // the body faces its velocity and turns at a rate that shrinks with speed, sharp reversals at speed brake
    // before pivoting, and airborne momentum can only be nudged.
    // Callers pass targetSpeed with their own factors applied (slope, landing recovery); with no input they
    // pass wish = state.Heading and targetSpeed = 0.
    public static class PlanarLocomotion
    {
        public static void Step(ref LocomotionState state, in LocomotionSettings s, Vector3 wish, float targetSpeed, bool hasInput, bool grounded, float dt)
        {
            state.Pivoting = false;
            if (grounded)
            {
                if (hasInput)
                {
                    var turn = Vector3.Angle(state.Heading, wish);
                    var turnRate = Mathf.Lerp(s.StandingTurnRate, s.MovingTurnRate, state.Speed / s.TurnRateReferenceSpeed);
                    // A runner cannot reverse on the spot: plant, brake, then turn. Tight turns also cost speed.
                    state.Pivoting = turn > s.PivotAngle && state.Speed > s.PivotMinSpeed;
                    if (state.Pivoting)
                    {
                        targetSpeed = 0f;
                        turnRate *= s.PivotTurnScale;
                    }
                    else
                    {
                        targetSpeed *= Mathf.Lerp(1f, s.TightTurnSpeedFactor, turn / 180f);
                    }
                    state.Heading = Vector3.RotateTowards(state.Heading, wish, turnRate * Mathf.Deg2Rad * dt, 0f);
                }
                state.Speed = Mathf.MoveTowards(state.Speed, targetSpeed, (targetSpeed > state.Speed ? s.Acceleration : s.Deceleration) * dt);
                state.PlanarVelocity = state.Heading * state.Speed;
            }
            else
            {
                state.PlanarVelocity = Vector3.MoveTowards(state.PlanarVelocity, wish * targetSpeed, s.AirAcceleration * dt);
                state.Speed = state.PlanarVelocity.magnitude;
                if (state.Speed > 0.1f) state.Heading = state.PlanarVelocity / state.Speed;
            }
        }
    }
}
