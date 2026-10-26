using UnityEngine.UIElements;

namespace DeadCounty.UI
{
    // A key in its frame followed by what it does, for the hint lines of the inventory and the menus.
    public sealed class KeyHint
    {
        readonly VisualElement row;
        readonly Label key, action;

        public KeyHint(VisualElement parent, string label = "")
        {
            row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("prompt-row");
            key = new Label { pickingMode = PickingMode.Ignore };
            key.AddToClassList("keycap");
            action = new Label(label) { pickingMode = PickingMode.Ignore };
            action.AddToClassList("prompt-action");
            row.Add(key);
            row.Add(action);
            parent.Add(row);
        }

        public void SetKey(string text) => key.text = text;

        public void Show(bool shown, string label = null)
        {
            row.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            if (label != null) action.text = label;
        }
    }
}
