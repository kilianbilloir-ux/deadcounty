namespace DeadCounty.AI
{
    // Felled: the zombie lies still, then takes RiseTime to get up and turns on whoever struck it. Blows on the
    // ground start the lie over, so a player who keeps hitting keeps it down.
    public partial class ZombieBrain
    {
        float downDuration;

        // Seconds this fall keeps the zombie lying still, before it starts getting up.
        public float DownDuration => downDuration;
        public bool Rising => State == ZombieState.Down && StateTime >= downDuration;

        void Fall()
        {
            downDuration = tuning.DownTime * (1f + tuning.DownTimeJitter * Signed());
            Transition(ZombieState.Down, TransitionReason.Felled);
        }

        void UpdateDown(in PerceptionSnapshot p)
        {
            if (StateTime < downDuration + tuning.RiseTime - Epsilon) return;
            GetBackAtIt(p);
        }

        // The body neither walks nor turns on the ground; only the blow slides it.
        void WriteDown(ref BrainCommands c)
        {
            c.Stop = true;
            c.HasLookPoint = false;
            c.Pose.ArmsForward = Rising ? StaggerArms : 0f;
            WritePush(ref c);
        }
    }
}
