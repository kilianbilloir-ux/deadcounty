using DeadCounty.Combat;
using NUnit.Framework;

namespace DeadCounty.Tests
{
    public class MeleeSwingTests
    {
        const float Dt = 1f / 60f;

        static readonly WeaponStats Bat = new() { Damage = 34f, Windup = 0.3f, Recovery = 0.5f, Stagger = 0.9f, Knockback = 0.5f };

        [Test]
        public void TheBlowLandsOnceWhenTheWindupEnds()
        {
            var swing = new MeleeSwing();
            Assert.That(swing.Start(Bat), Is.True);
            var blows = 0;
            var blowFrame = -1;
            var frames = 0;
            for (; swing.Busy && frames < 600; frames++)
            {
                if (!swing.Tick(Dt)) continue;
                blows++;
                blowFrame = frames;
            }
            Assert.That(blows, Is.EqualTo(1));
            Assert.That((blowFrame + 1) * Dt, Is.EqualTo(0.3f).Within(Dt), "the blow lands at the end of the wind-up");
            Assert.That(frames * Dt, Is.EqualTo(0.8f).Within(Dt), "the arm is free after wind-up plus recovery");
        }

        [Test]
        public void NoSecondSwingStartsBeforeTheFirstIsOver()
        {
            var swing = new MeleeSwing();
            swing.Start(Bat);
            swing.Tick(0.4f);
            Assert.That(swing.Phase, Is.EqualTo(SwingPhase.Recovery));
            Assert.That(swing.Start(Bat), Is.False);
            swing.Tick(0.5f);
            Assert.That(swing.Start(Bat), Is.True);
        }

        [Test]
        public void TimePastTheWindupCountsTowardTheRecovery()
        {
            var swing = new MeleeSwing();
            swing.Start(Bat);
            Assert.That(swing.Tick(0.55f), Is.True);
            Assert.That(swing.Progress, Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void ACancelledWindupNeverLands()
        {
            var swing = new MeleeSwing();
            swing.Start(Bat);
            swing.Tick(0.2f);
            swing.Cancel();
            Assert.That(swing.Busy, Is.False);
            Assert.That(swing.Tick(1f), Is.False);
        }

        [Test]
        public void ATiredSwingHurtsHalfAndPushesNoOne()
        {
            var tired = Bat.Tired();
            Assert.That(tired.Damage, Is.EqualTo(17f));
            Assert.That(tired.Stagger, Is.EqualTo(0.45f));
            Assert.That(tired.Knockback, Is.Zero);
            Assert.That(tired.Windup, Is.EqualTo(Bat.Windup));
        }
    }
}
