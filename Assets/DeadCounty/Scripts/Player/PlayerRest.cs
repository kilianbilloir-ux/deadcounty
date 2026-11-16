using System;
using DeadCounty.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeadCounty.Player
{
    // Stamina comes back slowly on the feet (StaminaModel): sitting down is how one gets it back, as in Project
    // Zomboid. Rest sits on the ground where the player stands, a Seat on itself. Moving, Rest again, attacking
    // or being hit gets the player up.
    [RequireComponent(typeof(ThirdPersonController), typeof(PlayerHealth), typeof(PlayerInteractor))]
    public class PlayerRest : MonoBehaviour
    {
        const float MoveToStand = 0.5f;
        // Where the hips are in a body sitting on a seat, above its top, and on the ground (m). On the ground they
        // stay above the ankles: the leg solver has no pose for a hip joint lower than the foot.
        const float HipAboveSeat = 0.12f;
        const float HipOnGround = 0.18f;
        // From the hips to the eyes of a body sitting up (m): where the camera looks.
        const float EyeAboveHips = 0.6f;

        ThirdPersonController controller;
        PlayerHealth health;
        PlayerInteractor interactor;
        PlayerCombat combat;
        InputAction rest, move, attack;
        Seat seat;
        Vector3 standPosition;
        Quaternion standRotation;

        public bool Seated => controller.Seated;
        // On a piece of furniture, as opposed to the ground.
        public bool OnSeat => seat != null;
        // Height of the hips above the feet while seated (m), for the pose.
        public float HipHeight { get; private set; }

        void Awake()
        {
            controller = GetComponent<ThirdPersonController>();
            health = GetComponent<PlayerHealth>();
            interactor = GetComponent<PlayerInteractor>();
            combat = GetComponent<PlayerCombat>();
            var actions = InputSystem.actions;
            rest = actions.FindAction("Player/Rest", true);
            move = actions.FindAction("Player/Move", true);
            attack = actions.FindAction("Player/Attack", true);
        }

        void OnEnable()
        {
            health.Damaged += OnHit;
            health.Died += OnHit;
        }

        void OnDisable()
        {
            health.Damaged -= OnHit;
            health.Died -= OnHit;
            if (Seated) StandUp();
        }

        void OnHit(PlayerHealth _)
        {
            if (Seated) StandUp();
        }

        void Update()
        {
            if (!controller.Controllable) return;
            if (Seated)
            {
                if (rest.WasPressedThisFrame() || attack.WasPressedThisFrame() || move.ReadValue<Vector2>().magnitude > MoveToStand) StandUp();
                return;
            }
            if (rest.WasPressedThisFrame()) SitOnGround();
        }

        // False in the air, in the middle of a search or of a swing.
        public bool SitOnGround()
        {
            if (!CanSit || !controller.IsGrounded) return false;
            HipHeight = HipOnGround;
            controller.Sit(HipHeight + EyeAboveHips);
            return true;
        }

        public bool SitOn(Seat target)
        {
            if (!CanSit) return false;
            standPosition = transform.position;
            standRotation = transform.rotation;
            seat = target;
            HipHeight = seat.Height + HipAboveSeat;
            var (position, rotation) = seat.Place(transform.position);
            seat.Occupied = true;
            controller.Sit(HipHeight + EyeAboveHips, position, rotation);
            return true;
        }

        public void StandUp()
        {
            if (!Seated) return;
            if (seat == null)
            {
                controller.Rise();
                return;
            }
            controller.Rise(standPosition, standRotation);
            seat.Occupied = false;
            seat = null;
        }

        bool CanSit => !Seated && health.IsAlive && !interactor.Busy && (combat == null || !combat.Swing.Busy);
    }
}
