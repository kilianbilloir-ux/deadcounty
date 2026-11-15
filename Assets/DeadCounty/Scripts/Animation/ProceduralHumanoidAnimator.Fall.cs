using UnityEngine;

namespace DeadCounty.Animation
{
    // Falls: the body topples about its feet and goes slack. A corpse stays down and the animator switches itself
    // off; a felled body claws at the ground until StandUp, then gathers its limbs and straightens back up.
    public partial class ProceduralHumanoidAnimator
    {
        enum FallState { Standing, Falling, Lying, Rising }

        // Seconds to reach the ground, and how far the model rises so that a body lying flat rests on the ground
        // instead of half in it (m).
        const float CollapseTime = 0.55f;
        const float CollapseLift = 0.12f;
        // Getting up, the back bends over at mid-rise (deg) and the limbs are back under the body by this share
        // of the rise.
        const float RiseHunch = 45f;
        const float RiseGather = 0.6f;
        // A felled body is not a corpse: its arms claw at the ground (deg, Hz), eased in after landing (s).
        const float WritheAngle = 14f;
        const float WritheFrequency = 0.9f;
        const float WritheEaseIn = 0.6f;
        // Joint rotations of a slack body, in the order of `joints`.
        static readonly Quaternion[] SlackPose =
        {
            Quaternion.identity, Quaternion.Euler(4f, 0f, 6f), Quaternion.Euler(10f, 25f, 12f),
            Quaternion.Euler(-12f, 0f, -38f), Quaternion.Euler(8f, 0f, 26f), Quaternion.Euler(-28f, 0f, 0f), Quaternion.Euler(-12f, 0f, 0f),
            Quaternion.Euler(-6f, 0f, -7f), Quaternion.Euler(4f, 0f, 5f), Quaternion.Euler(16f, 0f, 0f), Quaternion.Euler(6f, 0f, 0f),
            Quaternion.Euler(20f, 0f, 0f), Quaternion.Euler(20f, 0f, 0f),
        };

        Transform[] joints;
        Quaternion[] fallFrom;
        Quaternion modelUpright;
        Vector3 fallAxis, modelRest;
        // Fall angle of the model (deg, 90 lying flat), the angle and the joints the current fall or rise started
        // from, and the time into it: progress while falling or rising, seconds on the ground while lying.
        float fallAngle, fallAngleFrom, fallTime, riseDuration;
        FallState fall;
        bool dead;

        // A corpse lying on the ground, the fall over.
        public bool Collapsed => dead && fall == FallState.Lying;
        // From the start of a fall until back on its feet.
        public bool Down => fall != FallState.Standing;

        // Death: the body topples toward worldDirection, or stays down if it already is, then the animator
        // switches itself off. Snap stands it back up once the animator is enabled again.
        public void Collapse(Vector3 worldDirection)
        {
            dead = true;
            Topple(worldDirection);
        }

        // Felled: the body topples toward worldDirection and lies there until StandUp. Felled again while getting
        // up, it falls back the way it came.
        public void KnockDown(Vector3 worldDirection) => Topple(worldDirection);

        // Back on its feet in `duration` seconds, from wherever the fall is; a corpse never gets up.
        public void StandUp(float duration)
        {
            if (dead || fall is FallState.Standing or FallState.Rising) return;
            Capture();
            fall = FallState.Rising;
            riseDuration = Mathf.Max(duration, 1e-3f);
        }

        void Topple(Vector3 worldDirection)
        {
            if (fall is FallState.Falling or FallState.Lying) return;
            if (fall == FallState.Standing)
            {
                worldDirection.y = 0f;
                if (worldDirection.sqrMagnitude < 1e-6f) worldDirection = -transform.forward;
                fallAxis = Vector3.Cross(Vector3.up, transform.InverseTransformDirection(worldDirection.normalized));
                modelUpright = model.localRotation;
                fallAngle = 0f;
                joints ??= new[] { hips, spine, neck, leftShoulder, rightShoulder, leftElbow, rightElbow, leftHip, rightHip, leftKnee, rightKnee, leftAnkle, rightAnkle };
                fallFrom ??= new Quaternion[joints.Length];
            }
            Capture();
            fall = FallState.Falling;
        }

        void Capture()
        {
            for (var i = 0; i < joints.Length; i++) fallFrom[i] = joints[i].localRotation;
            fallAngleFrom = fallAngle;
            fallTime = 0f;
        }

        // Snap: a body on the ground is stood back up at once.
        void StandBackUp()
        {
            dead = false;
            if (fall == FallState.Standing) return;
            fall = FallState.Standing;
            fallAngle = 0f;
            model.localPosition = modelRest;
        }

        // True while the body is falling or lying, which replaces the whole pose.
        bool EvaluateFall(float dt)
        {
            if (fall == FallState.Falling)
            {
                fallTime = Mathf.Min(1f, fallTime + dt / CollapseTime);
                // Slow to tip, fast to land.
                fallAngle = Mathf.Lerp(fallAngleFrom, 90f, fallTime * fallTime);
                var slack = Mathf.SmoothStep(0f, 1f, fallTime);
                model.SetLocalPositionAndRotation(modelRest + Vector3.up * (CollapseLift * fallAngle / 90f), Quaternion.AngleAxis(fallAngle, fallAxis) * modelUpright);
                for (var i = 0; i < joints.Length; i++) joints[i].localRotation = Quaternion.Slerp(fallFrom[i], SlackPose[i], slack);
                if (fallTime < 1f) return true;
                fall = FallState.Lying;
                fallTime = 0f;
                if (dead) enabled = false;
                return true;
            }
            if (fall != FallState.Lying) return false;
            if (dead)
            {
                enabled = false;
                return true;
            }
            fallTime += dt;
            var claw = Mathf.Sin(fallTime * 2f * Mathf.PI * WritheFrequency) * WritheAngle * Mathf.Clamp01(fallTime / WritheEaseIn);
            leftShoulder.localRotation = SlackPose[3] * Quaternion.Euler(claw, 0f, 0f);
            rightShoulder.localRotation = SlackPose[4] * Quaternion.Euler(-claw, 0f, 0f);
            neck.localRotation = SlackPose[2] * Quaternion.Euler(0f, 0f, 0.4f * claw);
            return true;
        }

        // Over the standing pose just evaluated: the body swings up about its feet, bent over and pushing off the
        // ground, while the limbs leave the pose they had on the ground.
        void EvaluateRise(float dt)
        {
            fallTime = Mathf.Min(1f, fallTime + dt / riseDuration);
            fallAngle = fallAngleFrom * (1f - Mathf.SmoothStep(0f, 1f, fallTime));
            model.SetLocalPositionAndRotation(model.localPosition + Vector3.up * (CollapseLift * fallAngle / 90f),
                Quaternion.AngleAxis(fallAngle, fallAxis) * model.localRotation);
            var gather = Mathf.SmoothStep(0f, 1f, fallTime / RiseGather);
            for (var i = 0; i < joints.Length; i++) joints[i].localRotation = Quaternion.Slerp(fallFrom[i], joints[i].localRotation, gather);
            spine.localRotation = Quaternion.Euler(RiseHunch * Mathf.Sin(Mathf.PI * fallTime), 0f, 0f) * spine.localRotation;
            if (fallTime >= 1f) fall = FallState.Standing;
        }
    }
}
