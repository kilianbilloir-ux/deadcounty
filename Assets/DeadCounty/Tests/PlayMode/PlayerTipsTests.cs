#if UNITY_EDITOR
using System.Collections;
using DeadCounty.AI;
using DeadCounty.Player;
using DeadCounty.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DeadCounty.Tests
{
    // One-time tips of the generated rig. The tips the player has already seen and the option are put back after
    // each test, since PlayerPrefs is shared with the editor's own play sessions.
    public class PlayerTipsTests : PlayerRigTests
    {
        static readonly string[] SeenKeys = { "dc.tip.search", "dc.tip.need", "dc.tip.zombie", "dc.tip.night" };

        readonly int[] savedSeen = new int[SeenKeys.Length];
        bool savedTips;
        PlayerTips tips;
        PlayerNeeds needs;

        Label Text => Rig.GetComponent<UIDocument>().rootVisualElement.Q<Label>("tip-text");

        [UnitySetUp]
        public IEnumerator SetUpTips()
        {
            for (var i = 0; i < SeenKeys.Length; i++)
            {
                savedSeen[i] = PlayerPrefs.GetInt(SeenKeys[i], -1);
                PlayerPrefs.DeleteKey(SeenKeys[i]);
            }
            savedTips = GameSettings.Tips;
            GameSettings.Tips = true;
            tips = Rig.GetComponent<PlayerTips>();
            needs = Rig.GetComponentInChildren<PlayerNeeds>();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDownTips()
        {
            for (var i = 0; i < SeenKeys.Length; i++)
            {
                if (savedSeen[i] < 0) PlayerPrefs.DeleteKey(SeenKeys[i]);
                else PlayerPrefs.SetInt(SeenKeys[i], savedSeen[i]);
            }
            GameSettings.Tips = savedTips;
            yield return null;
        }

        // Thirst 1/12 and hunger 0.45: two gauges show.
        void DrainNeeds() => needs.Model.Tick(11f * 60f, false);

        [UnityTest]
        public IEnumerator FirstNeedGaugeShowsItsTipAndRemembersIt()
        {
            Assert.That(tips.Showing, Is.Null);
            DrainNeeds();
            yield return null;
            Assert.That(tips.Showing, Is.EqualTo("need"));
            Assert.That(Text.text, Does.Contain("inventaire"));
            Assert.That(PlayerPrefs.GetInt("dc.tip.need", 0), Is.EqualTo(1), "a shown tip must be remembered");
        }

        [UnityTest]
        public IEnumerator SeenTipsAndDisabledTipsStayHidden()
        {
            PlayerPrefs.SetInt("dc.tip.need", 1);
            DrainNeeds();
            yield return null;
            Assert.That(tips.Showing, Is.Null, "a tip seen in an earlier session came back");

            GameSettings.Tips = false;
            AIEvents.RaiseTransition(1, Chase(), Health.transform.position + Vector3.forward * 5f);
            yield return null;
            Assert.That(tips.Showing, Is.Null, "the option did not turn the tips off");
        }

        [UnityTest]
        public IEnumerator ZombieTipWaitsForTheMenuToClose()
        {
            var controller = Rig.GetComponentInChildren<ThirdPersonController>();
            AIEvents.RaiseTransition(1, Chase(), Health.transform.position + Vector3.forward * 40f);
            yield return null;
            Assert.That(tips.Showing, Is.Null, "a zombie 40 m away does not concern the player");

            controller.MenuOpen = true;
            AIEvents.RaiseTransition(2, Chase(), Health.transform.position + Vector3.forward * 10f);
            yield return null;
            Assert.That(tips.Showing, Is.Null, "a tip must not show over a menu");

            controller.MenuOpen = false;
            yield return null;
            Assert.That(tips.Showing, Is.EqualTo("zombie"));
        }

        static ZombieTransition Chase() => new() { From = ZombieState.Wander, To = ZombieState.Chase, Reason = TransitionReason.SawTarget };
    }
}
#endif
