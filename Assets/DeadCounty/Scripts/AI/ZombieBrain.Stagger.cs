using UnityEngine;

namespace DeadCounty.AI
{
    // A blow interrupts whatever the zombie was doing: it reels, shoved back, then turns on whoever struck it.
    public partial class ZombieBrain
    {
        // The shove is over well before the stagger: the body slides, then stands reeling.
        const float PushTime = 0.18f;
        const float StaggerArms = 0.3f;

        float staggerDuration;
        float pushSpeed;
        Vector3 pushDirection;

        // Between two Ticks, like Reset. The zombie now knows where the blow came from; `knockback` is the
        // distance it is pushed away from the attacker.
        public void TakeHit(Vector3 position, Vector3 attackerPosition, float stagger, float knockback)
        {
            Know(attackerPosition, Vector3.zero);
            Suspicion = 1f;
            sinceStimulus = 0f;
            staggerDuration = Mathf.Max(stagger, 0f);
            pushDirection = Planar(position - attackerPosition);
            pushSpeed = knockback > 0f ? knockback / PushTime : 0f;
            // A blow breaks a wind-up, and the zombie cannot strike back the instant it recovers.
            attackCooldown = Mathf.Max(attackCooldown, tuning.Attack.Cooldown);
            Transition(ZombieState.Stagger, TransitionReason.Hit);
        }

        void UpdateStagger(in PerceptionSnapshot p)
        {
            if (StateTime < staggerDuration - Epsilon) return;
            if (p.TargetAlive) EnterChase(TransitionReason.Recovered, true);
            else ReturnToAnchor(TransitionReason.TargetDead);
        }

        void WriteStagger(ref BrainCommands c)
        {
            Hold(ref c, AtEyes(LastKnownPosition), StaggerArms);
            // Only the part of the frame inside the shove moves the body, so the distance does not depend on the
            // frame rate.
            var pushed = Mathf.Clamp(PushTime - (StateTime - lastDt), 0f, lastDt);
            if (pushed <= 0f || lastDt <= 0f) return;
            c.PushDirection = pushDirection;
            c.PushSpeed = pushSpeed * pushed / lastDt;
        }
    }
}
