using UnityEngine;

namespace DeadCounty.Audio
{
    // Soundscape math, no scene state: how much of each bed plays at a given darkness, how a room muffles the
    // outside and how fast the levels follow. EditMode tested.
    public static class AmbienceMix
    {
        // Birds hand over to the insects while NightFactor crosses this range (most of dusk and dawn).
        const float DuskFrom = 0.15f;
        const float DuskTo = 0.85f;
        const float NightWind = 0.6f;
        const float RoomOutdoorGain = 0.3f;
        // Morning chorus: birds sing most from sunrise (DayNightModel dawn, 5-7 h) to mid-morning.
        const float ChorusFrom = 5.5f;
        const float ChorusTo = 9f;
        const float ChorusBoost = 1.5f;

        public const float OpenCutoff = 22000f;
        public const float RoomCutoff = 700f;

        static float Dusk(float night) => Mathf.InverseLerp(DuskFrom, DuskTo, night);

        // Equal power: the two beds are unrelated noises, so their powers add up to a constant.
        public static float DayWeight(float night) => Mathf.Clamp01(Mathf.Cos(Dusk(night) * Mathf.PI / 2f));

        public static float NightWeight(float night) => Mathf.Clamp01(Mathf.Sin(Dusk(night) * Mathf.PI / 2f));

        // The wind calms down after dark.
        public static float WindLevel(float night) => Mathf.Lerp(1f, NightWind, Mathf.Clamp01(night));

        // Songs per second relative to a plain day: none at night, more during the morning chorus.
        public static float BirdRate(float night, float hour)
        {
            var chorus = hour > ChorusFrom && hour < ChorusTo ? Mathf.Sin(Mathf.InverseLerp(ChorusFrom, ChorusTo, hour) * Mathf.PI) : 0f;
            return DayWeight(night) * (1f + ChorusBoost * chorus);
        }

        // indoor: 0 outside, 1 under a roof between walls.
        public static float OutdoorGain(float indoor) => Mathf.Lerp(1f, RoomOutdoorGain, Mathf.Clamp01(indoor));

        // Geometric sweep: each step of indoor muffles by the same musical interval.
        public static float Cutoff(float indoor) => OpenCutoff * Mathf.Pow(RoomCutoff / OpenCutoff, Mathf.Clamp01(indoor));

        // Exponential approach with a time constant, independent of the frame rate.
        public static float Approach(float current, float target, float dt, float seconds) =>
            seconds <= 0f ? target : Mathf.Lerp(current, target, 1f - Mathf.Exp(-dt / seconds));

        // Waiting time of a random event averaging one per mean seconds (Poisson); uniform in [0, 1).
        public static float NextDelay(float mean, float uniform) => -mean * Mathf.Log(1f - Mathf.Clamp(uniform, 0f, 0.999f));
    }
}
