using System;
using System.Collections.Generic;
using DeadCounty.Perception;
using DeadCounty.Player;
using DeadCounty.World;
using UnityEngine;

namespace DeadCounty.Items
{
    // Furniture searched with Interact (GDD 8.6): the player keeps still for a few seconds, or less while hurrying,
    // which is heard by the zombies, then takes everything found. The contents are drawn from the loot table with
    // a fixed seed, plus the guaranteed items; a searched piece stays empty until the player respawns.
    [RequireComponent(typeof(Collider))]
    public class LootContainer : Interactable
    {
        const float PromptAboveTop = 0.1f;

        [SerializeField] string displayName = "Placard";
        [SerializeField] LootTable table;
        [Tooltip("Same seed, same contents.")]
        [SerializeField] int seed;
        [SerializeField] ItemDefinition[] guaranteed = Array.Empty<ItemDefinition>();

        [Header("Search")]
        [SerializeField] float searchTime = 2.5f;
        [SerializeField] float hurriedSearchTime = 1f;
        [Tooltip("Radius of the noise of a hurried search (m).")]
        [SerializeField] float hurriedNoise = 8f;

        readonly List<ItemStack> found = new();
        Vector3 promptPoint;
        int sourceId;

        public bool Searched { get; private set; }

        public override string DisplayName => Searched ? $"{displayName} (fouillé)" : displayName;
        public override string Action => Searched ? null : "Fouiller";
        public override Vector3 PromptPoint => promptPoint;

        void Awake()
        {
            if (table == null) throw new InvalidOperationException($"LootContainer '{name}' has no loot table");
            var bounds = GetComponent<Collider>().bounds;
            promptPoint = new Vector3(bounds.center.x, bounds.max.y + PromptAboveTop, bounds.center.z);
            sourceId = gameObject.GetEntityId().GetHashCode();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            PlayerDeathFlow.PlayerRespawned += Refill;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            PlayerDeathFlow.PlayerRespawned -= Refill;
        }

        void Refill() => Searched = false;

        public override float UseTime(bool hurried) => Searched ? 0f : hurried ? hurriedSearchTime : searchTime;

        public override void BeginUse(bool hurried)
        {
            if (hurried) NoiseSystem.Emit(promptPoint, hurriedNoise, NoiseKind.Impact, sourceId);
        }

        public override void Interact(Transform user, bool hurried)
        {
            if (Searched || !user.TryGetComponent<IItemReceiver>(out var receiver)) return;
            Searched = true;
            found.Clear();
            foreach (var item in guaranteed) LootTable.Stack(found, item, 1);
            table.Roll(new System.Random(seed), found);
            receiver.Receive(found);
        }
    }
}
