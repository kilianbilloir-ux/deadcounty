using DeadCounty.Audio;
using NUnit.Framework;

namespace DeadCounty.Tests
{
    public class VoiceLimiterTests
    {
        readonly object a = new(), b = new(), c = new();

        [Test]
        public void FreeSlotsAreTakenWithoutEviction()
        {
            var limiter = new VoiceLimiter<object>(2);
            Assert.That(limiter.TryAcquire(a, 0.5f, 0f, 2f, out var evicted), Is.True);
            Assert.That(evicted, Is.Null);
            Assert.That(limiter.TryAcquire(b, 0.1f, 0f, 2f, out evicted), Is.True);
            Assert.That(evicted, Is.Null);
            Assert.That(limiter.ActiveCount(1f), Is.EqualTo(2));
        }

        [Test]
        public void FullLimiterEvictsTheWeakestOnlyForAHigherScore()
        {
            var limiter = new VoiceLimiter<object>(2);
            limiter.TryAcquire(a, 0.5f, 0f, 2f, out _);
            limiter.TryAcquire(b, 1.5f, 0f, 2f, out _);

            Assert.That(limiter.TryAcquire(c, 0.5f, 0.5f, 2f, out var evicted), Is.False, "a tie keeps the voice already speaking");
            Assert.That(evicted, Is.Null);
            Assert.That(limiter.TryAcquire(c, 0.8f, 0.5f, 2f, out evicted), Is.True);
            Assert.That(evicted, Is.SameAs(a));
            Assert.That(limiter.IsSpeaking(a, 0.5f), Is.False);
            Assert.That(limiter.IsSpeaking(b, 0.5f), Is.True);
            Assert.That(limiter.IsSpeaking(c, 0.5f), Is.True);
        }

        [Test]
        public void AnOwnerReplacesItsOwnVoiceOnlyWithAnEqualOrHigherScore()
        {
            var limiter = new VoiceLimiter<object>(1);
            limiter.TryAcquire(a, 2f, 0f, 3f, out _);

            Assert.That(limiter.TryAcquire(a, 1f, 1f, 2f, out _), Is.False);
            Assert.That(limiter.TryAcquire(a, 2f, 1f, 4f, out var evicted), Is.True);
            Assert.That(evicted, Is.Null, "replacing its own voice evicts nobody");
            Assert.That(limiter.IsSpeaking(a, 3.5f), Is.True);
            Assert.That(limiter.ActiveCount(1f), Is.EqualTo(1));
        }

        [Test]
        public void SlotsFreeThemselvesAtTheirEndOrOnRelease()
        {
            var limiter = new VoiceLimiter<object>(1);
            limiter.TryAcquire(a, 4f, 0f, 1f, out _);
            Assert.That(limiter.TryAcquire(b, 0f, 1f, 2f, out var evicted), Is.True, "the voice ended at 1 s");
            Assert.That(evicted, Is.Null);

            limiter.Release(b);
            Assert.That(limiter.ActiveCount(1.5f), Is.EqualTo(0));
            Assert.That(limiter.TryAcquire(c, 0f, 1.5f, 2f, out _), Is.True);

            limiter.Clear();
            Assert.That(limiter.IsSpeaking(c, 1.5f), Is.False);
        }
    }
}
