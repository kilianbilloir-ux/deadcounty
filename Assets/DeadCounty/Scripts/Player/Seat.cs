using System;
using DeadCounty.UI;
using DeadCounty.World;
using UnityEngine;

namespace DeadCounty.Player
{
    // A bench, a sofa: somewhere to sit down and get one's breath back (PlayerRest).
    [RequireComponent(typeof(Collider))]
    public class Seat : Interactable
    {
        const float PromptAboveTop = 0.3f;

        [SerializeField] string displayName = "Banc";
        [Tooltip("Top of the seat above the floor (m).")]
        [SerializeField] float height = 0.45f;
        [Tooltip("How far from the middle one can sit, along the local X (m): 0 for a single seat.")]
        [SerializeField] float halfWidth;
        [Tooltip("Local Z of the sitter.")]
        [SerializeField] float depth;
        [Tooltip("Local yaw the sitter faces (deg): 180 for furniture whose front is -Z, 0 for a pew facing +Z.")]
        [SerializeField] float facing = 180f;

        Collider block;
        Vector3 promptPoint;

        public float Height => height;
        // Someone sits here: the block stops standing in the way of the camera behind them, and of nothing else,
        // since the sitter is in the seat.
        public bool Occupied
        {
            get => !block.enabled;
            set => block.enabled = !value;
        }
        public override string DisplayName => Loc.T(displayName);
        public override string Action => Loc.T("S'asseoir");
        public override Vector3 PromptPoint => promptPoint;

        public void Configure(string seatName, float seatHeight, float seatHalfWidth, float seatDepth, float seatFacing = 180f)
        {
            displayName = seatName;
            height = seatHeight;
            halfWidth = seatHalfWidth;
            depth = seatDepth;
            facing = seatFacing;
        }

        void Awake()
        {
            block = GetComponent<Collider>();
            var bounds = block.bounds;
            promptPoint = new Vector3(bounds.center.x, bounds.max.y + PromptAboveTop, bounds.center.z);
        }

        // Where someone coming from `from` sits: the nearest free stretch of the seat, feet on the floor.
        public (Vector3 position, Quaternion rotation) Place(Vector3 from)
        {
            var along = Mathf.Clamp(transform.InverseTransformPoint(from).x, -halfWidth, halfWidth);
            return (transform.TransformPoint(new Vector3(along, 0f, depth)), transform.rotation * Quaternion.Euler(0f, facing, 0f));
        }

        public override void Interact(Transform user, bool hurried)
        {
            if (!user.TryGetComponent<PlayerRest>(out var rest)) throw new InvalidOperationException($"{user.name} cannot sit: no PlayerRest");
            rest.SitOn(this);
        }
    }
}
