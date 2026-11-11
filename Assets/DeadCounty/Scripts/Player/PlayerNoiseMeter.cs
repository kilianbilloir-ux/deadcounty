using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeadCounty.Player
{
    // How far the player's movement carries, as four segments under the stamina bar, after the sound meters of
    // Splinter Cell: Chaos Theory and Breath of the Wild. Walking is the baseline and stays hidden like the other
    // gauges while all is well: the meter shows while crouched, to confirm the player sneaks, and when they are
    // louder than a walk, then fades a moment later. A short peak such as a hard landing is held long enough to read.
    [RequireComponent(typeof(UIDocument))]
    public class PlayerNoiseMeter : MonoBehaviour
    {
        // Lowest radius (m) that lights each segment, for the GDD values: crouched 1, walk 3, run 8, landing 6, sprint 12.
        static readonly float[] SegmentFrom = { 0.01f, 2f, 5f, 10f };
        const int LoudLevel = 3;
        const float PeakHoldTime = 0.5f;
        const float ShowTime = 1.5f;
        const float FadeSpeed = 3f;

        readonly VisualElement[] segments = new VisualElement[SegmentFrom.Length];
        ThirdPersonController controller;
        PlayerHealth health;
        VisualElement row, meter;
        float peakUntil, shownUntil = float.NegativeInfinity, opacity;
        int shownLevel = -1;

        // Lit segments, 0 when silent.
        public int Level { get; private set; }

        void Awake()
        {
            controller = GetComponentInChildren<ThirdPersonController>(true);
            health = GetComponentInChildren<PlayerHealth>(true);
            if (controller == null || health == null)
                throw new InvalidOperationException($"PlayerNoiseMeter on '{name}' needs a ThirdPersonController and a PlayerHealth in its children");
        }

        // Not in Awake: PlayerHudController loads the layout into the document in its own Awake.
        void Start()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            row = root.Q<VisualElement>("noise-row") ?? throw new InvalidOperationException($"PlayerNoiseMeter on '{name}': the HUD has no 'noise-row'");
            meter = root.Q<VisualElement>("noise-meter") ?? throw new InvalidOperationException($"PlayerNoiseMeter on '{name}': the HUD has no 'noise-meter'");
            if (meter.childCount != segments.Length)
                throw new InvalidOperationException($"PlayerNoiseMeter on '{name}': 'noise-meter' needs {segments.Length} segments, not {meter.childCount}");
            for (var i = 0; i < segments.Length; i++) segments[i] = meter[i];
        }

        void Update()
        {
            var now = Time.time;
            var radius = controller.NoiseRadius;
            var level = 0;
            while (level < SegmentFrom.Length && radius >= SegmentFrom[level]) level++;
            if (level >= Level || now >= peakUntil)
            {
                if (level > Level) peakUntil = now + PeakHoldTime;
                Level = level;
            }

            var alive = health.IsAlive;
            if (alive && (controller.IsCrouching || Level >= LoudLevel)) shownUntil = now + ShowTime;
            if (Level != shownLevel)
            {
                shownLevel = Level;
                for (var i = 0; i < segments.Length; i++) segments[i].EnableInClassList("noise-segment--lit", i < Level);
                meter.EnableInClassList("noise-meter--loud", Level == LoudLevel);
                meter.EnableInClassList("noise-meter--very-loud", Level > LoudLevel);
            }
            opacity = Mathf.MoveTowards(opacity, alive && now < shownUntil ? 1f : 0f, FadeSpeed * Time.deltaTime);
            row.style.opacity = opacity;
        }
    }
}
