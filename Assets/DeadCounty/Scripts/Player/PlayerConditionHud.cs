using System;
using DeadCounty.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeadCounty.Player
{
    // Afflictions at the top right, each shown only while active (GDD 8.4, in the spirit of moodles, laid out after
    // The Long Dark), and the screen that hides the world while the player sleeps, with the clock running.
    [RequireComponent(typeof(UIDocument))]
    public class PlayerConditionHud : MonoBehaviour
    {
        const float LowNeed = 0.2f;
        const float SleepFadeSpeed = 2f;
        const float BleedingPulseRate = 3f;

        PlayerHealth health;
        PlayerNeeds needs;
        PlayerInventory inventory;
        PlayerSleep sleep;
        VisualElement sleepScreen;
        Label bleeding, hungry, thirsty, tired, encumbered, clock, sleepTime;
        float sleepOpacity;
        bool tiredShowsExhausted;

        void Awake()
        {
            health = Require<PlayerHealth>();
            needs = Require<PlayerNeeds>();
            inventory = Require<PlayerInventory>();
            sleep = Require<PlayerSleep>();
        }

        T Require<T>() where T : Component
        {
            var component = GetComponentInChildren<T>(true);
            if (component == null) throw new InvalidOperationException($"PlayerConditionHud on '{name}' needs a {typeof(T).Name} in its children");
            return component;
        }

        // Not in Awake: PlayerHudController loads the layout into the document in its own Awake.
        void Start()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            bleeding = Find<Label>(root, "status-bleeding");
            hungry = Find<Label>(root, "status-hungry");
            thirsty = Find<Label>(root, "status-thirsty");
            tired = Find<Label>(root, "status-tired");
            encumbered = Find<Label>(root, "status-encumbered");
            sleepScreen = Find<VisualElement>(root, "sleep");
            sleepTime = Find<Label>(root, "sleep-time");
            clock = Find<Label>(root, "time");
        }

        T Find<T>(VisualElement root, string elementName) where T : VisualElement =>
            root.Q<T>(elementName) ?? throw new InvalidOperationException($"PlayerConditionHud on '{name}': the HUD has no '{elementName}' {typeof(T).Name}");

        void Update()
        {
            var alive = health.IsAlive;
            var model = needs.Model;
            Show(bleeding, alive && health.Bleeding);
            // Bleeding is the one affliction that kills fast: it pulses.
            if (health.Bleeding) bleeding.style.opacity = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * BleedingPulseRate));
            Show(hungry, alive && model.Hunger <= LowNeed);
            Show(thirsty, alive && model.Thirst <= LowNeed);
            Show(tired, alive && model.Fatigue <= LowNeed);
            if (model.Exhausted != tiredShowsExhausted)
            {
                tiredShowsExhausted = model.Exhausted;
                tired.text = tiredShowsExhausted ? "ÉPUISÉ" : "FATIGUÉ";
            }
            Show(encumbered, alive && inventory.Items.Overloaded);

            // Real time: game time runs 20 times faster in bed.
            sleepOpacity = Mathf.MoveTowards(sleepOpacity, sleep.Asleep ? 1f : 0f, SleepFadeSpeed * Time.unscaledDeltaTime);
            sleepScreen.style.display = sleepOpacity > 0f ? DisplayStyle.Flex : DisplayStyle.None;
            sleepScreen.style.opacity = sleepOpacity;
            if (sleep.Asleep) sleepTime.text = clock.text;
        }

        static void Show(VisualElement element, bool shown) => element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
