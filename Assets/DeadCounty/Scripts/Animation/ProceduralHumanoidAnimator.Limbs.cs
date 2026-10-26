using UnityEngine;

namespace DeadCounty.Animation
{
    // Feet and limbs of ProceduralHumanoidAnimator: the foot cycle that keeps planted feet on the ground, the cap on
    // the hip height, the ground probe under each foot and the two-bone IK of the legs and arms.
    public partial class ProceduralHumanoidAnimator
    {
        struct FootPose
        {
            public float Forward, Lift, Roll, Support;
        }

        // A planted foot moves back exactly as far as the body goes forward, so it keeps its place on the ground
        // when the stride under it is resized by a speed change (lunge, braking). Drift is how far it ends up
        // from the steady-speed cycle; the next swing absorbs it. A swinging foot covers, each frame, the share
        // of its remaining way that the swing curve covers, toward a landing point that follows speed changes
        // at a bounded rate, and lands where the swing brought it.
        struct FootTrack
        {
            public bool Planted, Swinging;
            // ToeOff: how far the cycle still had to go back at the last stance frame.
            // Lateral: sideways foothold in the model frame; stance keeps it world-fixed like Forward.
            public float Forward, Drift, ToeOff, Blend, Landing, Lateral;
        }

        // How fast a swinging foot's landing point follows the stride: normal accelerations stay well below it,
        // the stop at the end of a lunge does not swing the foot back.
        const float LandingShiftSpeed = 3f;


        // Stance: the foot moves from +half to -half with the ground (heel strike, then toe-off), the whole
        // window shifted back by `shift`; it lands on the cycle and then moves back by `advance` per frame, which
        // matches the cycle at a steady speed. The body also turns under a planted foot: the old foothold is
        // rotated by the frame's yaw delta, like the backward slide cancels the translation. Swing: it travels
        // forward again on an arc from where it took off, lifting at least as high as the distance it has to
        // catch up (capped at a walking step), and steps back toward its nominal sideways place. All match the
        // plain cycle exactly going straight.
        FootPose FootCycle(ref FootTrack track, float p, float duty, float half, float shift, float lift, float advance, float dt, float sideX, float yawDelta)
        {
            var heel = gait.HeelStrikeRoll;
            var toe = gait.ToeOffRoll;
            if (p < duty)
            {
                var t = p / duty;
                var roll = t < 0.2f ? Mathf.Lerp(heel, 0f, t / 0.2f) : t > 0.7f ? Mathf.Lerp(0f, toe, (t - 0.7f) / 0.3f) : 0f;
                var cycle = Mathf.Lerp(half, -half, t) - shift;
                // Landing: on the cycle, unless the swing ended away from it, then where the swing ended (the
                // window covers how far a steady swing and the cycle can differ over one frame).
                if (track.Planted)
                {
                    // The root turned under the foot: rotate the old foothold by the frame's yaw expressed in
                    // the banked model frame (a plain yaw would shear it), then slide it back by the travel.
                    // ankleHeight stands in for the true ankle height: exact for yaw, second order via bank.
                    var yaw = Quaternion.Inverse(model.localRotation)
                        * Quaternion.Euler(0f, -yawDelta * Mathf.Rad2Deg, 0f)
                        * model.localRotation;
                    var rotated = yaw * new Vector3(track.Lateral, ankleHeight, track.Forward);
                    track.Lateral = rotated.x;
                    track.Forward = rotated.z - advance;
                }
                else track.Forward = track.Swinging
                    ? Mathf.Clamp(cycle, track.Forward - 2f * advance, track.Forward + advance)
                    : cycle;
                track.Planted = true;
                track.Swinging = false;
                track.Drift = track.Forward - cycle;
                track.ToeOff = cycle + half + shift;
                return new FootPose { Forward = track.Forward, Roll = roll, Support = Mathf.Sin(Mathf.PI * t) };
            }
            var landing = half - shift;
            if (!track.Swinging)
            {
                // Takes off from where the foot stands, after the rest of its stance (at most this frame's step).
                if (track.Planted) track.Forward -= Mathf.Min(track.ToeOff, advance);
                track.Planted = false;
                track.Swinging = true;
                track.Blend = 0f;
                track.Landing = landing;
            }
            track.Landing = Mathf.MoveTowards(track.Landing, landing, LandingShiftSpeed * dt);
            track.Lateral = Mathf.Lerp(track.Lateral, sideX, 1f - Mathf.Exp(-10f * dt));
            var s = (p - duty) / (1f - duty);
            var blend = Mathf.SmoothStep(0f, 1f, s);
            if (blend > track.Blend && track.Blend < 1f)
                track.Forward +=(track.Landing - track.Forward) * (blend - track.Blend) / (1f - track.Blend);
            track.Blend = blend;
            return new FootPose
            {
                Forward = track.Forward,
                Lift = Mathf.Max(lift, Mathf.Min(Mathf.Abs(track.Drift), gait.WalkLift)) * Mathf.Sin(Mathf.PI * s),
                Roll = Mathf.Lerp(toe, heel, s),
            };
        }

