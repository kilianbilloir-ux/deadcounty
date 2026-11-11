using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace DeadCounty.Player
{
    // Adapts the ThirdPersonFollow rig to the surroundings so the camera stays readable while searching
    // buildings, without authored trigger volumes:
    //  - scroll zoom, capped closer when the space is confined (ceiling overhead + walls on most sides);
    //  - wall anticipation: sphere casts behind the camera (and 25 degrees to each side) ease it in before
    //    Cinemachine's collision would snap it, then it eases back out slowly;
    //  - shoulder lean-in when a wall is close on the shoulder side, auto swap to the free side, V to swap;
    //  - camera arm kept under low ceilings, wider FOV when confined, player hidden when the camera is inside it.
    // Cinemachine's own obstacle avoidance stays on as the last-resort safety.
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(CinemachineCamera), typeof(CinemachineThirdPersonFollow))]
    public class PlayerCameraDirector : MonoBehaviour
    {
        static readonly float[] BackProbeAngles = { 0f, -25f, 25f };

        [Header("Zoom")]
        [SerializeField] float minDistance = 1.2f;
        [SerializeField] float maxDistance = 6f;
        [SerializeField] float startDistance = 3.2f;
        [SerializeField] float zoomStep = 0.5f;

        [Header("Open / confined framing")]
        [SerializeField] float confinedMaxDistance = 2.2f;
        [SerializeField] float openShoulder = 0.55f;
        [SerializeField] float confinedShoulder = 0.4f;
        [SerializeField] float openArm = 0.2f;
        [SerializeField] float confinedArm = 0.05f;
        [SerializeField] float openFov = 55f;
        [SerializeField] float confinedFov = 62f;
        [SerializeField] float confinementBlendTime = 0.35f;

        [Header("Probes")]
        [SerializeField] LayerMask obstacles = 1;
        [SerializeField] float ceilingProbe = 4f;
        [SerializeField] float roomProbe = 8f;
        [SerializeField, Range(4, 16)] int roomRays = 8;
        [SerializeField, Range(0f, 1f)] float enclosedRatio = 0.6f;
        [SerializeField] float wallMargin = 0.2f;
        [SerializeField] float ceilingMargin = 0.35f;
        [SerializeField] float pullInTime = 0.08f;
        [SerializeField] float pushOutTime = 0.6f;

        [Header("Shoulder")]
        [SerializeField] bool autoSwapShoulder = true;
        [SerializeField] float sideClearance = 0.7f;
        [SerializeField] float swapDelay = 0.4f;
        [SerializeField] float manualSwapHold = 2f;

        [Header("Player")]
        [SerializeField] float hidePlayerBelow = 0.6f;

        CinemachineCamera vcam;
        CinemachineThirdPersonFollow follow;
        Transform pivot;
        Renderer[] playerRenderers;
        InputAction zoom, switchShoulder;
        float userDistance, distance, distanceVelocity, confinement, confinementVelocity;
        float side = 1f, sideTarget = 1f, sideVelocity, shoulder, shoulderVelocity, arm, armVelocity;
        float swapTimer, manualHoldTimer;
        bool playerHidden;

        // 0 = open air, 1 = tight interior. Smoothed, so doorways do not make the camera flicker.
        public float Confinement => confinement;

        void Awake()
        {
            vcam = GetComponent<CinemachineCamera>();
            follow = GetComponent<CinemachineThirdPersonFollow>();
            pivot = vcam.Target.TrackingTarget;
            playerRenderers = pivot.GetComponentInParent<ThirdPersonController>().GetComponentsInChildren<Renderer>();
            var actions = InputSystem.actions;
            zoom = actions.FindAction("Player/Zoom", true);
            switchShoulder = actions.FindAction("Player/SwitchShoulder", true);
            userDistance = distance = startDistance;
            shoulder = openShoulder;
            arm = openArm;
        }

        void Update()
        {
            var origin = pivot.position;
            var flatRight = Quaternion.Euler(0f, pivot.eulerAngles.y, 0f) * Vector3.right;

            userDistance = Mathf.Clamp(userDistance - zoom.ReadValue<float>() * zoomStep, minDistance, maxDistance);

            var hasCeiling = Physics.Raycast(origin, Vector3.up, out var ceiling, ceilingProbe, obstacles, QueryTriggerInteraction.Ignore);
            var confinedTarget = hasCeiling && EnclosedFraction(origin) >= enclosedRatio ? 1f : 0f;
            confinement = Mathf.SmoothDamp(confinement, confinedTarget, ref confinementVelocity, confinementBlendTime);

            UpdateShoulder(origin, flatRight);
            UpdateArm(hasCeiling ? ceiling.distance : float.PositiveInfinity);
            UpdateDistance(origin, flatRight);
            vcam.Lens.FieldOfView = Mathf.Lerp(openFov, confinedFov, confinement);
            UpdatePlayerVisibility(origin);
        }

        float EnclosedFraction(Vector3 origin)
        {
            var hits = 0;
            for (var i = 0; i < roomRays; i++)
            {
                var dir = Quaternion.Euler(0f, i * 360f / roomRays, 0f) * Vector3.forward;
                if (Physics.Raycast(origin, dir, roomProbe, obstacles, QueryTriggerInteraction.Ignore)) hits++;
            }
            return (float)hits / roomRays;
        }

        void UpdateShoulder(Vector3 origin, Vector3 flatRight)
        {
            var freeRight = FreeDistance(origin, flatRight, 2f);
            var freeLeft = FreeDistance(origin, -flatRight, 2f);

            manualHoldTimer -= Time.deltaTime;
            if (switchShoulder.WasPressedThisFrame())
            {
                sideTarget = 1f - sideTarget;
                manualHoldTimer = manualSwapHold;
                swapTimer = 0f;
            }

            // Swap only when the shoulder side is blocked and the other side is clearly free, after a short delay.
            var onRight = sideTarget > 0.5f;
            var blocked = (onRight ? freeRight : freeLeft) < sideClearance;
            var otherFree = (onRight ? freeLeft : freeRight) > sideClearance * 2f;
            swapTimer = autoSwapShoulder && manualHoldTimer <= 0f && blocked && otherFree ? swapTimer + Time.deltaTime : 0f;
            if (swapTimer >= swapDelay)
            {
                sideTarget = 1f - sideTarget;
                swapTimer = 0f;
            }

            side = Mathf.SmoothDamp(side, sideTarget, ref sideVelocity, 0.25f);
            follow.CameraSide = side;

            // Lean the shoulder toward the center when a wall is close on the current side.
            var baseShoulder = Mathf.Lerp(openShoulder, confinedShoulder, confinement);
            var sideFree = Mathf.Lerp(freeLeft, freeRight, side) - follow.AvoidObstacles.CameraRadius - wallMargin;
            shoulder = Mathf.SmoothDamp(shoulder, Mathf.Clamp(sideFree, 0.1f, baseShoulder), ref shoulderVelocity, 0.15f);
            var offset = follow.ShoulderOffset;
            offset.x = shoulder;
            follow.ShoulderOffset = offset;
        }

        void UpdateArm(float ceilingDistance)
        {
            var target = Mathf.Lerp(openArm, confinedArm, confinement);
            target = Mathf.Max(-0.3f, Mathf.Min(target, ceilingDistance - ceilingMargin));
            arm = Mathf.SmoothDamp(arm, target, ref armVelocity, 0.15f);
            follow.VerticalArmLength = arm;
        }

        void UpdateDistance(Vector3 origin, Vector3 flatRight)
        {
            var wanted = Mathf.Min(userDistance, Mathf.Lerp(maxDistance, confinedMaxDistance, confinement));

            var hand = origin + flatRight * Mathf.Lerp(-shoulder, shoulder, side) + Vector3.up * arm;
            var back = -pivot.forward;
            var radius = follow.AvoidObstacles.CameraRadius;
            foreach (var angle in BackProbeAngles)
            {
                var dir = Quaternion.AngleAxis(angle, Vector3.up) * back;
                if (Physics.SphereCast(hand, radius, dir, out var hit, wanted, obstacles, QueryTriggerInteraction.Ignore))
                    wanted = Mathf.Min(wanted, hit.distance - wallMargin);
            }
            wanted = Mathf.Max(0.3f, wanted);

            // Pull in fast to stay ahead of walls, push out slowly so the camera does not pump.
            distance = Mathf.SmoothDamp(distance, wanted, ref distanceVelocity, wanted < distance ? pullInTime : pushOutTime);
            follow.CameraDistance = distance;
        }

        void UpdatePlayerVisibility(Vector3 origin)
        {
            var hide = Vector3.Distance(vcam.State.GetFinalPosition(), origin) < hidePlayerBelow;
            if (hide == playerHidden) return;
            playerHidden = hide;
            foreach (var r in playerRenderers)
                r.shadowCastingMode = hide ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
        }

        float FreeDistance(Vector3 origin, Vector3 dir, float max)
            => Physics.Raycast(origin, dir, out var hit, max, obstacles, QueryTriggerInteraction.Ignore) ? hit.distance : max;
    }
}
