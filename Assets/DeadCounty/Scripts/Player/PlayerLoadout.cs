using System;
using DeadCounty.Items;
using UnityEngine;

namespace DeadCounty.Player
{
    // Shows on the body what the player carries and does with it: the backpack only while one is carried, the
    // weapon in the right hand, replaced by the item being eaten, drunk or applied, the shoulder lamp lit with the
    // flashlight.
    [RequireComponent(typeof(PlayerInventory), typeof(PlayerFlashlight), typeof(ThirdPersonController))]
    public class PlayerLoadout : MonoBehaviour
    {
        [Tooltip("Parts of the body that make the backpack.")]
        [SerializeField] GameObject[] backpack = Array.Empty<GameObject>();
        [Tooltip("Joint the hand hangs from; a used item is held under it.")]
        [SerializeField] Transform hand;
        [SerializeField] Vector3 gripOffset = new(0f, -0.36f, 0.05f);
        [Tooltip("Tilt of a weapon in the fist (deg): it lies along +Z, which at 0 points straight ahead of a hanging arm and at 90 runs on from the forearm.")]
        [SerializeField] float weaponTilt = 20f;
        [Tooltip("Tilt once the wrist has snapped through a blow, when the weapon nearly runs on from the forearm.")]
        [SerializeField] float strikeTilt = 78f;
        [Tooltip("Seconds an item stays in the hand once used.")]
        [SerializeField] float useTime = 1.3f;
        [SerializeField] Renderer lampLens;
        [SerializeField] Material lensLit;
        [SerializeField] Material lensDark;

        PlayerInventory inventory;
        PlayerFlashlight flashlight;
        ThirdPersonController controller;
        GameObject held, weapon;
        float heldUntil;

        // True while a used item is in the hand; the pose driver raises that hand to the mouth.
        public bool Using => held != null;
        public bool BackpackWorn { get; private set; }

        void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            flashlight = GetComponent<PlayerFlashlight>();
            controller = GetComponent<ThirdPersonController>();
            if (hand == null) throw new InvalidOperationException($"PlayerLoadout on '{name}' has no hand joint");
        }

        // The inventory builds its item list in Awake, on this same object.
        void Start()
        {
            inventory.Items.Changed += ShowBackpack;
            inventory.Used += Hold;
            inventory.WeaponChanged += ShowWeapon;
            ShowBackpack();
            ShowWeapon();
        }

        void OnDestroy()
        {
            if (inventory == null || inventory.Items == null) return;
            inventory.Items.Changed -= ShowBackpack;
            inventory.Used -= Hold;
            inventory.WeaponChanged -= ShowWeapon;
        }

        void ShowBackpack()
        {
            BackpackWorn = inventory.Items.Capacity > inventory.Items.BaseCapacity;
            foreach (var part in backpack) part.SetActive(BackpackWorn);
        }

        void ShowWeapon()
        {
            if (weapon != null) Destroy(weapon);
            weapon = inventory.Weapon != null ? InHand(inventory.Weapon, "Arme_En_Main", Quaternion.Euler(weaponTilt, 0f, 0f)) : null;
        }

        void Hold(ItemDefinition item)
        {
            if (held != null) Destroy(held);
            held = InHand(item, "Objet_En_Main", Quaternion.identity);
            heldUntil = Time.time + useTime;
        }

        // Null when the item has no model.
        GameObject InHand(ItemDefinition item, string objectName, Quaternion rotation)
        {
            if (item.WorldModel == null) return null;
            var model = Instantiate(item.WorldModel, hand);
            model.name = objectName;
            model.transform.SetLocalPositionAndRotation(gripOffset, rotation);
            // The world model is made to lie on the ground and be picked up: in the hand it is only a picture.
            foreach (var collider in model.GetComponentsInChildren<Collider>()) Destroy(collider);
            foreach (var part in model.GetComponentsInChildren<Transform>()) part.gameObject.layer = hand.gameObject.layer;
            return model;
        }

        void Update()
        {
            if (held != null && Time.time >= heldUntil)
            {
                Destroy(held);
                held = null;
            }
            if (weapon != null)
            {
                // The hand holds one thing at a time.
                if (weapon.activeSelf != (held == null)) weapon.SetActive(held == null);
                // There is no wrist joint: the weapon itself is cocked with the arm, then snapped through the blow.
                var pose = controller.Pose;
                var snapped = Mathf.Lerp(weaponTilt, strikeTilt, Mathf.SmoothStep(0f, 1f, pose.Swing / 0.5f));
                weapon.transform.localRotation = Quaternion.Euler(Mathf.Lerp(weaponTilt, snapped, pose.SwingWeight), 0f, 0f);
            }
            if (lampLens == null) return;
            var lens = flashlight.IsOn ? lensLit : lensDark;
            if (lampLens.sharedMaterial != lens) lampLens.sharedMaterial = lens;
        }
    }
}
