namespace DeadCounty.Core
{
    // Layer indices and tags shared by the scene builder and gameplay queries.
    public static class GameLayers
    {
        public const int Player = 6;
        public const int Zombie = 7;
        public const string WallTag = "Wall";
        public const string GlassTag = "Glass";
        // Environment colliders (Default layer): they block sight, and the tagged ones count as walls for hearing.
        public const int EnvironmentMask = 1 << 0;
    }
}
