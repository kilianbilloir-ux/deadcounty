using System.Collections.Generic;
using System.Linq;
using DeadCounty.AI;
using DeadCounty.Audio;
using NUnit.Framework;

namespace DeadCounty.Tests
{
    public class ZombieVoiceTests
    {
        [Test]
        public void VoiceIsStablePerSeedAndVariesAcrossTheScene()
        {
            Assert.That(ZombieVoice.FromSeed(7).Pitch, Is.EqualTo(ZombieVoice.FromSeed(7).Pitch));
            Assert.That(ZombieVoice.FromSeed(7).Variant, Is.EqualTo(ZombieVoice.FromSeed(7).Variant));

            var voices = Enumerable.Range(1, 24).Select(ZombieVoice.FromSeed).ToList();
            foreach (var voice in voices)
            {
                Assert.That(voice.Pitch, Is.InRange(ZombieVoice.MinPitch, ZombieVoice.MaxPitch));
                Assert.That(voice.TelegraphPitch, Is.InRange(0.95f, 1.05f));
            }
            Assert.That(voices.Max(v => v.Pitch) - voices.Min(v => v.Pitch), Is.GreaterThan(0.18f));
            Assert.That(voices.Select(v => v.Variant % 5).Distinct().Count(), Is.GreaterThanOrEqualTo(4));
        }

        [TestCase(5)]
        [TestCase(3)]
        public void PickClipStaysOnTheVoicesTwoVariants(int count)
        {
            var voice = ZombieVoice.FromSeed(11);
            var rng = new System.Random(3);
            var picked = new HashSet<int>();
            for (var i = 0; i < 200; i++) picked.Add(voice.PickClip(count, rng));
            Assert.That(picked, Is.EquivalentTo(new[] { voice.Variant % count, (voice.Variant + 1) % count }));
            Assert.That(voice.PickClip(1, rng), Is.EqualTo(0));
            Assert.That(voice.PickClip(0, rng), Is.EqualTo(-1));
        }

        [Test]
        public void TierOutranksDistanceAndCloserWinsWithinATier()
        {
            Assert.That(ZombieVocals.Score(ZombieVocal.Windup, 29f, 30f), Is.GreaterThan(ZombieVocals.Score(ZombieVocal.Alert, 0f, 30f)));
            Assert.That(ZombieVocals.Score(ZombieVocal.Alert, 29f, 30f), Is.GreaterThan(ZombieVocals.Score(ZombieVocal.Growl, 0f, 30f)));
            Assert.That(ZombieVocals.Score(ZombieVocal.Growl, 29f, 30f), Is.GreaterThan(ZombieVocals.Score(ZombieVocal.Sniff, 0f, 15f)));
            Assert.That(ZombieVocals.Score(ZombieVocal.Sniff, 14f, 15f), Is.GreaterThan(ZombieVocals.Score(ZombieVocal.Moan, 0f, 28f)));
            Assert.That(ZombieVocals.Score(ZombieVocal.Moan, 5f, 28f), Is.GreaterThan(ZombieVocals.Score(ZombieVocal.Moan, 20f, 28f)));
            Assert.That(ZombieVocals.Score(ZombieVocal.Moan, 50f, 28f), Is.EqualTo(ZombieVocals.Score(ZombieVocal.Moan, 28f, 28f)));
        }

        [Test]
        public void UnpromptedVocalsFollowTheState()
        {
            var rng = new System.Random(5);
            for (var i = 0; i < 100; i++)
            {
                Assert.That(ZombieVocals.NextDelay(ZombieState.Idle, rng), Is.InRange(5f, 14f));
                Assert.That(ZombieVocals.NextDelay(ZombieState.Search, rng), Is.InRange(2.5f, 6f));
                Assert.That(ZombieVocals.NextDelay(ZombieState.Chase, rng), Is.InRange(1.8f, 4f));
                Assert.That(ZombieVocals.FirstDelay(rng), Is.InRange(0f, 12f));
            }
            Assert.That(ZombieVocals.NextDelay(ZombieState.Alert, rng), Is.EqualTo(float.PositiveInfinity));
            Assert.That(ZombieVocals.NextDelay(ZombieState.Attack, rng), Is.EqualTo(float.PositiveInfinity));
            Assert.That(ZombieVocals.TryPickAmbient(ZombieState.Attack, rng, out _), Is.False);
            Assert.That(ZombieVocals.TryPickAmbient(ZombieState.Alert, rng, out _), Is.False);

            var calm = new HashSet<ZombieVocal>();
            var hunting = new HashSet<ZombieVocal>();
            for (var i = 0; i < 100; i++)
            {
                Assert.That(ZombieVocals.TryPickAmbient(ZombieState.Wander, rng, out var vocal), Is.True);
                calm.Add(vocal);
                ZombieVocals.TryPickAmbient(ZombieState.Chase, rng, out vocal);
                hunting.Add(vocal);
            }
            Assert.That(calm, Is.EquivalentTo(new[] { ZombieVocal.Moan, ZombieVocal.Breath }));
            Assert.That(hunting, Is.EquivalentTo(new[] { ZombieVocal.Growl }));
        }

        [TestCase(ZombieState.Idle, ZombieState.Wander, true)]
        [TestCase(ZombieState.Investigate, ZombieState.Search, true)]
        [TestCase(ZombieState.Wander, ZombieState.Alert, false)]
        [TestCase(ZombieState.Search, ZombieState.Chase, false)]
        [TestCase(ZombieState.Attack, ZombieState.Chase, false)]
        public void CalmOrSearchingActivitiesShareARhythm(ZombieState from, ZombieState to, bool same)
            => Assert.That(ZombieVocals.SameCadence(from, to), Is.EqualTo(same));

        [Test]
        public void SameSeedReplaysTheSameSchedule()
        {
            var first = new System.Random(42);
            var second = new System.Random(42);
            for (var i = 0; i < 20; i++)
            {
                Assert.That(ZombieVocals.NextDelay(ZombieState.Wander, first), Is.EqualTo(ZombieVocals.NextDelay(ZombieState.Wander, second)));
                ZombieVocals.TryPickAmbient(ZombieState.Search, first, out var a);
                ZombieVocals.TryPickAmbient(ZombieState.Search, second, out var b);
                Assert.That(a, Is.EqualTo(b));
            }
        }

        [TestCase(Surface.Grass, StepKind.Soft)]
        [TestCase(Surface.Dirt, StepKind.Soft)]
        [TestCase(Surface.Gravel, StepKind.Gravel)]
        [TestCase(Surface.Asphalt, StepKind.Hard)]
        [TestCase(Surface.Concrete, StepKind.Hard)]
        [TestCase(Surface.Wood, StepKind.Hard)]
        [TestCase(Surface.Metal, StepKind.Hard)]
        public void SurfacesMapToStepKinds(Surface surface, StepKind kind) => Assert.That(ZombieVocals.StepKindOf(surface), Is.EqualTo(kind));
    }
}
