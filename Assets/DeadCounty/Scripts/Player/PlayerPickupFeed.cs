using System;
using System.Collections.Generic;
using DeadCounty.Items;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeadCounty.Player
{
    // What the player just took, one line per kind of item with its picture and count, at the bottom right where
    // nothing else sits (after the collected items list of 7 Days to Die): routine news stays out of the centre,
    // where the prompt and the world need the eye. Taking more of an item still listed adds to its count instead of
    // stacking a new line; an empty piece of furniture says so. Lines fade a few seconds later, hidden behind the
    // menus and the inventory, which lists everything anyway.
    [RequireComponent(typeof(UIDocument))]
    public class PlayerPickupFeed : MonoBehaviour
    {
        const int LineCount = 5;
        const float ShowTime = 4f;
        const float FadeSpeed = 3f;

        sealed class Line
        {
            public VisualElement Row, Icon;
            public Label Name, Count;
            public ItemDefinition Item;
            public int Total;
            public float Until = float.NegativeInfinity, Opacity;
        }

        readonly Line[] lines = new Line[LineCount];
        PlayerHealth health;
        PlayerInventory inventory;
        ThirdPersonController controller;
        VisualElement feed;

        void Awake()
        {
            health = Require<PlayerHealth>();
            inventory = Require<PlayerInventory>();
            controller = Require<ThirdPersonController>();
        }

        T Require<T>() where T : Component
        {
            var component = GetComponentInChildren<T>(true);
            if (component == null) throw new InvalidOperationException($"PlayerPickupFeed on '{name}' needs a {typeof(T).Name} in its children");
            return component;
        }

        // Not in Awake: PlayerHudController loads the layout into the document in its own Awake.
        void Start()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            feed = root.Q<VisualElement>("pickup-feed") ?? throw new InvalidOperationException($"PlayerPickupFeed on '{name}': the HUD has no 'pickup-feed'");
            for (var i = 0; i < LineCount; i++) lines[i] = CreateLine();
        }

        void OnEnable() => inventory.Received += OnReceived;

        void OnDisable() => inventory.Received -= OnReceived;

        Line CreateLine()
        {
            var line = new Line { Row = new VisualElement { pickingMode = PickingMode.Ignore } };
            line.Row.AddToClassList("pickup");
            line.Row.style.display = DisplayStyle.None;
            line.Icon = new VisualElement { pickingMode = PickingMode.Ignore };
            line.Icon.AddToClassList("pickup-icon");
            line.Name = new Label { pickingMode = PickingMode.Ignore };
            line.Name.AddToClassList("pickup-name");
            line.Count = new Label { pickingMode = PickingMode.Ignore };
            line.Count.AddToClassList("pickup-count");
            line.Row.Add(line.Icon);
            line.Row.Add(line.Name);
            line.Row.Add(line.Count);
            feed.Add(line.Row);
            return line;
        }

        void OnReceived(IReadOnlyList<ItemStack> items)
        {
            if (items.Count == 0) Show(null, 0);
            for (var i = 0; i < items.Count; i++) Show(items[i].Item, items[i].Count);
        }

        // A null item is a search that found nothing.
        void Show(ItemDefinition item, int count)
        {
            var now = Time.time;
            var line = Listed(item, now) ?? Oldest();
            if (line.Item != item || !Live(line, now))
            {
                line.Item = item;
                line.Total = 0;
                line.Opacity = 0f;
                line.Name.text = item != null ? item.DisplayName : "Rien trouvé";
                line.Icon.style.backgroundImage = item != null && item.Icon != null ? new StyleBackground(item.Icon) : StyleKeyword.None;
                line.Row.EnableInClassList("pickup--empty", item == null);
            }
            line.Total += count;
            line.Count.text = $"×{line.Total}";
            line.Count.style.display = line.Total > 1 ? DisplayStyle.Flex : DisplayStyle.None;
            line.Until = now + ShowTime;
            // Last child: the bottom line, nearest the corner.
            line.Row.BringToFront();
        }

        Line Listed(ItemDefinition item, float now)
        {
            foreach (var line in lines)
                if (line.Item == item && Live(line, now)) return line;
            return null;
        }

        static bool Live(Line line, float now) => now < line.Until || line.Opacity > 0f;

        Line Oldest()
        {
            var oldest = lines[0];
            foreach (var line in lines)
                if (line.Until < oldest.Until) oldest = line;
            return oldest;
        }

        void Update()
        {
            feed.style.display = health.IsAlive && !controller.MenuOpen ? DisplayStyle.Flex : DisplayStyle.None;
            var now = Time.time;
            var step = FadeSpeed * Time.deltaTime;
            foreach (var line in lines)
            {
                line.Opacity = Mathf.MoveTowards(line.Opacity, now < line.Until ? 1f : 0f, step);
                line.Row.style.opacity = line.Opacity;
                line.Row.style.display = Live(line, now) ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
    }
}
