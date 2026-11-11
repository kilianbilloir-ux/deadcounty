using System;
using System.Collections.Generic;
using DeadCounty.AI;
using DeadCounty.Perception;
using NUnit.Framework;
using UnityEngine;

namespace DeadCounty.Tests
{
    // Deterministic stand-in for NavMesh sampling: a uniform point in the disc of radius x Scale around center.
    sealed class FakeWorld : IZombieWorld
    {
        public float Scale = 1f;
        public bool Fail;
        public Vector3 LastCenter;
        public float LastRadius;

        public bool TryRandomPoint(Vector3 center, float radius, double random01a, double random01b, out Vector3 point)
        {
            LastCenter = center;
            LastRadius = radius;
            var angle = (float)(random01a * 2.0 * Math.PI);
            point = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (radius * Scale * Mathf.Sqrt((float)random01b));
            return !Fail;
        }
    }

    struct LoggedTransition
    {
        public int Frame;
        public ZombieTransition Transition;

        public override string ToString() => $"{Frame}: {Transition.From} -> {Transition.To} ({Transition.Reason})";
    }

    // Minimal zombie agent for brain tests at 60 Hz: builds the snapshot from plain fields, walks the body
    // straight to the commanded destination, performs the lunge and resolves strikes with AttackResolver.
    sealed class BrainSim
    {
        public const float Dt = 1f / 60f;
        const float ArriveRadius = 0.1f;

        public readonly ZombieTuning Tuning = ZombieTuning.Standard;
        public readonly FakeWorld World = new();
        public readonly ZombieBrain Brain;
        public List<LoggedTransition> Log = new();
        public BrainCommands Commands;
        public Vector3 Position;
        public Vector3 Forward = Vector3.forward;
        public bool MoveBody = true;
        public bool TargetAlive = true;
        public bool TargetVisible;
        public bool InProximity;
        public bool MoveTarget;
        public Vector3 TargetPosition = new(0f, 0f, 100f);
        public Vector3 TargetVelocity;
        public float SightRange = 20f;
        // False leaves strikes unreported, which the brain must treat as misses.
        public bool ReportStrikes = true;
        public int Frame;
        public int Strikes;

        bool noisePending;
        Vector3 noisePosition;
        NoiseKind noiseKind;

        public BrainSim(int seed = 7, Vector3 anchor = default)
        {
            Brain = new ZombieBrain(Tuning, seed, anchor);
            Position = anchor;
        }

        public ZombieState State => Brain.State;
        public LoggedTransition Last => Log[Log.Count - 1];

        public void Hear(Vector3 position, NoiseKind kind = NoiseKind.Footstep)
        {
            noisePending = true;
            noisePosition = position;
            noiseKind = kind;
        }

        public void Step()
        {
            if (MoveTarget) TargetPosition += TargetVelocity * Dt;
            var snapshot = new PerceptionSnapshot
            {
                Time = Frame * Dt,
                Position = Position,
                Forward = Forward,
                TargetAlive = TargetAlive,
                TargetVisible = TargetVisible,
                VisibleFraction = 1f,
                TargetDistance = PlanarDistance(Position, TargetPosition),
                TargetPosition = TargetPosition,
                TargetVelocity = TargetVelocity,
                TargetSpeed = TargetVelocity.magnitude,
                ConeFactor = 1f,
                SightRange = SightRange,
                InProximity = InProximity,
                HeardNoise = noisePending,
                NoisePosition = noisePosition,
                NoiseKind = noiseKind,
                ArrivedAtDestination = Commands.HasDestination && PlanarDistance(Position, Commands.Destination) <= ArriveRadius,
            };
            noisePending = false;
            Brain.Tick(Dt, snapshot, World, ref Commands);
            if (Log != null && Brain.TransitionedThisTick) Log.Add(new LoggedTransition { Frame = Frame, Transition = Brain.LastTransition });
            Move();
            if (Commands.StrikeNow)
            {
                Strikes++;
                if (ReportStrikes) Brain.ReportStrike(AttackResolver.Hits(Tuning.Attack, Position, Commands.LungeDirection, TargetPosition));
            }
            Frame++;
        }

        public void Run(float seconds)
        {
            for (var i = Mathf.RoundToInt(seconds / Dt); i > 0; i--) Step();
        }

        // Steps until the brain enters state and returns the frame that entered it.
        public int RunUntil(ZombieState state, float maxSeconds = 60f)
        {
            for (var i = Mathf.RoundToInt(maxSeconds / Dt); i > 0; i--)
            {
                Step();
                if (Brain.TransitionedThisTick && State == state) return Frame - 1;
            }
            Assert.Fail($"No {state} within {maxSeconds} s, still {State}; transitions: {string.Join(", ", Log ?? new List<LoggedTransition>())}");
            return -1;
        }

        // Puts the target at distance in front of the zombie, yawDeg off its forward, moving away at awaySpeed.
        public void PlaceTarget(float distance, float yawDeg = 0f, float awaySpeed = 0f)
        {
            var direction = Quaternion.Euler(0f, yawDeg, 0f) * Forward;
            TargetPosition = Position + direction * distance;
            TargetVelocity = direction * awaySpeed;
        }

        void Move()
        {
            if (!MoveBody) return;
            if (Commands.AttackPhase == AttackPhase.Lunge)
            {
                Position += Commands.LungeDirection * (Commands.Speed * Dt);
                return;
            }
            if (!Commands.HasDestination || Commands.Stop) return;
            var destination = Commands.Destination;
            destination.y = Position.y;
            var offset = destination - Position;
            if (offset.sqrMagnitude > 1e-8f) Forward = offset.normalized;
            Position = Vector3.MoveTowards(Position, destination, Commands.Speed * Dt);
        }

        public static float PlanarDistance(Vector3 a, Vector3 b)
        {
            var offset = a - b;
            offset.y = 0f;
            return offset.magnitude;
        }

        // A zombie that has just started chasing a target felt 1 m behind it, body and target held still.
        public static BrainSim Chasing(int seed = 7)
        {
            var sim = new BrainSim(seed) { MoveBody = false, InProximity = true, TargetPosition = new Vector3(0f, 0f, -1f) };
            sim.RunUntil(ZombieState.Chase);
            sim.InProximity = false;
            return sim;
        }

        public static void AssertNear(Vector3 actual, Vector3 expected, string message = null, float tolerance = 1e-3f)
            => Assert.That(Vector3.Distance(actual, expected), Is.LessThanOrEqualTo(tolerance), $"{message} expected {expected}, got {actual}");
    }
}