        // Highest the hips can be while this foot stays on the ground with a stretched leg (no limit for a
        // foot that is lifted in its swing). `hipForward` is how far the hips are pushed ahead of the feet frame.
        float HipCap(FootPose foot, float groundOffset, float hipForward)
        {
            if (foot.Lift > 0.02f) return float.PositiveInfinity;
            var leg = thighLength + shinLength - 0.01f;
            var reach = foot.Forward - hipForward;
            var heelRaise = foot.Roll > 0f ? footLength * Mathf.Sin(foot.Roll * Mathf.Deg2Rad) : 0f;
            return Mathf.Sqrt(Mathf.Max(0f, leg * leg - reach * reach)) + ankleHeight + heelRaise + foot.Lift + groundOffset - hipJointOffset;
        }

        // Ground height under a foot `forward` ahead of its hip joint, relative to the visible feet level,
        // clamped to one step up or down.
        float GroundOffset(Transform hip, float forward)
        {
            var feetY = model.position.y;
            var foot = hip.position + transform.forward * forward;
            var range = gait.MaxFootOffset;
            var origin = new Vector3(foot.x, feetY + range, foot.z);
            return Physics.Raycast(origin, Vector3.down, out var hit, range * 2f, groundMask, QueryTriggerInteraction.Ignore)
                ? Mathf.Clamp(hit.point.y - feetY, -range, range)
                : 0f;
        }

        // Negative X swings a limb forward; Z spreads the arm away from the torso. `reach` blends toward arms
        // held out at `reachPitch` with nearly straight elbows.
        static void Arm(Transform shoulder, Transform elbow, float swing, float outward, float bend, float reach, float reachPitch, float reachBend)
        {
            shoulder.localRotation = Quaternion.Euler(Mathf.Lerp(swing, reachPitch, reach), 0f, outward);
            elbow.localRotation = Quaternion.Euler(-Mathf.Lerp(bend + Mathf.Max(0f, -swing) * 0.4f, reachBend, reach), 0f, 0f);
        }

        // Two-bone IK placing the ankle in the model frame at (`lateral`, `height` above the ground, `forward`),
        // with the knee pointing forward. The pelvis sway, yaw, roll and lunge shift are cancelled so they never
        // drag a `planted` foot. `roll` pitches the boot (heel strike, toe-off) and lifts the ankle as the foot
        // pivots on its toes; the boot otherwise stays level.
        void Leg(Transform hip, Transform knee, Transform ankle, float lateral, float forward, float height, float roll, bool planted)
        {
            var joint = hips.localPosition + hips.localRotation * hip.localPosition;
            var down = joint.y - ankleHeight - height;
            if (roll > 0f) down -= footLength * Mathf.Sin(roll * Mathf.Deg2Rad);
            var lateralOffset = lateral - joint.x;
            forward -= joint.z;
            // A foot out of reach (hips lagging over a far foot) is lifted straight up rather than pulled toward the
            // hip, so it never slides when planted nor jumps forward when it takes off; past heel strike a planted
            // boot pitches further so the toes stay down while the heel rises.
            var maxReach = thighLength + shinLength - 0.001f;
            var reachableDown = Mathf.Sqrt(Mathf.Max(0f, maxReach * maxReach - forward * forward - lateralOffset * lateralOffset));
            if (down > reachableDown)
            {
                if (planted && roll >= 0f) roll = Mathf.Asin(Mathf.Min(1f, Mathf.Sin(roll * Mathf.Deg2Rad) + (down - reachableDown) / footLength)) * Mathf.Rad2Deg;
                down = reachableDown;
            }
            var tilt = Quaternion.AngleAxis(Mathf.Atan2(lateralOffset, down) * Mathf.Rad2Deg, Vector3.forward);
            var planeDown = Mathf.Sqrt(down * down + lateralOffset * lateralOffset);

            var reach = Mathf.Min(Mathf.Sqrt(forward * forward + planeDown * planeDown), maxReach);
            var hipAngle = Mathf.Atan2(forward, planeDown) * Mathf.Rad2Deg + LawOfCosines(thighLength, reach, shinLength);
            var kneeAngle = 180f - LawOfCosines(thighLength, shinLength, reach);
            var thigh = tilt * Quaternion.Euler(-hipAngle, 0f, 0f);
            hip.localRotation = Quaternion.Inverse(hips.localRotation) * thigh;
            knee.localRotation = Quaternion.Euler(kneeAngle, 0f, 0f);
            ankle.localRotation = Quaternion.Inverse(thigh * knee.localRotation) * Quaternion.Euler(roll, 0f, 0f);
        }

        // Angle in degrees between sides a and b of a triangle, given the side opposite to it.
        static float LawOfCosines(float a, float b, float opposite)
            => Mathf.Acos(Mathf.Clamp((a * a + b * b - opposite * opposite) / (2f * a * b), -1f, 1f)) * Mathf.Rad2Deg;
    }
}
