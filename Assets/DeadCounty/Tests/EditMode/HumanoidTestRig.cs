using System;
using DeadCounty.Animation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadCounty.Tests
{
    // The joint layout of TestSceneBuilder.BuildSurvivor (transforms only) animated by a
    // ProceduralHumanoidAnimator and driven by a FakeHumanoidMotion. Lives in a preview scene so tests never
    // touch the open scene; the ground mask is cleared so foot rays never hit its colliders. Dispose also
    // destroys the gait profile it was given.
    sealed class HumanoidTestRig : IDisposable
    {
        public const float Dt = 1f / 60f;

        public readonly Transform Root, Model, Hips, Spine, Neck;
        public readonly Transform LeftShoulder, RightShoulder, LeftElbow, RightElbow;
        public readonly Transform LeftHip, RightHip, LeftKnee, RightKnee, LeftAnkle, RightAnkle;
        public readonly ProceduralHumanoidAnimator Animator;
        public readonly FakeHumanoidMotion Motion = new();
        public readonly GaitProfile Gait;
        Scene scene;

        public HumanoidTestRig(GaitProfile gait)
        {
            Gait = gait;
            scene = EditorSceneManager.NewPreviewScene();
            var root = EditorUtility.CreateGameObjectWithHideFlags("Rig", HideFlags.DontSave);
            SceneManager.MoveGameObjectToScene(root, scene);
            Root = root.transform;
            Model = Joint(Root, "Model", Vector3.zero);
            Hips = Joint(Model, "Hanches", new Vector3(0f, 1f, 0f));
            Spine = Joint(Hips, "Colonne", new Vector3(0f, 0.06f, 0f));
            Neck = Joint(Spine, "Cou", new Vector3(0f, 0.42f, 0f));
            LeftShoulder = Joint(Spine, "Epaule_G", new Vector3(-0.24f, 0.36f, 0f));
            RightShoulder = Joint(Spine, "Epaule_D", new Vector3(0.24f, 0.36f, 0f));
            LeftElbow = Joint(LeftShoulder, "Coude_G", new Vector3(0f, -0.28f, 0f));
            RightElbow = Joint(RightShoulder, "Coude_D", new Vector3(0f, -0.28f, 0f));
            LeftHip = Joint(Hips, "Hanche_G", new Vector3(-0.095f, -0.05f, 0f));
            RightHip = Joint(Hips, "Hanche_D", new Vector3(0.095f, -0.05f, 0f));
            LeftKnee = Joint(LeftHip, "Genou_G", new Vector3(0f, -0.44f, 0f));
            RightKnee = Joint(RightHip, "Genou_D", new Vector3(0f, -0.44f, 0f));
            LeftAnkle = Joint(LeftKnee, "Cheville_G", new Vector3(0f, -0.43f, 0f));
            RightAnkle = Joint(RightKnee, "Cheville_D", new Vector3(0f, -0.43f, 0f));

            Animator = root.AddComponent<ProceduralHumanoidAnimator>();
            var so = new SerializedObject(Animator);
            so.FindProperty("groundMask").intValue = 0;
            so.FindProperty("gait").objectReferenceValue = gait;
            Bind(so, "hips", Hips);
            Bind(so, "spine", Spine);
            Bind(so, "neck", Neck);
            Bind(so, "leftShoulder", LeftShoulder);
            Bind(so, "rightShoulder", RightShoulder);
            Bind(so, "leftElbow", LeftElbow);
            Bind(so, "rightElbow", RightElbow);
            Bind(so, "leftHip", LeftHip);
            Bind(so, "rightHip", RightHip);
            Bind(so, "leftKnee", LeftKnee);
            Bind(so, "rightKnee", RightKnee);
            Bind(so, "leftAnkle", LeftAnkle);
            Bind(so, "rightAnkle", RightAnkle);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Awake does not run in edit mode.
        public void Initialize() => Animator.Initialize(Motion);

        // Moves the root along its forward at `speed` while turning at `yawRate` (deg/s), then animates.
        public void Step(float speed, float yawRate = 0f)
        {
            Root.rotation = Quaternion.Euler(0f, Root.eulerAngles.y + yawRate * Dt, 0f);
            Root.position += Root.forward * (speed * Dt);
            Motion.CurrentSpeed = speed;
            Animator.Evaluate(Dt);
        }

        // Every joint the animator writes: model and hips local position and rotation, then each limb rotation.
        public float[] Sample()
        {
            var values = new float[SampleLength];
            var i = 0;
            Position(Model.localPosition);
            Rotation(Model.localRotation);
            Position(Hips.localPosition);
            Rotation(Hips.localRotation);
            foreach (var joint in new[] { Spine, Neck, LeftShoulder, RightShoulder, LeftElbow, RightElbow, LeftHip, RightHip, LeftKnee, RightKnee, LeftAnkle, RightAnkle })
                Rotation(joint.localRotation);
            return values;

            void Position(Vector3 p)
            {
                values[i++] = p.x;
                values[i++] = p.y;
                values[i++] = p.z;
            }

            void Rotation(Quaternion q)
            {
                values[i++] = q.x;
                values[i++] = q.y;
                values[i++] = q.z;
                values[i++] = q.w;
            }
        }

        public const int SampleLength = 2 * 3 + 14 * 4;

        // Index in Sample() of the first component of the rotation holding `index`, -1 for a position.
        public static int RotationStartOf(int index) => index is >= 3 and < 7 ? 3 : index is >= 10 and < 14 ? 10 : index >= 14 ? index - (index - 14) % 4 : -1;

        public void Dispose()
        {
            UnityEngine.Object.DestroyImmediate(Root.gameObject);
            UnityEngine.Object.DestroyImmediate(Gait);
            EditorSceneManager.ClosePreviewScene(scene);
        }

        static Transform Joint(Transform parent, string name, Vector3 position)
        {
            var t = EditorUtility.CreateGameObjectWithHideFlags(name, HideFlags.DontSave).transform;
            t.SetParent(parent, false);
            t.localPosition = position;
            return t;
        }

        static void Bind(SerializedObject so, string field, Transform joint) => so.FindProperty(field).objectReferenceValue = joint;
    }
}
