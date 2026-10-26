using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DeadCounty.AI;
using DeadCounty.Animation;
using DeadCounty.Core;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace DeadCounty.Tests
{
    // A 40 x 40 m NavMesh floor with a ZombieManager perceiving a StubTarget, built at runtime for one test.
    // Game time advances a fixed 1/60 s per frame, times timeScale, so results do not depend on the machine.
    sealed class ZombieTestWorld
    {
        public const float FrameTime = 1f / 60f;

        readonly List<Object> owned = new();
        readonly float savedTimeScale;
        readonly float savedCaptureDeltaTime;
        readonly float savedNightFactor;

        public readonly StubTarget Target = new();
        public readonly List<ZombieTransitionEvent> Transitions = new();
        public readonly List<ZombieStrikeEvent> Strikes = new();
        public readonly ZombieProfile Profile;
        public readonly ZombieManager Manager;

        // groundScale: Unity plane scale, 10 m per unit (the default floor is 40 x 40 m).
        public ZombieTestWorld(float timeScale, float groundScale = 4f)
        {
            savedTimeScale = Time.timeScale;
            savedCaptureDeltaTime = Time.captureDeltaTime;
            savedNightFactor = WorldLighting.NightFactor;
            Time.captureDeltaTime = FrameTime;
            Time.timeScale = timeScale;
            WorldLighting.NightFactor = 0f;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "TestGround";
            ground.transform.localScale = new Vector3(groundScale, 1f, groundScale);
            owned.Add(ground);
            Physics.SyncTransforms();
            var surface = ground.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();

            Profile = ScriptableObject.CreateInstance<ZombieProfile>();
            // Test zombies stand still until something happens, so a wander walk never turns them away.
            Profile.Tuning.IdleChance = 1f;
            // Readable idle spine (no hunch) for the death-snap test; no PlayMode test asserts hunch.
            Profile.Tuning.HunchMin = 0f;
            Profile.Tuning.HunchMax = 0f;
            owned.Add(Profile);

            var managerObject = new GameObject("TestZombieManager");
            owned.Add(managerObject);
            Manager = managerObject.AddComponent<ZombieManager>();
            Manager.TargetOverride = Target;

            AIEvents.Transition += Transitions.Add;
            AIEvents.Strike += Strikes.Add;
        }

        // Built inactive so Configure runs before Awake, like a prefab body would be set up in the editor.
        // deterministic: no speed jitter (and no other per-seed variation), like the scene's test zombies.
        public ZombieAgent SpawnZombie(Vector3 position, float yaw = 0f, int seed = 1, bool withAnimator = false, bool deterministic = true)
        {
            var body = new GameObject($"TestZombie_{seed}");
            body.SetActive(false);
            owned.Add(body);
            body.layer = GameLayers.Zombie;
            body.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var capsule = body.AddComponent<CapsuleCollider>();
            capsule.radius = 0.3f;
            capsule.height = 1.8f;
            capsule.center = new Vector3(0f, 0.9f, 0f);
            body.AddComponent<Rigidbody>().isKinematic = true;
            var agent = body.AddComponent<ZombieAgent>();
            agent.Configure(Profile, seed, deterministic);
            if (withAnimator) AttachAnimator(body, agent);
            body.SetActive(true);
            return agent;
        }

        // A real animator on a test body: joint layout mirrors the EditMode rig, linked by reflection since
        // the fields are serialized privates.
        void AttachAnimator(GameObject body, ZombieAgent agent)
        {
            var model = Joint(body.transform, "Model", Vector3.zero);
            var hips = Joint(model, "Hips", new Vector3(0f, 1f, 0f));
            var spine = Joint(hips, "Spine", new Vector3(0f, 0.06f, 0f));
            var neck = Joint(spine, "Neck", new Vector3(0f, 0.42f, 0f));
            var leftShoulder = Joint(spine, "ShoulderL", new Vector3(-0.24f, 0.36f, 0f));
            var rightShoulder = Joint(spine, "ShoulderR", new Vector3(0.24f, 0.36f, 0f));
            var leftElbow = Joint(leftShoulder, "ElbowL", new Vector3(0f, -0.28f, 0f));
            var rightElbow = Joint(rightShoulder, "ElbowR", new Vector3(0f, -0.28f, 0f));
            var leftHip = Joint(hips, "HipL", new Vector3(-0.095f, -0.05f, 0f));
            var rightHip = Joint(hips, "HipR", new Vector3(0.095f, -0.05f, 0f));
            var leftKnee = Joint(leftHip, "KneeL", new Vector3(0f, -0.44f, 0f));
            var rightKnee = Joint(rightHip, "KneeR", new Vector3(0f, -0.44f, 0f));
            var leftAnkle = Joint(leftKnee, "AnkleL", new Vector3(0f, -0.43f, 0f));
            var rightAnkle = Joint(rightKnee, "AnkleR", new Vector3(0f, -0.43f, 0f));
            var animator = body.AddComponent<ProceduralHumanoidAnimator>();
            var gait = ScriptableObject.CreateInstance<GaitProfile>();
            owned.Add(gait);
            Bind(animator, "controller", agent);
            Bind(animator, "gait", gait);
            Bind(animator, "hips", hips);
            Bind(animator, "spine", spine);
            Bind(animator, "neck", neck);
            Bind(animator, "leftShoulder", leftShoulder);
            Bind(animator, "rightShoulder", rightShoulder);
            Bind(animator, "leftElbow", leftElbow);
            Bind(animator, "rightElbow", rightElbow);
            Bind(animator, "leftHip", leftHip);
            Bind(animator, "rightHip", rightHip);
            Bind(animator, "leftKnee", leftKnee);
            Bind(animator, "rightKnee", rightKnee);
            Bind(animator, "leftAnkle", leftAnkle);
            Bind(animator, "rightAnkle", rightAnkle);
            Bind(agent, "animator", animator);
        }

        static Transform Joint(Transform parent, string name, Vector3 position)
        {
            var joint = new GameObject(name).transform;
            joint.SetParent(parent, false);
            joint.localPosition = position;
            return joint;
        }

        static void Bind(Component target, string field, Object value)
        {
            var info = target.GetType().GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (info == null) throw new MissingFieldException(target.GetType().Name, field + " (did you rename?)");
            info.SetValue(target, value);
        }

        // An environment box on the Default layer, tagged Wall or Glass.
        public GameObject Box(Vector3 center, Vector3 size, string tag)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            owned.Add(box);
            box.tag = tag;
            box.transform.SetPositionAndRotation(center, Quaternion.identity);
            box.transform.localScale = size;
            Physics.SyncTransforms();
            return box;
        }

        public bool Saw(ZombieAgent zombie, ZombieState to, out ZombieTransitionEvent transition)
        {
            foreach (var t in Transitions)
            {
                if (t.AgentId != zombie.AgentId || t.To != to) continue;
                transition = t;
                return true;
            }
            transition = default;
            return false;
        }

        public static bool IsCalm(ZombieState state) => state is ZombieState.Idle or ZombieState.Wander;

        public static float PlanarDistance(Vector3 a, Vector3 b)
        {
            var offset = a - b;
            offset.y = 0f;
            return offset.magnitude;
        }

        // Waits up to maxSeconds of game time, or less once condition holds.
        public static IEnumerator WaitUntil(Func<bool> condition, float maxSeconds)
        {
            var end = Time.time + maxSeconds;
            while (!condition() && Time.time < end) yield return null;
        }

        // The zombie has walked to its anchor and stands idle.
        public static IEnumerator Settle(ZombieAgent zombie)
        {
            yield return WaitUntil(() => zombie.State == ZombieState.Idle, 2f);
            Assert.That(zombie.State, Is.EqualTo(ZombieState.Idle), "the test zombie did not settle into Idle");
        }

        // Objects are destroyed at the end of the frame, so the NavMesh data is removed one frame later.
        public IEnumerator Teardown()
        {
            AIEvents.Transition -= Transitions.Add;
            AIEvents.Strike -= Strikes.Add;
            Time.timeScale = savedTimeScale;
            Time.captureDeltaTime = savedCaptureDeltaTime;
            WorldLighting.NightFactor = savedNightFactor;
            for (var i = owned.Count - 1; i >= 0; i--)
                if (owned[i] != null) Object.Destroy(owned[i]);
            yield return null;
            NavMesh.RemoveAllNavMeshData();
        }
    }

    public abstract class ZombieWorldTests
    {
        private protected ZombieTestWorld World;

        [UnityTearDown]
        public IEnumerator TearDownWorld()
        {
            if (World == null) yield break;
            yield return World.Teardown();
            World = null;
        }
    }
}
