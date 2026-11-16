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
    // Shown once each, the first time they apply (GDD 8.11); seen tips are kept in PlayerPrefs.
    [RequireComponent(typeof(UIDocument))]
    public class PlayerTips : MonoBehaviour
    {
        const float ShowTime = 9f;
        const float FadeSpeed = 3f;
        // Matches the HUD, which shows a need gauge under half.
        const float NeedShownBelow = 0.5f;
        const float ZombieNoticeRange = 25f;
        const string SeenPrefix = "dc.tip.";
        const string Search = "search", Need = "need", Zombie = "zombie", Night = "night", Weapon = "weapon", Felled = "felled", Rest = "rest";
        // A zombie felled this close was most likely felled by the player.
        const float FelledNoticeRange = 4f;

        readonly Queue<string> pending = new();
        // Tips already queued or seen, so PlayerPrefs is read once per tip.
        readonly HashSet<string> done = new();
        PlayerHealth health;
        PlayerNeeds needs;
        PlayerInteractor interactor;
        PlayerSleep sleep;
        PlayerInventory inventory;
        PlayerStamina stamina;
        ThirdPersonController controller;
        DayNightDirector director;
        VisualElement panel;
        Label body;
        float remaining, opacity;
        bool zombieNoticed, zombieFelled;

        // Id of the tip on screen, null when none.
        public string Showing { get; private set; }

        void Awake()
        {
            health = Require<PlayerHealth>();
            needs = Require<PlayerNeeds>();
            interactor = Require<PlayerInteractor>();
            sleep = Require<PlayerSleep>();
            inventory = Require<PlayerInventory>();
            stamina = Require<PlayerStamina>();
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

        void OnEnable()
        {
            AIEvents.Transition += OnZombieTransition;
            GameSettings.LanguageChanged += Translate;
        }

        void OnDisable()
        {
            AIEvents.Transition -= OnZombieTransition;
            GameSettings.LanguageChanged -= Translate;
        }

        void Translate(Language _)
        {
            if (Showing != null) body.text = Text(Showing);
        }

        void OnZombieTransition(ZombieTransitionEvent e)
        {
            var distance = (e.Position - health.transform.position).magnitude;
            if (e.To is ZombieState.Alert or ZombieState.Chase && distance <= ZombieNoticeRange) zombieNoticed = true;
            if (e.To == ZombieState.Down && distance <= FelledNoticeRange) zombieFelled = true;
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
                Check(Weapon, inventory.Weapon != null);
                Check(Felled, zombieFelled);
                Check(Rest, stamina.Model.Exhausted);
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
            Search => Loc.T("Appuyez sur {0} pour fouiller, sans bouger. En maintenant {1}, la fouille va plus vite mais fait du bruit.", Key("Player/Interact"), Key("Player/Run")),
            Need => Loc.T("Une jauge apparaît quand un besoin baisse. Mangez et buvez depuis l'inventaire ({0}).", Key("Player/Inventory")),
            Zombie => Loc.T("Un zombie vous a remarqué. En marchant, vous allez plus vite que lui : éloignez-vous et sortez de sa vue. Accroupi ({0}), vous êtes plus discret.", Key("Player/Crouch")),
            Weapon => Loc.T("Vous avez une arme en main : {0} pour frapper. Chaque coup coûte de l'endurance et s'entend. Un zombie qui ne vous a pas remarqué encaisse le double.", Key("Player/Attack")),
            Felled => Loc.T("Un zombie est à terre. {0} sur lui pour frapper au sol : le coup fait le double et le maintient au sol. Sinon, il se relève au bout de quelques secondes.", Key("Player/Attack")),
            Rest => Loc.T("Vous êtes à bout de souffle. L'endurance remonte lentement : asseyez-vous par terre ({0}) ou sur un banc pour récupérer bien plus vite.", Key("Player/Rest")),
            Night => Loc.T("La nuit tombe. Les zombies voient moins loin, mais une lampe allumée ({0}) se voit de très loin.", Key("Player/Flashlight")),
            _ => throw new ArgumentOutOfRangeException(nameof(id), id, "unknown tip"),
        };

        static string Key(string action) => $"<b><color=#D4B976>{InputPrompts.Key(action)}</color></b>";
    }
}
