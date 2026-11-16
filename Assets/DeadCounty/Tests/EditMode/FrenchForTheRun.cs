using DeadCounty.UI;
using NUnit.Framework;

namespace DeadCounty.Tests
{
    // The tests read the game's texts in French, whatever the language of the machine or the one last chosen.
    [SetUpFixture]
    public class FrenchForTheRun
    {
        Language saved;

        [OneTimeSetUp]
        public void SetFrench()
        {
            saved = GameSettings.Language;
            GameSettings.Language = Language.French;
        }

        [OneTimeTearDown]
        public void Restore() => GameSettings.Language = saved;
    }
}
