using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DeadCounty.Items;
using DeadCounty.Player;
using DeadCounty.UI;
using NUnit.Framework;
using UnityEditor;

namespace DeadCounty.Tests
{
    // The English table against everything the game can show: a text left out would show in French to an English
    // player, and nothing but these tests would say so before then.
    public class LocTests
    {
        const string Scripts = "Assets/DeadCounty/Scripts";
        const string Layout = "Assets/DeadCounty/UI/Hud.uxml";
        const string Builder = "Assets/DeadCounty/Editor";
        // Written over by the code before they are ever seen.
        static readonly string[] Placeholders = { "Jour 1 — 11:00", "0,0 / 6 kg", "Temps survécu : 00:00" };

        Language saved;

        [SetUp]
        public void Remember() => saved = GameSettings.Language;

        [TearDown]
        public void Restore() => GameSettings.Language = saved;

        [Test]
        public void EveryTextTheCodeTranslatesIsInTheTable()
        {
            var missing = new List<string>();
            foreach (var file in Directory.GetFiles(Scripts, "*.cs", SearchOption.AllDirectories))
            foreach (var text in FirstArguments(File.ReadAllText(file)))
                if (!Loc.EnglishTexts.ContainsKey(text)) missing.Add($"{Path.GetFileName(file)}: {text}");
            Assert.That(missing, Is.Empty);
        }

        [Test]
        public void EveryTextOfTheLayoutIsInTheTable()
        {
            var texts = Regex.Matches(File.ReadAllText(Layout), "\\btext=\"([^\"]+)\"").Select(m => System.Net.WebUtility.HtmlDecode(m.Groups[1].Value)).ToList();
            Assert.That(texts.Count, Is.GreaterThan(50), "the layout was not read");
            Assert.That(texts.Where(t => !Loc.EnglishTexts.ContainsKey(t) && !Placeholders.Contains(t)), Is.Empty);
        }

        [Test]
        public void ItemsContainersAndControlsAreInTheTable()
        {
            var texts = KeyBindings.Rows.Select(row => row.Label).ToList();
            var items = AssetDatabase.FindAssets("t:ItemDefinition").Select(guid => AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid))).ToList();
            Assert.That(items.Count, Is.GreaterThan(10), "the items were not found");
            foreach (var item in items)
            {
                texts.Add(item.DisplayName);
                if (item.Usable) texts.Add(item.UseVerb);
            }
            // The names the builder gives to the furniture that can be searched.
            var containers = Directory.GetFiles(Builder, "*.cs").SelectMany(file => Regex.Matches(File.ReadAllText(file), "Searchable\\([^;\"]*\"([^\"]+)\"").Select(m => m.Groups[1].Value)).Distinct().ToList();
            Assert.That(containers.Count, Is.GreaterThan(5), "the containers were not found");
            texts.AddRange(containers);
            Assert.That(texts.Where(t => !Loc.EnglishTexts.ContainsKey(t)), Is.Empty);
        }

        [Test]
        public void ATranslationFillsInTheSameValues()
        {
            foreach (var (french, english) in Loc.EnglishTexts)
            {
                Assert.That(english, Is.Not.Empty, french);
                Assert.That(Holes(english), Is.EquivalentTo(Holes(french)), french);
            }
        }

        [Test]
        public void TextsNumbersAndKeysFollowTheLanguage()
        {
            GameSettings.Language = Language.French;
            Assert.That(Loc.T("Fouiller"), Is.EqualTo("Fouiller"));
            Assert.That(Loc.T("{0} (fouillé)", "Placards"), Is.EqualTo("Placards (fouillé)"));
            Assert.That(Loc.Percent(35), Is.EqualTo("35\u00A0%"));
            Assert.That(1.5f.ToString("0.0", Loc.Numbers), Is.EqualTo("1,5"));
            Assert.That(InputPrompts.CancelKey, Is.EqualTo("Échap"));
            Assert.That(PlayerHudController.CauseOfDeath(false, true, true, true), Is.EqualTo("Mort d'une hémorragie, de faim et de soif"));

            GameSettings.Language = Language.English;
            Assert.That(Loc.T("Fouiller"), Is.EqualTo("Search"));
            Assert.That(Loc.T("{0} (fouillé)", Loc.T("Placards")), Is.EqualTo("Cupboards (searched)"));
            Assert.That(Loc.Percent(35), Is.EqualTo("35%"));
            Assert.That(1.5f.ToString("0.0", Loc.Numbers), Is.EqualTo("1.5"));
            Assert.That(InputPrompts.CancelKey, Is.EqualTo("Esc"));
            Assert.That(PlayerHudController.CauseOfDeath(true, false, false, false), Is.EqualTo("Killed by a zombie"));
            Assert.That(PlayerHudController.CauseOfDeath(false, false, true, false), Is.EqualTo("Died of hunger"));
            Assert.That(PlayerHudController.CauseOfDeath(false, true, true, true), Is.EqualTo("Died of blood loss, hunger and thirst"));
            Assert.That(ItemDefinition.CategoryName(ItemCategory.Weapon), Is.EqualTo("Weapon"));
        }

        [Test]
        public void ChangingTheLanguageTellsWhichOneWasShown()
        {
            GameSettings.Language = Language.French;
            var told = new List<Language>();
            void Listen(Language before) => told.Add(before);
            GameSettings.LanguageChanged += Listen;
            try
            {
                GameSettings.Language = Language.English;
                GameSettings.Language = Language.English;
                GameSettings.Language = Language.French;
            }
            finally
            {
                GameSettings.LanguageChanged -= Listen;
            }
            Assert.That(told, Is.EqualTo(new[] { Language.French, Language.English }));
        }

        static IEnumerable<string> Holes(string text) => Regex.Matches(text, "\\{\\d+\\}").Select(m => m.Value).Distinct();

        // The string literals written in the first argument of each Loc.T(...) call, a ?: or a switch included;
        // those of a call nested in it are that call's own.
        static IEnumerable<string> FirstArguments(string source)
        {
            const string call = "Loc.T(";
            for (var at = source.IndexOf(call, System.StringComparison.Ordinal); at >= 0; at = source.IndexOf(call, at + 1, System.StringComparison.Ordinal))
            {
                var depth = 1;
                for (var i = at + call.Length; i < source.Length && depth > 0; i++)
                {
                    var c = source[i];
                    if (c == '(') depth++;
                    else if (c == ')') depth--;
                    else if (c == ',' && depth == 1) break;
                    else if (c == '"')
                    {
                        var end = i + 1;
                        while (source[end] != '"') end += source[end] == '\\' ? 2 : 1;
                        if (depth == 1) yield return Regex.Unescape(source.Substring(i + 1, end - i - 1));
                        i = end;
                    }
                }
            }
        }
    }
}
