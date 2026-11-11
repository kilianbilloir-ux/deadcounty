using System.Reflection;
using DeadCounty.World;
using UnityEngine;

namespace DeadCounty.Tests
{
    static class TestDoor
    {
        // A 1 m by 2.1 m door built in code; the opening runs along the hinge's rotated +Z.
        public static Door Create(Vector3 hinge, float yaw)
        {
            var root = new GameObject("TestDoor");
            root.SetActive(false);
            root.transform.SetPositionAndRotation(hinge, Quaternion.Euler(0f, yaw, 0f));
            var leaf = new GameObject("Leaf").transform;
            leaf.SetParent(root.transform, false);
            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.transform.SetParent(leaf, false);
            panel.transform.SetLocalPositionAndRotation(new Vector3(0f, 1.05f, 0.5f), Quaternion.identity);
            panel.transform.localScale = new Vector3(0.05f, 2.1f, 0.96f);
            var door = root.AddComponent<Door>();
            typeof(Door).GetField("leaf", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(door, leaf);
            root.SetActive(true);
            Physics.SyncTransforms();
            return door;
        }
    }
}
