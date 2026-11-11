using System;
using UnityEngine;

namespace DeadCounty.Animation
{
    // Per-body walking asymmetry: the bad leg spends less of the cycle on the ground and barely lifts, the
    // good leg comes down early after it, and the pelvis drops and rolls more over the bad leg.
    [Serializable]
    public struct LimpSettings
    {
        public bool Enabled;
        public bool LeftIsBad;
        [Tooltip("Fraction of a walking cycle the bad leg spends on the ground.")]
        public float BadDuty;
        public float GoodDuty;
        [Tooltip("Phase of the bad leg after the good one; 0.5 is an even gait.")]
        public float PhaseOffset;
        [Tooltip("Swing lift of the bad leg relative to the good one.")]
        public float BadLiftScale;
        public float PelvisRollScale;
        [Tooltip("Extra hip drop (m) while the bad leg carries the body.")]
        public float ExtraDip;

        public static LimpSettings Default => new()
        {
            BadDuty = 0.62f,
            GoodDuty = 0.78f,
            PhaseOffset = 0.56f,
            BadLiftScale = 0.4f,
            PelvisRollScale = 2f,
            ExtraDip = 0.03f,
        };
    }
}
