namespace DeadCounty.Core
{
    // When the built night lamps (street, canopy, dock, floodlights) are on: hysteresis around dusk and dawn so
    // they never flicker while NightFactor hovers near the middle. Pure, EditMode tested.
    public static class NightLamps
    {
        public const float OnThreshold = 0.55f;
        public const float OffThreshold = 0.45f;

        public static bool ShouldBeOn(bool currentlyOn, float nightFactor) =>
            currentlyOn ? nightFactor > OffThreshold : nightFactor >= OnThreshold;
    }
}
