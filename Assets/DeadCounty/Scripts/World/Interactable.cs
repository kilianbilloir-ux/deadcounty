using System.Collections.Generic;
using UnityEngine;

namespace DeadCounty.World
{
    // Something the player can use with the Interact action. Registered while enabled, so PlayerInteractor
    // picks one without physics queries.
    public abstract class Interactable : MonoBehaviour
    {
        static readonly List<Interactable> all = new();

        public static IReadOnlyList<Interactable> All => all;

        // Shown above the key and the action: "Porte", then "[E] Ouvrir".
        public abstract string DisplayName { get; }
        // Null when there is nothing to do (a searched cupboard): the prompt then shows the name alone.
        public abstract string Action { get; }
        // Where the prompt is drawn and the reach is measured.
        public abstract Vector3 PromptPoint { get; }

        // hurried: the player held Run or Sprint.
        public abstract void Interact(Transform user, bool hurried);

        // Seconds the player must stay still before Interact happens, 0 for an instant use.
        public virtual float UseTime(bool hurried) => 0f;

        // A timed use starts; Interact follows unless the player moves, gets hit or walks away.
        public virtual void BeginUse(bool hurried) { }

        protected virtual void OnEnable() => all.Add(this);

        protected virtual void OnDisable() => all.Remove(this);

        // Domain reload is disabled on entering Play mode: objects of the previous session must be dropped by hand.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => all.Clear();
    }
}
