using System;
using System.Collections.Generic;
using DeadCounty.Player;
using DeadCounty.World;
using UnityEngine;

namespace DeadCounty.Items
{
    // Items lying on the ground, picked up with Interact, drawn with their item's WorldModel (a plain box without
    // one). Items placed in the level come back when the player respawns, like the loot of the furniture; dropped
    // items are gone once picked up.
    public class WorldItem : Interactable
    {
        const float PromptHeight = 0.3f;
        static readonly Vector3 DroppedSize = new(0.22f, 0.14f, 0.18f);
        static readonly List<WorldItem> Taken = new();

        [SerializeField] ItemDefinition item;
        [Min(1)]
        [SerializeField] int count = 1;
        [Tooltip("Placed in the level: hidden when taken, back when the player respawns.")]
        [SerializeField] bool placed;

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
            if (!placed)
            {
                Destroy(gameObject);
                return;
            }
            Taken.Add(this);
            gameObject.SetActive(false);
        }

        public static WorldItem Spawn(ItemDefinition definition, int count, Vector3 position, Quaternion rotation)
        {
            GameObject body;
            if (definition.WorldModel != null)
            {
                body = Instantiate(definition.WorldModel, position, rotation);
                body.SetActive(false);
            }
            else
            {
                body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.SetActive(false);
                body.transform.SetPositionAndRotation(position + Vector3.up * (DroppedSize.y * 0.5f), rotation);
                body.transform.localScale = DroppedSize;
            }
            body.name = $"Objet_{definition.DisplayName}";
            var worldItem = body.AddComponent<WorldItem>();
            worldItem.item = definition;
            worldItem.count = count;
            body.SetActive(true);
            return worldItem;
        }

        // Taken items of a level reloaded since are destroyed, hence the null check.
        static void BringBack()
        {
            foreach (var taken in Taken)
                if (taken != null) taken.gameObject.SetActive(true);
            Taken.Clear();
        }

        // Domain reload is disabled on entering Play mode: forget the last session's taken items. PlayerRespawned is
        // cleared at SubsystemRegistration, so the listener is added back after it.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Taken.Clear();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ListenForRespawn() => PlayerDeathFlow.PlayerRespawned += BringBack;
    }
}
