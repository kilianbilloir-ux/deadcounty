using System;
using DeadCounty.World;
using UnityEngine;

namespace DeadCounty.Player
{
    // A bed (GDD 8.4): lying down takes a second of standing still, and only a tired player can fall asleep.
    [RequireComponent(typeof(Collider))]
    public class Bed : Interactable
    {
        const float PromptAboveTop = 0.3f;
        const float LieDownTime = 1f;

        Vector3 promptPoint;

        public override string DisplayName => CanSleep ? "Lit" : "Lit (pas fatigué)";
        public override string Action => CanSleep ? "Dormir" : null;
        public override Vector3 PromptPoint => promptPoint;

        static bool CanSleep
        {
            get
            {
                var player = PerceptionTarget.Player;
                return player != null && player.TryGetComponent<PlayerSleep>(out var sleep) && sleep.CanSleep;
            }
        }

        void Awake()
        {
            var bounds = GetComponent<Collider>().bounds;
            promptPoint = new Vector3(bounds.center.x, bounds.max.y + PromptAboveTop, bounds.center.z);
        }

        public override float UseTime(bool hurried) => CanSleep ? LieDownTime : 0f;

        public override void Interact(Transform user, bool hurried)
        {
            if (!user.TryGetComponent<PlayerSleep>(out var sleep)) throw new InvalidOperationException($"{user.name} cannot sleep: no PlayerSleep");
            sleep.TrySleep();
        }
    }
}
