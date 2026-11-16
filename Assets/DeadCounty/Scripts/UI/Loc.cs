using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeadCounty.UI
{
    public enum Language { French, English }

    // The game is written in French: a text is looked up by its French wording, which is also what shows in French.
    // English is the table of Loc.English.cs. Key names are InputPrompts' business, not this table's.
    public static partial class Loc
    {
        static readonly CultureInfo FrenchNumbers = CultureInfo.GetCultureInfo("fr-FR");
        static readonly HashSet<string> Reported = new();

        public static bool Translated => GameSettings.Language != Language.French;
        // Decimal comma in French, point in English.
        public static CultureInfo Numbers => Translated ? CultureInfo.InvariantCulture : FrenchNumbers;

        public static string T(string french)
        {
            if (!Translated || string.IsNullOrEmpty(french)) return french;
            if (English.TryGetValue(french, out var english)) return english;
            // Shown in French rather than not at all, and said once: LocTests keeps the table complete.
            if (Reported.Add(french)) Debug.LogError($"No English text for \"{french}\"");
            return french;
        }

        public static string T(string french, params object[] args) => string.Format(T(french), args);

        // French sets the sign off with a no-break space.
        public static string Percent(int value) => Translated ? value + "%" : value + " %";

        public static IReadOnlyDictionary<string, string> EnglishTexts => English;

        // The texts written in a layout, as opposed to those the code writes into it.
        public sealed class Layout
        {
            readonly List<(TextElement Element, string French)> labels = new();

            public Layout(VisualElement root)
            {
                root.Query<TextElement>().ForEach(element =>
                {
                    if (English.ContainsKey(element.text)) labels.Add((element, element.text));
                });
                Apply(Language.French);
            }

            // `before` is the language the labels show now: one the code rewrote since is the code's to update.
            public void Apply(Language before)
            {
                foreach (var (element, french) in labels)
                {
                    var shown = before == Language.French ? french : English[french];
                    if (element.text == shown) element.text = T(french);
                }
            }
        }

        // Domain reload is disabled on entering Play mode.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Reported.Clear();
    }
}
