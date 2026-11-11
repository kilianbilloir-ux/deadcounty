using DeadCounty.Audio;
using NUnit.Framework;
using UnityEngine;

namespace DeadCounty.Tests
{
    public class SoundCueTests
    {
        SoundCue cue;

        [SetUp]
        public void CreateCue() => cue = ScriptableObject.CreateInstance<SoundCue>();

        [TearDown]
        public void DestroyCue()
        {
            if (cue.clips != null)
                foreach (var clip in cue.clips) Object.DestroyImmediate(clip);
            Object.DestroyImmediate(cue);
        }

        static AudioClip[] Clips(int count)
        {
            var clips = new AudioClip[count];
            for (var i = 0; i < count; i++) clips[i] = AudioClip.Create($"Clip{i}", 320, 1, 32000, false);
            return clips;
        }

        [Test]
        public void PickNeverRepeatsAndUsesEveryClip([Values(2, 3, 5)] int count)
        {
            cue.clips = Clips(count);
            Random.InitState(1234);
            var used = new System.Collections.Generic.HashSet<AudioClip>();
            AudioClip previous = null;
            for (var i = 0; i < 300; i++)
            {
                var clip = cue.Pick();
                Assert.That(clip, Is.Not.SameAs(previous), $"pick {i} repeated the previous clip");
                used.Add(clip);
                previous = clip;
            }
            Assert.That(used.Count, Is.EqualTo(count));
        }

        [Test]
        public void ASingleClipIsAlwaysPicked()
        {
            cue.clips = Clips(1);
            Assert.That(cue.Pick(), Is.SameAs(cue.clips[0]));
            Assert.That(cue.Pick(), Is.SameAs(cue.clips[0]));
        }

        [Test]
        public void AnEmptyCuePicksNothing()
        {
            Assert.That(cue.Pick(), Is.Null);
            cue.clips = new AudioClip[0];
            Assert.That(cue.Pick(), Is.Null);
        }

        [Test]
        public void ShrinkingTheClipListAfterAPickStaysInRange()
        {
            cue.clips = Clips(4);
            Random.InitState(7);
            for (var i = 0; i < 20; i++) cue.Pick();
            var kept = new[] { cue.clips[0], cue.clips[1] };
            for (var i = 2; i < 4; i++) Object.DestroyImmediate(cue.clips[i]);
            cue.clips = kept;
            for (var i = 0; i < 20; i++) Assert.That(kept, Does.Contain(cue.Pick()));
        }
    }
}
