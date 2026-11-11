using DeadCounty.Player;
using NUnit.Framework;

namespace DeadCounty.Tests
{
    // The line under VOUS ÊTES MORT: a zombie's blow wins, otherwise every drain running at the time is named.
    public class CauseOfDeathTests
    {
        [TestCase(true, true, true, true, "Tué par un zombie")]
        [TestCase(false, true, false, false, "Mort d'une hémorragie")]
        [TestCase(false, false, true, false, "Mort de faim")]
        [TestCase(false, false, true, true, "Mort de faim et de soif")]
        [TestCase(false, true, true, true, "Mort d'une hémorragie, de faim et de soif")]
        [TestCase(false, false, false, false, "")]
        public void NamesWhatDrainedTheLastHitPoints(bool struck, bool bleeding, bool starving, bool dehydrated, string expected) =>
            Assert.That(PlayerHudController.CauseOfDeath(struck, bleeding, starving, dehydrated), Is.EqualTo(expected));
    }
}
