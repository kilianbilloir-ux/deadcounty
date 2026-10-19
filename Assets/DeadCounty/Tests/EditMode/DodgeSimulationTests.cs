using DeadCounty.AI;
using DeadCounty.Locomotion;
using NUnit.Framework;
using UnityEngine;

namespace DeadCounty.Tests
{
    // Design check of the attack timings against the player's real locomotion: a 1.2 m/s zombie winds up for
    // 1 s while still walking and tracking, then lunges along a locked direction. Walking away gets the player
    // caught, running away or sideways dodges, and a player already walking away is never caught at all.
    // Everything happens on the ground plane, zombie starting at the origin facing the player.
    public class DodgeSimulationTests
    {
        const float Dt = 1f / 60f;
        const float ZombieSpeed = 1.2f;
        const float ReactionTime = 0.25f;
        const float WalkSpeed = 1.5f;
        const float RunSpeed = 3.5f;
        const float MaxTime = 20f;

        static readonly AttackSettings Attack = AttackSettings.Default;

        enum Phase { Chase, Windup, Lunge }

        struct Outcome
        {
            public bool Struck;
            public bool Hit;
            public float StrikeTime;
            public float Distance;
            public float Lateral;
            public float MinDistance;
            public float FinalDistance;

            public override string ToString() => Struck
                ? $"{(Hit ? "hit" : "miss")} at {StrikeTime:0.000} s, distance {Distance:0.000} m, lateral {Lateral:0.000} m"
                : $"no attack in {MaxTime} s, min distance {MinDistance:0.000} m, final {FinalDistance:0.000} m";
        }

        // The player stands still until ReactionTime after the attack starts, then heads along fleeDirection;
        // with fleeingFromStart it holds that input from the first frame.
        static Outcome Simulate(Vector3 playerPosition, LocomotionState player, Vector3 fleeDirection, float fleeSpeed, bool fleeingFromStart)
        {
            var locomotion = LocomotionSettings.Player;
            var windupFrames = Mathf.RoundToInt(Attack.Windup / Dt);
            var lungeFrames = Mathf.RoundToInt(Attack.LungeDuration / Dt);
            var lungeSpeed = Attack.LungeDistance / Attack.LungeDuration;
            var zombie = Vector3.zero;
            var facing = playerPosition.normalized;
            var strikeDirection = Vector3.zero;
            var phase = Phase.Chase;
            var phaseFrames = 0;
            var attackFrame = -1;
            var outcome = new Outcome { MinDistance = float.MaxValue };

            for (var frame = 0; frame * Dt < MaxTime; frame++)
            {
                var fleeing = fleeingFromStart || (attackFrame >= 0 && (frame - attackFrame) * Dt >= ReactionTime - 1e-4f);
                if (fleeing) PlanarLocomotion.Step(ref player, locomotion, fleeDirection, fleeSpeed, true, true, Dt);
                else PlanarLocomotion.Step(ref player, locomotion, player.Heading, 0f, false, true, Dt);
                playerPosition += player.PlanarVelocity * Dt;

                var toPlayer = playerPosition - zombie;
                outcome.MinDistance = Mathf.Min(outcome.MinDistance, toPlayer.magnitude);
                outcome.FinalDistance = toPlayer.magnitude;

                if (phase == Phase.Chase)
                {
                    var awaySpeed = Vector3.Dot(player.PlanarVelocity, toPlayer.normalized);
                    if (toPlayer.magnitude <= AttackResolver.TriggerDistance(Attack, awaySpeed) && Vector3.Angle(facing, toPlayer) <= Attack.TriggerHalfAngle)
                    {
                        phase = Phase.Windup;
                        attackFrame = frame;
                    }
                    else
                    {
                        facing = toPlayer.normalized;
                        zombie += facing * StepKeepingContact(zombie, facing, ZombieSpeed * Dt, playerPosition);
                    }
                }

                if (phase == Phase.Windup)
                {
                    facing = Vector3.RotateTowards(facing, toPlayer, Attack.WindupTurnRate * Mathf.Deg2Rad * Dt, 0f);
                    zombie += facing * StepKeepingContact(zombie, facing, ZombieSpeed * Dt, playerPosition);
                    if (++phaseFrames >= windupFrames)
                    {
                        phase = Phase.Lunge;
                        phaseFrames = 0;
                        strikeDirection = facing;
                    }
                }
                else if (phase == Phase.Lunge)
                {
                    zombie += strikeDirection * StepKeepingContact(zombie, strikeDirection, lungeSpeed * Dt, playerPosition);
                    if (++phaseFrames >= lungeFrames)
                    {
                        var offset = playerPosition - zombie;
                        outcome.Struck = true;
                        outcome.Hit = AttackResolver.Hits(Attack, zombie, strikeDirection, playerPosition);
                        outcome.StrikeTime = (frame - attackFrame + 1) * Dt;
                        outcome.Distance = offset.magnitude;
                        outcome.Lateral = (offset - strikeDirection * Vector3.Dot(offset, strikeDirection)).magnitude;
                        break;
                    }
                }
            }
            TestContext.WriteLine(outcome.ToString());
            return outcome;
        }

        // Longest step along direction that keeps the zombie at least ContactDistance from the player.
        static float StepKeepingContact(Vector3 from, Vector3 direction, float step, Vector3 target)
        {
            var minSqr = Attack.ContactDistance * Attack.ContactDistance;
            var offset = from - target;
            if ((offset + direction * step).sqrMagnitude >= minSqr) return step;
            var c = offset.sqrMagnitude - minSqr;
            if (c <= 0f) return 0f;
            var b = Vector3.Dot(offset, direction);
            return Mathf.Clamp(-b - Mathf.Sqrt(Mathf.Max(b * b - c, 0f)), 0f, step);
        }

        static LocomotionState Moving(Vector3 heading, float speed) => new() { Heading = heading, Speed = speed, PlanarVelocity = heading * speed };

        [Test]
        public void TurningAwayAndWalkingGetsHit()
        {
            var outcome = Simulate(new Vector3(0f, 0f, 1.6f), LocomotionState.Facing(Vector3.back), Vector3.forward, WalkSpeed, false);
            Assert.That(outcome.Struck && outcome.Hit, outcome.ToString());
        }

        [Test]
        public void TurningAwayAndRunningDodges()
        {
            var outcome = Simulate(new Vector3(0f, 0f, 1.6f), LocomotionState.Facing(Vector3.back), Vector3.forward, RunSpeed, false);
            Assert.That(outcome.Struck && !outcome.Hit, outcome.ToString());
        }

        [Test]
        public void WalkingAwayIsNeverCaught()
        {
            Assert.That(AttackResolver.TriggerDistance(Attack, WalkSpeed), Is.EqualTo(1.15f).Within(1e-5f));
            var outcome = Simulate(new Vector3(0f, 0f, 1.6f), Moving(Vector3.forward, WalkSpeed), Vector3.forward, WalkSpeed, true);
            Assert.That(outcome.Struck, Is.False, outcome.ToString());
            Assert.That(outcome.MinDistance, Is.GreaterThanOrEqualTo(1.6f), outcome.ToString());
            Assert.That(outcome.FinalDistance, Is.GreaterThan(1.6f + (WalkSpeed - ZombieSpeed) * MaxTime * 0.9f), outcome.ToString());
        }

        [Test]
        public void RunningSidewaysDodges()
        {
            var outcome = Simulate(new Vector3(0f, 0f, 1.2f), LocomotionState.Facing(Vector3.back), Vector3.right, RunSpeed, false);
            Assert.That(outcome.Struck && !outcome.Hit, outcome.ToString());
        }
    }
}
