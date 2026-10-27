using System;
using System.Collections.Generic;
using DeadCounty.AI;
using DeadCounty.Core;
using DeadCounty.Items;
using DeadCounty.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeadCounty.Player
{
    // Tips shown once each, the first time they apply (GDD 8.11): the first furniture to search, the first need
    // gauge, the first zombie that notices the player and the first nightfall. Seen tips are kept in PlayerPrefs
    // and the options turn them off. One at a time at the top left, held back while a menu is open, the player
    // sleeps or is dead.
    [RequireComponent(typeof(UIDocument))]
    public class PlayerTips : MonoBehaviour
    {
        const float ShowTime = 9f;
        const float FadeSpeed = 3f;
        // Matches the HUD, which shows a need gauge under half.
        const float NeedShownBelow = 0.5f;
        const float ZombieNoticeRange = 25f;
        const string SeenPrefix = "dc.tip.";
        const string Search = "search", Need = "need", Zombie = "zombie", Night = "night";

        readonly Queue<string> pending = new();
        // Tips already queued or seen, so PlayerPrefs is read once per tip.
        readonly HashSet<string> done = new();
        PlayerHealth health;
        PlayerNeeds needs;
        PlayerInteractor interactor;
        PlayerSleep sleep;
        ThirdPersonController controller;
        DayNightDirector director;
        VisualElement panel;
        Label body;
        float remaining, opacity;
        bool zombieNoticed;

        // Id of the tip on screen, null when none.
        public string Showing { get; private set; }

        void Awake()
        {
            health = Require<PlayerHealth>();
            needs = Require<PlayerNeeds>();
            interactor = Require<PlayerInteractor>();
            sleep = Require<PlayerSleep>();
            controller = Require<ThirdPersonController>();
            director = FindAnyObjectByType<DayNightDirector>();
        }

        T Require<T>() where T : Component
        {
            var component = GetComponentInChildren<T>(true);
            if (component == null) throw new InvalidOperationException($"PlayerTips on '{name}' needs a {typeof(T).Name} in its children");
            return component;
        }

        // Not in Awake: PlayerHudController loads the layout into the document in its own Awake.
        void Start()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            panel = root.Q<VisualElement>("tip") ?? throw new InvalidOperationException($"PlayerTips on '{name}': the HUD has no 'tip'");
            body = root.Q<Label>("tip-text") ?? throw new InvalidOperationException($"PlayerTips on '{name}': the HUD has no 'tip-text' Label");
            panel.style.display = DisplayStyle.None;
        }

        void OnEnable() => AIEvents.Transition += OnZombieTransition;

        void OnDisable() => AIEvents.Transition -= OnZombieTransition;

        void OnZombieTransition(ZombieTransitionEvent e)
        {
            if (e.To is not (ZombieState.Alert or ZombieState.Chase)) return;
            var offset = e.Position - health.transform.position;
            if (offset.sqrMagnitude <= ZombieNoticeRange * ZombieNoticeRange) zombieNoticed = true;
        }

        // Game time, which the engine caps per frame so a hitch cannot eat a tip. A menu or sleep hides the tip
        // at once and holds its countdown.
        void Update()
        {
            if (GameSettings.Tips)
            {
                var model = needs.Model;
                Check(Search, interactor.Current is LootContainer);
                Check(Need, Mathf.Min(model.Hunger, Mathf.Min(model.Thirst, model.Fatigue)) < NeedShownBelow);
                Check(Zombie, zombieNoticed);
                Check(Night, director != null && (director.Hour >= DayNightModel.DuskStart || director.Hour < DayNightModel.DawnStart));
            }
            else
            {
                pending.Clear();
                Hide();
            }

            var free = health.IsAlive && !controller.MenuOpen && !sleep.Asleep;
            if (!free) opacity = 0f;
            var dt = Time.deltaTime;
            // The next tip waits for the previous one to fade out.
            if (free && Showing == null && opacity <= 0f && pending.Count > 0) Show(pending.Dequeue());
            if (free && Showing != null)
            {
                remaining -= dt;
                if (remaining <= 0f) Hide();
            }
            opacity = Mathf.MoveTowards(opacity, free && Showing != null ? 1f : 0f, FadeSpeed * dt);
            panel.style.display = opacity > 0f ? DisplayStyle.Flex : DisplayStyle.None;
            panel.style.opacity = opacity;
        }

        void Check(string id, bool applies)
        {
            if (!applies || done.Contains(id)) return;
            done.Add(id);
            if (PlayerPrefs.GetInt(SeenPrefix + id, 0) == 0) pending.Enqueue(id);
        }

        void Show(string id)
        {
            Showing = id;
            remaining = ShowTime;
            body.text = Text(id);
            PlayerPrefs.SetInt(SeenPrefix + id, 1);
        }

        void Hide()
        {
            Showing = null;
            remaining = 0f;
        }

        // Written when shown, so the keys are those of the device in use.
        static string Text(string id) => id switch
        {
            Search => $"Appuyez sur {Key("Player/Interact")} pour fouiller, sans bouger. En maintenant {Key("Player/Run")}, la fouille va plus vite mais fait du bruit.",
            Need => $"Une jauge apparaît quand un besoin baisse. Mangez et buvez depuis l'inventaire ({Key("Player/Inventory")}).",
            Zombie => $"Un zombie vous a remarqué. En marchant, vous allez plus vite que lui : éloignez-vous et sortez de sa vue. Accroupi ({Key("Player/Crouch")}), vous êtes plus discret.",
            Night => $"La nuit tombe. Les zombies voient moins loin, mais une lampe allumée ({Key("Player/Flashlight")}) se voit de très loin.",
            _ => throw new ArgumentOutOfRangeException(nameof(id), id, "unknown tip"),
        };

        static string Key(string action) => $"<b><color=#D4B976>{InputPrompts.Key(action)}</color></b>";
    }
}
