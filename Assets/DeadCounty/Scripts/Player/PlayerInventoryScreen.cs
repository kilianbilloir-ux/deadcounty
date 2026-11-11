using System;
using System.Collections.Generic;
using System.Globalization;
using DeadCounty.Items;
using DeadCounty.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace DeadCounty.Player
{
    // Inventory screen (GDD 8.5), drawn in the HUD document: the character's state on the left, the items on the
    // right as a list with their pictures, sortable by name, category or weight, with the carried weight against the
    // capacity, red when overloaded. Under the list, the selected item in large with what using it would do to the
    // gauges as they are now. Inventory opens and closes it, Cancel closes it; the player stands still with a free
    // cursor meanwhile, the world keeps running. Up and down pick an item, Interact uses it, Drop leaves it on the
    // ground, Previous and Next change the sort; the mouse can click rows and column headers, and double-click to use.
    [RequireComponent(typeof(UIDocument))]
    public class PlayerInventoryScreen : MonoBehaviour
    {
        public enum SortMode { Name, Category, Weight }

        const float NavigateThreshold = 0.5f;
        const float RepeatDelay = 0.35f;
        const float RepeatInterval = 0.12f;
        const float LowHealth = 0.4f;
        const float LowNeed = 0.2f;
        // Hunger, thirst, health, bleeding and capacity: all an item can do.
        const int EffectCount = 5;
        static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

        ThirdPersonController controller;
        PlayerHealth health;
        PlayerStamina stamina;
        PlayerNeeds needs;
        PlayerInventory inventory;
        InputAction toggle, cancel, move, use, drop, previous, next;
        VisualElement screen, healthFill, staminaFill, hungerFill, thirstFill, fatigueFill, capacityFill, detail, detailIcon;
        Label weight, encumbered, empty, detailName, detailMeta, sortName, sortCategory, sortWeight;
        Label healthWord, staminaWord, hungerWord, thirstWord, fatigueWord;
        KeyHint useHint, dropHint, sortHint, closeHint;
        ScrollView list;
        readonly List<ItemStack> sorted = new();
        readonly List<VisualElement> rows = new();
        readonly VisualElement[] effects = new VisualElement[EffectCount];
        readonly Label[] effectNames = new Label[EffectCount], effectValues = new Label[EffectCount];
        // Rounded hunger, thirst and health and the bleeding the detail was written for.
        (int, int, int, bool) detailState;
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
            detail = Find<VisualElement>(root, "item-detail");
            detailIcon = Find<VisualElement>(root, "item-detail-icon");
            detailName = Find<Label>(root, "item-detail-name");
            detailMeta = Find<Label>(root, "item-detail-meta");
            var effectList = Find<VisualElement>(root, "item-effects");
            for (var i = 0; i < EffectCount; i++) effectList.Add(CreateEffect(i));
            capacityFill = Find<VisualElement>(root, "inventory-capacity");
            healthWord = Find<Label>(root, "stat-health-word");
            staminaWord = Find<Label>(root, "stat-stamina-word");
            hungerWord = Find<Label>(root, "stat-hunger-word");
            thirstWord = Find<Label>(root, "stat-thirst-word");
            fatigueWord = Find<Label>(root, "stat-fatigue-word");
            var hints = Find<VisualElement>(root, "inventory-hints");
            useHint = new KeyHint(hints);
            dropHint = new KeyHint(hints, "Jeter");
            sortHint = new KeyHint(hints, "Trier");
            closeHint = new KeyHint(hints, "Fermer");
            ShowKeys();
            InputPrompts.KeysChanged += ShowKeys;
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
            InputPrompts.KeysChanged -= ShowKeys;
        }

        void ShowKeys()
        {
            useHint.SetKey(InputPrompts.Key("Player/Interact"));
            dropHint.SetKey(InputPrompts.Key("Player/Drop"));
            sortHint.SetKey($"{InputPrompts.Key("Player/Previous")} {InputPrompts.Key("Player/Next")}");
            closeHint.SetKey(InputPrompts.Key("Player/Inventory"));
        }

        T Find<T>(VisualElement root, string elementName) where T : VisualElement =>
            root.Q<T>(elementName) ?? throw new InvalidOperationException($"PlayerInventoryScreen on '{name}': the HUD has no '{elementName}' {typeof(T).Name}");

        void MarkDirty() => dirty = true;

        void Update()
        {
            // Dead, on the death screen or asleep: no inventory.
            if (!health.IsAlive || !controller.InputEnabled || controller.Resting)
            {
                if (IsOpen) SetOpen(false);
                return;
            }
            // Another menu (pause) already holds the screen.
            if (toggle.WasPressedThisFrame() && (IsOpen || !controller.MenuOpen)) SetOpen(!IsOpen);
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
                row[0].style.backgroundImage = Picture(stack.Item);
                row.EnableInClassList("item-row--selected", i == selected);
            }
            if (selected < sorted.Count) list.ScrollTo(rows[selected]);
            empty.style.display = sorted.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;

            var items = inventory.Items;
            weight.text = $"{items.Weight.ToString("0.0", French)} / {items.Capacity.ToString("0.#", French)} kg";
            weight.EnableInClassList("inventory-weight--over", items.Overloaded);
            capacityFill.style.width = Length.Percent(Mathf.Clamp01(items.Weight / items.Capacity) * 100f);
            capacityFill.EnableInClassList("capacity-fill--over", items.Overloaded);
            encumbered.style.display = items.Overloaded ? DisplayStyle.Flex : DisplayStyle.None;
            sortName.EnableInClassList("item-column--active", Sort == SortMode.Name);
            sortCategory.EnableInClassList("item-column--active", Sort == SortMode.Category);
            sortWeight.EnableInClassList("item-column--active", Sort == SortMode.Weight);
            var item = Selected;
            ShowDetail(item);
            useHint.Show(item != null && item.Usable, item != null ? item.UseVerb : null);
            dropHint.Show(item != null);
            dirty = false;
        }

        VisualElement CreateRow(int index)
        {
            var row = new VisualElement();
            row.AddToClassList("item-row");
            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("item-icon");
            row.Add(icon);
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

        static StyleBackground Picture(ItemDefinition item) => item.Icon != null ? new StyleBackground(item.Icon) : StyleKeyword.None;

        VisualElement CreateEffect(int index)
        {
            var row = effects[index] = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("item-effect");
            row.style.display = DisplayStyle.None;
            var label = effectNames[index] = new Label { pickingMode = PickingMode.Ignore };
            label.AddToClassList("item-effect-name");
            var value = effectValues[index] = new Label { pickingMode = PickingMode.Ignore };
            value.AddToClassList("item-effect-value");
            row.Add(label);
            row.Add(value);
            return row;
        }

        // What using the item would do, stated like the food tooltips of Project Zomboid but measured on the gauges as
        // they are now: a full gauge or no bleeding to stop reads as wasted, in grey.
        void ShowDetail(ItemDefinition item)
        {
            var model = needs.Model;
            var pool = health.Pool;
            detailState = (Percent(model.Hunger), Percent(model.Thirst), Percent(pool.Normalized), health.Bleeding);
            // Hidden rather than removed, so the list keeps its height.
            detail.style.visibility = item != null ? Visibility.Visible : Visibility.Hidden;
            if (item == null) return;
            detailIcon.style.backgroundImage = Picture(item);
            detailName.text = item.DisplayName;
            detailMeta.text = $"{ItemDefinition.CategoryName(item.Category)} · {item.Weight.ToString("0.##", French)} kg";
            var shown = 0;
            if (item.Hunger > 0f) Gain(ref shown, "Faim", model.Hunger, item.Hunger);
            if (item.Thirst > 0f) Gain(ref shown, "Soif", model.Thirst, item.Thirst);
            if (item.Health > 0f) Gain(ref shown, "Santé", pool.Normalized, item.Health / pool.Max);
            if (item.StopsBleeding) Effect(ref shown, "Saignement", health.Bleeding ? "En cours → arrêté" : "Aucun", health.Bleeding);
            if (item.CapacityBonus > 0f) Effect(ref shown, "Capacité", $"+{item.CapacityBonus.ToString("0.#", French)} kg", true);
            for (var i = shown; i < EffectCount; i++) effects[i].style.display = DisplayStyle.None;
        }

        // Capped at full, as eating and healing are.
        void Gain(ref int shown, string label, float now, float gain)
        {
            var before = Percent(now);
            var after = Percent(Mathf.Min(now + gain, 1f));
            Effect(ref shown, label, $"{before}\u00A0% → {after}\u00A0%", after > before);
        }

        void Effect(ref int shown, string label, string value, bool useful)
        {
            effectNames[shown].text = label;
            effectValues[shown].text = value;
            effects[shown].EnableInClassList("item-effect--wasted", !useful);
            effects[shown].style.display = DisplayStyle.Flex;
            shown++;
        }

        static int Percent(float value) => Mathf.RoundToInt(value * 100f);

        // Words rather than numbers (GDD 8.4): the bar says how much, the word says what it means.
        void UpdateStats()
        {
            var model = needs.Model;
            var healthValue = health.Pool.Normalized;
            Stat(healthFill, healthWord, healthValue, healthValue <= LowHealth, healthValue >= 0.7f ? "Bonne" : healthValue > LowHealth ? "Blessé" : "Grave");
            var staminaModel = stamina.Model;
            Stat(staminaFill, staminaWord, staminaModel.Normalized, staminaModel.Exhausted, staminaModel.Exhausted ? "À bout de souffle" : "");
            Stat(hungerFill, hungerWord, model.Hunger, model.Hunger <= LowNeed, Word(model.Hunger, "Rassasié", "Petit creux", "Affamé"));
            Stat(thirstFill, thirstWord, model.Thirst, model.Thirst <= LowNeed, Word(model.Thirst, "Désaltéré", "Gorge sèche", "Assoiffé"));
            Stat(fatigueFill, fatigueWord, model.Fatigue, model.Fatigue <= LowNeed, Word(model.Fatigue, "Reposé", "Fatigué", "Épuisé"));
            if ((Percent(model.Hunger), Percent(model.Thirst), Percent(healthValue), health.Bleeding) != detailState) ShowDetail(Selected);
        }

        static string Word(float value, string fine, string middle, string low) => value >= 0.5f ? fine : value > LowNeed ? middle : low;

        static void Stat(VisualElement fill, Label word, float value, bool low, string text)
        {
            fill.style.width = Length.Percent(Mathf.Clamp01(value) * 100f);
            fill.EnableInClassList("stat-fill--low", low);
            word.text = text;
            word.EnableInClassList("stat-word--low", low);
        }
    }
}
