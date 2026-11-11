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
        public abstract string Action { get; }
        // Where the prompt is drawn and the reach is measured.
        public abstract Vector3 PromptPoint { get; }

        // hurried: the player was sprinting.
        public abstract void Interact(Vector3 userPosition, bool hurried);

        protected virtual void OnEnable() => all.Add(this);

        protected virtual void OnDisable() => all.Remove(this);

        // Domain reload is disabled on entering Play mode: objects of the previous session must be dropped by hand.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => all.Clear();
    }
}
