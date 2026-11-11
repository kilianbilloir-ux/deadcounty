using DeadCounty.Items;
using DeadCounty.Player;
using UnityEngine;

namespace DeadCounty.Audio
{
    // Using, dropping and picking items up are body sounds, played by PlayerActionAudio. A cue left empty is simply
    // not heard.
    [RequireComponent(typeof(GameMenu), typeof(PlayerInventoryScreen))]
    public class UiSounds : MonoBehaviour
    {
        [SerializeField] SoundCue move;
        [SerializeField] SoundCue confirm;
        [SerializeField] SoundCue back;
        [SerializeField] SoundCue step;
        [SerializeField] SoundCue toggle;
        [SerializeField] SoundCue menuOpen;
        [SerializeField] SoundCue menuClose;
        [SerializeField] SoundCue bagOpen;
        [SerializeField] SoundCue bagClose;

        GameMenu menu;
        PlayerInventoryScreen inventoryScreen;
        PlayerInventory inventory;
        ItemDefinition shownItem;
        PlayerInventoryScreen.SortMode shownSort;
        bool shownOpen, itemsChanged;

        void Awake()
        {
            menu = GetComponent<GameMenu>();
            inventoryScreen = GetComponent<PlayerInventoryScreen>();
            inventory = GetComponentInChildren<PlayerInventory>(true);
        }

        void OnEnable() => menu.Feedback += OnFeedback;

        void OnDisable() => menu.Feedback -= OnFeedback;

        // The inventory is created in the player's Awake, which may come after this one.
        void Start()
        {
            if (inventory != null) inventory.Items.Changed += OnItemsChanged;
            shownSort = inventoryScreen.Sort;
        }

        void OnDestroy()
        {
            if (inventory != null && inventory.Items != null) inventory.Items.Changed -= OnItemsChanged;
        }

        void OnItemsChanged() => itemsChanged = true;

        void OnFeedback(GameMenu.MenuFeedback feedback) => Sfx.Play2D(feedback switch
        {
            GameMenu.MenuFeedback.Move => move,
            GameMenu.MenuFeedback.Confirm => confirm,
            GameMenu.MenuFeedback.Back => back,
            GameMenu.MenuFeedback.Step => step,
            GameMenu.MenuFeedback.Toggle => toggle,
            GameMenu.MenuFeedback.Open => menuOpen,
            _ => menuClose,
        });

        // After the inventory screen's Update, which rebuilds its list: compare what it shows with last frame.
        void LateUpdate()
        {
            var open = inventoryScreen.IsOpen;
            var item = inventoryScreen.Selected;
            if (open != shownOpen) Sfx.Play2D(open ? bagOpen : bagClose);
            // A used or dropped item moves the selection by itself: that is the item's sound, not a step.
            else if (open && item != shownItem && item != null && !itemsChanged) Sfx.Play2D(move);
            if (open && inventoryScreen.Sort != shownSort) Sfx.Play2D(step);
            shownOpen = open;
            shownItem = item;
            shownSort = inventoryScreen.Sort;
            itemsChanged = false;
        }
    }
}
