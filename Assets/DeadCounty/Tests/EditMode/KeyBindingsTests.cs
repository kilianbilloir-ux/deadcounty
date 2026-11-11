using DeadCounty.Player;
using NUnit.Framework;

namespace DeadCounty.Tests
{
    public class KeyBindingsTests
    {
        // A renamed action or composite part would only throw once the player opens the controls options.
        [Test]
        public void EveryRowFindsItsKeyboardBinding()
        {
            foreach (var row in KeyBindings.Rows)
                Assert.That(KeyBindings.Key(row), Is.Not.Empty, row.Label);
        }
    }
}
