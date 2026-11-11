using System;
using UnityEngine;

namespace DeadCounty.AI
{
    public readonly struct ZombieTransitionEvent
    {
        public readonly int AgentId;
        public readonly float Time;
        public readonly ZombieState From;
        public readonly ZombieState To;
        public readonly TransitionReason Reason;
        public readonly Vector3 Position;

        public ZombieTransitionEvent(int agentId, in ZombieTransition transition, Vector3 position)
        {
            AgentId = agentId;
            Time = transition.Time;
            From = transition.From;
            To = transition.To;
            Reason = transition.Reason;
            Position = position;
        }
    }

    public readonly struct ZombieStrikeEvent
    {
        public readonly int AgentId;
        public readonly float Time;
        public readonly Vector3 Origin;
        public readonly Vector3 Direction;
        public readonly bool Hit;

        public ZombieStrikeEvent(int agentId, float time, Vector3 origin, Vector3 direction, bool hit)
        {
            AgentId = agentId;
            Time = time;
            Origin = origin;
            Direction = direction;
            Hit = hit;
        }
    }

    // Global bus of zombie events, raised by the agents: the debug tools listen today, the pacing director later.
    public static class AIEvents
    {
        public static event Action<ZombieTransitionEvent> Transition;
        public static event Action<ZombieStrikeEvent> Strike;

        public static void RaiseTransition(int agentId, in ZombieTransition transition, Vector3 position)
            => Transition?.Invoke(new ZombieTransitionEvent(agentId, transition, position));

        public static void RaiseStrike(int agentId, float time, Vector3 origin, Vector3 direction, bool hit)
            => Strike?.Invoke(new ZombieStrikeEvent(agentId, time, origin, direction, hit));

        // Domain reload is disabled on entering Play mode, so the previous session's listeners must be dropped by hand.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetStatics()
        {
            Transition = null;
            Strike = null;
        }
    }
}
