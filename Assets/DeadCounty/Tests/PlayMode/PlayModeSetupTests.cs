using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DeadCounty.Tests
{
    public class PlayModeSetupTests
    {
        [UnityTest]
        public IEnumerator FramesAdvance()
        {
            var start = Time.frameCount;
            yield return null;
            yield return null;
            Assert.That(Time.frameCount, Is.GreaterThan(start));
        }
    }
}
