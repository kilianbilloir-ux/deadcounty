using DeadCounty.Animation;
using DeadCounty.Perception;
using UnityEngine;

namespace DeadCounty.AI
{
    public enum ZombieState { Idle, Wander, Alert, Investigate, Chase, Search, Attack, Feed }

    public enum AttackPhase { None, Windup, Lunge, Recover }

    public enum TransitionReason { SawTarget, HeardNoise, Proximity, AlertTimeout, LostTarget, SearchDone, Arrived, AttackStart, AttackDone, TargetDead, Called, Reset, IdleDone }

    public struct ZombieTransition
    {
        public ZombieState From;
        public ZombieState To;
        public TransitionReason Reason;
        public float Time;
    }

    // Navigation queries the brain needs, kept off the NavMesh so it stays pure. The brain supplies the random
    // numbers so that sampling stays deterministic.
    public interface IZombieWorld
    {
        // A reachable point within radius of center, picked from two uniform numbers in [0, 1).
        bool TryRandomPoint(Vector3 center, float radius, double random01a, double random01b, out Vector3 point);
    }

    // What the agent sensed this frame. Positions are at ground level. Target fields always hold the true values;
    // the brain only uses them while it perceives the target, or briefly after (chase tracking). Visibility
    // values may be cached between time-sliced sensing ticks. Arrival and path failure refer to the destination
    // of the previous commands.
    public struct PerceptionSnapshot
    {
        public float Time;
        public Vector3 Position;
        public Vector3 Forward;
        public bool TargetAlive;
        public bool TargetVisible;
        // Share of the sight rays (head, chest) that reach the target.
        public float VisibleFraction;
        public float TargetDistance;
        public Vector3 TargetPosition;
        public Vector3 TargetVelocity;
        public float TargetSpeed;
        // PerceptionMath.ConeEdgeFactor of the target.
        public float ConeFactor;
        public float SightRange;
        public bool InProximity;
        // True only on the frame a new noise was heard.
        public bool HeardNoise;
        public Vector3 NoisePosition;
        public NoiseKind NoiseKind;
        public bool ArrivedAtDestination;
        public bool PathFailed;
    }

    // The brain's answer for this frame; every field is rewritten on each Tick.
    public struct BrainCommands
    {
        public bool HasDestination;
        public Vector3 Destination;
        public float Speed;
        public bool Stop;
        public Vector3 LookPoint;
        public bool HasLookPoint;
        public AttackPhase AttackPhase;
        // 0 to 1 within the current attack phase.
        public float AttackProgress;
        // During the lunge the agent moves the body along this direction itself.
        public Vector3 LungeDirection;
        // Last lunge frame only: move the body, resolve the strike, then call ZombieBrain.ReportStrike.
        public bool StrikeNow;
        // One frame: growl a ZombieCall noise of ZombieTuning.CallRadius.
        public bool EmitCall;
        public PoseModifiers Pose;
    }
}
