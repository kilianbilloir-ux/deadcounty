using UnityEngine;

namespace DeadCounty.Core
{
    // Where the wind blows and how hard, from the time alone: the heading wanders around its mean and the
    // strength swells and drops over tens of seconds, so calm spells alternate with windy ones.
    public static class WindModel
    {
        // Degrees clockwise from north (+Z), the way the wind blows to.
        public const float MeanHeading = 250f;
        public const float HeadingWander = 30f;
        public const float HeadingPeriod = 83f;
        public const float Calm = 0.35f;
        public const float SwellPeriod = 47f;

        // x, y: unit direction on the XZ plane; z: strength from Calm to 1.
        public static Vector3 Sample(float time)
        {
            const float turn = 2f * Mathf.PI;
            var heading = (MeanHeading + HeadingWander * Mathf.Sin(time * turn / HeadingPeriod)) * Mathf.Deg2Rad;
            // Two periods that never line up, so the swell does not read as a loop.
            var swell = 0.5f + 0.25f * Mathf.Sin(time * turn / SwellPeriod) + 0.25f * Mathf.Sin(time * turn / (SwellPeriod * 0.37f) + 1.3f);
            return new Vector3(Mathf.Sin(heading), Mathf.Cos(heading), Mathf.Lerp(Calm, 1f, swell));
        }
    }
}
