using System;
using DeadCounty.World;
using UnityEngine;

namespace DeadCounty.Items
{
    // Items lying on the ground, picked up with Interact; dropped items are spawned as a plain box.
    public class WorldItem : Interactable
    {
        const float PromptHeight = 0.3f;
        static readonly Vector3 DroppedSize = new(0.22f, 0.14f, 0.18f);

        [SerializeField] ItemDefinition item;
        [Min(1)]
        [SerializeField] int count = 1;

        public ItemDefinition Item => item;
        public int Count => count;

        public override string DisplayName => count > 1 ? $"{item.DisplayName} ×{count}" : item.DisplayName;
        public override string Action => "Ramasser";
        public override Vector3 PromptPoint => transform.position + Vector3.up * PromptHeight;

        void Awake()
        {
            if (item == null) throw new InvalidOperationException($"WorldItem '{name}' has no item");
        }

        public override void Interact(Transform user, bool hurried)
        {
            if (!user.TryGetComponent<IItemReceiver>(out var receiver)) return;
            receiver.Receive(new[] { new ItemStack(item, count) });
            Destroy(gameObject);
        }

        public static WorldItem Spawn(ItemDefinition definition, int count, Vector3 position, Quaternion rotation)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.SetActive(false);
            box.name = $"Objet_{definition.DisplayName}";
            box.transform.SetPositionAndRotation(position + Vector3.up * (DroppedSize.y * 0.5f), rotation);
            box.transform.localScale = DroppedSize;
            var worldItem = box.AddComponent<WorldItem>();
            worldItem.item = definition;
            worldItem.count = count;
            box.SetActive(true);
            return worldItem;
        }
    }
}
