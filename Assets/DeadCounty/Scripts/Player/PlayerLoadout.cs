using System;
using DeadCounty.Items;
using UnityEngine;

namespace DeadCounty.Player
{
    // Shows on the body what the player carries and does with it: the backpack only while one is carried, the item
    // being eaten, drunk or applied in the right hand, the shoulder lamp lit with the flashlight.
    [RequireComponent(typeof(PlayerInventory), typeof(PlayerFlashlight))]
    public class PlayerLoadout : MonoBehaviour
    {
        [Tooltip("Parts of the body that make the backpack.")]
        [SerializeField] GameObject[] backpack = Array.Empty<GameObject>();
        [Tooltip("Joint the hand hangs from; a used item is held under it.")]
        [SerializeField] Transform hand;
        [SerializeField] Vector3 gripOffset = new(0f, -0.36f, 0.05f);
        [Tooltip("Seconds an item stays in the hand once used.")]
        [SerializeField] float useTime = 1.3f;
        [SerializeField] Renderer lampLens;
        [SerializeField] Material lensLit;
        [SerializeField] Material lensDark;

        PlayerInventory inventory;
        PlayerFlashlight flashlight;
        GameObject held;
        float heldUntil;

        // True while a used item is in the hand; the pose driver raises that hand to the mouth.
        public bool Using => held != null;
        public bool BackpackWorn { get; private set; }

        void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            flashlight = GetComponent<PlayerFlashlight>();
            if (hand == null) throw new InvalidOperationException($"PlayerLoadout on '{name}' has no hand joint");
        }

        // The inventory builds its item list in Awake, on this same object.
        void Start()
        {
            inventory.Items.Changed += ShowBackpack;
            inventory.Used += Hold;
            ShowBackpack();
        }

        void OnDestroy()
        {
            if (inventory == null || inventory.Items == null) return;
            inventory.Items.Changed -= ShowBackpack;
            inventory.Used -= Hold;
        }

        void ShowBackpack()
        {
            BackpackWorn = inventory.Items.Capacity > inventory.Items.BaseCapacity;
            foreach (var part in backpack) part.SetActive(BackpackWorn);
        }

        void Hold(ItemDefinition item)
        {
            if (held != null) Destroy(held);
            held = null;
            if (item.WorldModel == null) return;
            held = Instantiate(item.WorldModel, hand);
            held.name = "Objet_En_Main";
            held.transform.SetLocalPositionAndRotation(gripOffset, Quaternion.identity);
            // The world model is made to lie on the ground and be picked up: in the hand it is only a picture.
            foreach (var collider in held.GetComponentsInChildren<Collider>()) Destroy(collider);
            foreach (var part in held.GetComponentsInChildren<Transform>()) part.gameObject.layer = hand.gameObject.layer;
            heldUntil = Time.time + useTime;
        }

        void Update()
        {
            if (held != null && Time.time >= heldUntil)
            {
                Destroy(held);
                held = null;
            }
            if (lampLens == null) return;
            var lens = flashlight.IsOn ? lensLit : lensDark;
            if (lampLens.sharedMaterial != lens) lampLens.sharedMaterial = lens;
        }
    }
}
