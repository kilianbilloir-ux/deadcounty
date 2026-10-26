using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using DeadCounty.Items;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace DeadCounty.Player
{
    // Inventory screen (GDD 8.5), drawn in the HUD document: the character's state on the left, the items on the
    // right as a list sortable by name, category or weight, with the carried weight against the capacity, red when
    // overloaded. Inventory opens and closes it, Cancel closes it; the player stands still with a free cursor
    // meanwhile, the world keeps running. Up and down pick an item, Interact uses it, Drop leaves it on the ground,
    // Previous and Next change the sort; the mouse can click rows and column headers, and double-click to use.
    [RequireComponent(typeof(UIDocument))]
    public class PlayerInventoryScreen : MonoBehaviour
    {
        public enum SortMode { Name, Category, Weight }

        const float NavigateThreshold = 0.5f;
        const float RepeatDelay = 0.35f;
        const float RepeatInterval = 0.12f;
        const float LowHealth = 0.4f;
        const float LowNeed = 0.2f;
        const string Separator = "   ·   ";
        static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

        ThirdPersonController controller;
        PlayerHealth health;
        PlayerStamina stamina;
        PlayerNeeds needs;
        PlayerInventory inventory;
        InputAction toggle, cancel, move, use, drop, previous, next;
        VisualElement screen, healthFill, staminaFill, hungerFill, thirstFill, fatigueFill;
        Label weight, encumbered, empty, detail, hints, sortName, sortCategory, sortWeight;
        ScrollView list;
        readonly List<ItemStack> sorted = new();
        readonly List<VisualElement> rows = new();
        readonly StringBuilder text = new();
        string useKeys, dropKeys, sortKeys, closeKeys;
        int selected, heldDirection;
        float repeatAt;
        bool dirty;

        public bool IsOpen { get; private set; }
        public SortMode Sort { get; private set; }
        public ItemDefinition Selected => selected < sorted.Count ? sorted[selected].Item : null;

        void Awake()
        {
            controller = Require<ThirdPersonController>();
            health = Require<PlayerHealth>();
            stamina = Require<PlayerStamina>();
            needs = Require<PlayerNeeds>();
            inventory = Require<PlayerInventory>();
            var actions = InputSystem.actions;
            toggle = actions.FindAction("Player/Inventory", true);
            cancel = actions.FindAction("UI/Cancel", true);
            move = actions.FindAction("Player/Move", true);
            use = actions.FindAction("Player/Interact", true);
            drop = actions.FindAction("Player/Drop", true);
            previous = actions.FindAction("Player/Previous", true);
            next = actions.FindAction("Player/Next", true);
            useKeys = InputPrompts.Keys("Player/Interact");
            dropKeys = InputPrompts.Keys("Player/Drop");
            sortKeys = InputPrompts.KeyboardKeys("Player/Previous", "Player/Next");
            closeKeys = InputPrompts.Keys("Player/Inventory");
        }

        T Require<T>() where T : Component
        {
            var component = GetComponentInChildren<T>(true);
            if (component == null) throw new InvalidOperationException($"PlayerInventoryScreen on '{name}' needs a {typeof(T).Name} in its children");
            return component;
        }

        // Not in Awake: PlayerHudController loads the layout into the document in its own Awake.
        void Start()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            screen = Find<VisualElement>(root, "inventory");
            healthFill = Find<VisualElement>(root, "stat-health");
            staminaFill = Find<VisualElement>(root, "stat-stamina");
            hungerFill = Find<VisualElement>(root, "stat-hunger");
            thirstFill = Find<VisualElement>(root, "stat-thirst");
            fatigueFill = Find<VisualElement>(root, "stat-fatigue");
            weight = Find<Label>(root, "inventory-weight");
            encumbered = Find<Label>(root, "inventory-encumbered");
            empty = Find<Label>(root, "inventory-empty");
            detail = Find<Label>(root, "item-detail");
            hints = Find<Label>(root, "inventory-hints");
            sortName = Find<Label>(root, "sort-name");
            sortCategory = Find<Label>(root, "sort-category");
            sortWeight = Find<Label>(root, "sort-weight");
            list = Find<ScrollView>(root, "item-list");
            sortName.RegisterCallback<ClickEvent>(_ => SetSort(SortMode.Name));
            sortCategory.RegisterCallback<ClickEvent>(_ => SetSort(SortMode.Category));
            sortWeight.RegisterCallback<ClickEvent>(_ => SetSort(SortMode.Weight));
            inventory.Items.Changed += MarkDirty;
            screen.style.display = DisplayStyle.None;
        }

        void OnDestroy()
        {
            if (inventory != null && inventory.Items != null) inventory.Items.Changed -= MarkDirty;
        }

        T Find<T>(VisualElement root, string elementName) where T : VisualElement =>
            root.Q<T>(elementName) ?? throw new InvalidOperationException($"PlayerInventoryScreen on '{name}': the HUD has no '{elementName}' {typeof(T).Name}");

        void MarkDirty() => dirty = true;

        void Update()
        {
            // Dead or on the death screen: no inventory.
            if (!health.IsAlive || !controller.InputEnabled)
            {
                if (IsOpen) SetOpen(false);
                return;
            }
            if (toggle.WasPressedThisFrame()) SetOpen(!IsOpen);
            else if (IsOpen && cancel.WasPressedThisFrame()) SetOpen(false);
            if (!IsOpen) return;

            Navigate();
            if (previous.WasPressedThisFrame()) SetSort((SortMode)(((int)Sort + 2) % 3));
            if (next.WasPressedThisFrame()) SetSort((SortMode)(((int)Sort + 1) % 3));
            if (use.WasPressedThisFrame()) UseSelected();
            if (drop.WasPressedThisFrame()) DropSelected();
            if (dirty) Rebuild();
            UpdateStats();
        }

        public void SetOpen(bool open)
        {
            IsOpen = open;
            controller.MenuOpen = open;
            screen.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            heldDirection = 0;
            if (open) Rebuild();
        }

        public void SetSort(SortMode mode)
        {
            Sort = mode;
            dirty = true;
        }

        public bool UseSelected() => Selected != null && inventory.Use(Selected);

        public bool DropSelected() => Selected != null && inventory.Drop(Selected);

        public void Select(int index)
        {
            selected = Mathf.Clamp(index, 0, Mathf.Max(sorted.Count - 1, 0));
            dirty = true;
        }

        // Up and down step through the list, repeating while held.
        void Navigate()
        {
            var y = move.ReadValue<Vector2>().y;
            var direction = y > NavigateThreshold ? -1 : y < -NavigateThreshold ? 1 : 0;
            if (direction == 0)
            {
                heldDirection = 0;
                return;
            }
            if (direction == heldDirection && Time.unscaledTime < repeatAt) return;
            repeatAt = Time.unscaledTime + (direction == heldDirection ? RepeatInterval : RepeatDelay);
            heldDirection = direction;
            Select(selected + direction);
        }

        // The selection follows its item through sorting and use; a used-up item leaves it on the same row.
        void Rebuild()
        {
            var selectedItem = Selected;
            sorted.Clear();
            sorted.AddRange(inventory.Items.Stacks);
            sorted.Sort(Compare);
            var index = selectedItem != null ? sorted.FindIndex(s => s.Item == selectedItem) : -1;
            selected = index >= 0 ? index : Mathf.Clamp(selected, 0, Mathf.Max(sorted.Count - 1, 0));

            while (rows.Count < sorted.Count) rows.Add(CreateRow(rows.Count));
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var shown = i < sorted.Count;
                row.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
                if (!shown) continue;
                var stack = sorted[i];
                var labels = row.Query<Label>().ToList();
                labels[0].text = stack.Count > 1 ? $"{stack.Item.DisplayName} ×{stack.Count}" : stack.Item.DisplayName;
                labels[1].text = ItemDefinition.CategoryName(stack.Item.Category);
                labels[2].text = $"{stack.Weight.ToString("0.##", French)} kg";
                row.EnableInClassList("item-row--selected", i == selected);
            }
            if (selected < sorted.Count) list.ScrollTo(rows[selected]);
            empty.style.display = sorted.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;

            var items = inventory.Items;
            weight.text = $"{items.Weight.ToString("0.0", French)} / {items.Capacity.ToString("0.#", French)} kg";
            weight.EnableInClassList("inventory-weight--over", items.Overloaded);
            encumbered.style.display = items.Overloaded ? DisplayStyle.Flex : DisplayStyle.None;
            sortName.EnableInClassList("item-column--active", Sort == SortMode.Name);
            sortCategory.EnableInClassList("item-column--active", Sort == SortMode.Category);
            sortWeight.EnableInClassList("item-column--active", Sort == SortMode.Weight);
            detail.text = Describe(Selected);
            hints.text = Hints(Selected);
            dirty = false;
        }

        VisualElement CreateRow(int index)
        {
            var row = new VisualElement();
            row.AddToClassList("item-row");
            foreach (var column in new[] { "item-name", "item-category", "item-weight" })
            {
                var label = new Label();
                label.AddToClassList(column);
                label.pickingMode = PickingMode.Ignore;
                row.Add(label);
            }
            row.RegisterCallback<ClickEvent>(e =>
            {
                Select(index);
                if (e.clickCount == 2) UseSelected();
            });
            list.Add(row);
            return row;
        }

        int Compare(ItemStack a, ItemStack b)
        {
            var byName = French.CompareInfo.Compare(a.Item.DisplayName, b.Item.DisplayName, CompareOptions.IgnoreCase);
            return Sort switch
            {
                SortMode.Category when a.Item.Category != b.Item.Category => a.Item.Category.CompareTo(b.Item.Category),
                SortMode.Weight when !Mathf.Approximately(a.Weight, b.Weight) => b.Weight.CompareTo(a.Weight),
                _ => byName,
            };
        }

        string Describe(ItemDefinition item)
        {
            if (item == null) return "";
            text.Clear();
            text.Append(ItemDefinition.CategoryName(item.Category)).Append(" · ").Append(item.Weight.ToString("0.##", French)).Append(" kg");
            if (item.Hunger > 0f) text.Append(" · Faim +").Append(Mathf.RoundToInt(item.Hunger * 100f));
            if (item.Thirst > 0f) text.Append(" · Soif +").Append(Mathf.RoundToInt(item.Thirst * 100f));
            if (item.Health > 0f) text.Append(" · Santé +").Append(Mathf.RoundToInt(item.Health));
            if (item.CapacityBonus > 0f) text.Append(" · Capacité +").Append(item.CapacityBonus.ToString("0.#", French)).Append(" kg");
            return text.ToString();
        }

        string Hints(ItemDefinition item)
        {
            text.Clear();
            if (item != null && item.Usable) text.Append(InputPrompts.Prompt(useKeys, item.UseVerb)).Append(Separator);
            if (item != null) text.Append(InputPrompts.Prompt(dropKeys, "Jeter")).Append(Separator);
            text.Append(InputPrompts.Prompt(sortKeys, "Trier")).Append(Separator);
            text.Append(InputPrompts.Prompt(closeKeys, "Fermer"));
            return text.ToString();
        }

        void UpdateStats()
        {
            var model = needs.Model;
            Bar(healthFill, health.Pool.Normalized, LowHealth);
            Bar(staminaFill, stamina.Model.Normalized, 0f);
            Bar(hungerFill, model.Hunger, LowNeed);
            Bar(thirstFill, model.Thirst, LowNeed);
            Bar(fatigueFill, model.Fatigue, LowNeed);
        }

        static void Bar(VisualElement fill, float value, float low)
        {
            fill.style.width = Length.Percent(Mathf.Clamp01(value) * 100f);
            fill.EnableInClassList("stat-fill--low", value <= low);
        }
    }
}
