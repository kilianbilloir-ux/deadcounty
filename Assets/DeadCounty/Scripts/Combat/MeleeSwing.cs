namespace DeadCounty.Combat
{
    public enum SwingPhase { Ready, Windup, Recovery }

    // Timeline of one swing, kept off the scene so it can be tested: the wind-up, the blow on the tick it ends,
    // then the recovery. Time past the end of a phase carries over into the next.
    public class MeleeSwing
    {
        float elapsed;

        public SwingPhase Phase { get; private set; }
        public WeaponStats Stats { get; private set; }
        public bool Busy => Phase != SwingPhase.Ready;
        // 0 to 1 within the current phase, 0 when ready.
        public float Progress => Phase switch
        {
            SwingPhase.Windup => Stats.Windup > 0f ? elapsed / Stats.Windup : 1f,
            SwingPhase.Recovery => Stats.Recovery > 0f ? elapsed / Stats.Recovery : 1f,
            _ => 0f,
        };

        // False while a swing is under way.
        public bool Start(in WeaponStats stats)
        {
            if (Busy) return false;
            Stats = stats;
            Phase = SwingPhase.Windup;
            elapsed = 0f;
            return true;
        }

        // True on the tick the blow lands.
        public bool Tick(float dt)
        {
            if (!Busy) return false;
            elapsed += dt;
            var struck = false;
            if (Phase == SwingPhase.Windup && elapsed >= Stats.Windup)
            {
                elapsed -= Stats.Windup;
                Phase = SwingPhase.Recovery;
                struck = true;
            }
            if (Phase == SwingPhase.Recovery && elapsed >= Stats.Recovery) Phase = SwingPhase.Ready;
            return struck;
        }

        // A swing interrupted before the blow never lands.
        public void Cancel() => Phase = SwingPhase.Ready;
    }
}
