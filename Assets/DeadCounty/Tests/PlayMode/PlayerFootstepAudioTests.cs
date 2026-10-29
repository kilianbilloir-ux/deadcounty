#if UNITY_EDITOR
using System.Collections;
using DeadCounty.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace DeadCounty.Tests
{
    // Footsteps of the generated rig on the bare test floor, whose default material falls back to dirt.
    public class PlayerFootstepAudioTests : PlayerRigTests
    {
        PlayerFootstepAudio footsteps;

        [UnitySetUp]
        public IEnumerator SetUpFootsteps()
        {
            footsteps = Rig.GetComponentInChildren<PlayerFootstepAudio>();
            Assert.That(footsteps, Is.Not.Null, "rebuild the test scene: the rig has no PlayerFootstepAudio");
            yield return new WaitForSeconds(0.3f);
        }

        [UnityTest]
        public IEnumerator StandingStillIsSilentWalkingStepsOnEachPlant()
        {
            var before = footsteps.StepCount;
            yield return new WaitForSeconds(1f);
            Assert.That(footsteps.StepCount, Is.EqualTo(before), "the animator cycles while idle, but standing still must not step");

            InputSystem.QueueStateEvent(Keyboard, new KeyboardState(Key.W));
            yield return new WaitForSeconds(2f);
            InputSystem.QueueStateEvent(Keyboard, new KeyboardState());
            var walked = footsteps.StepCount - before;
            // Two seconds of walking at about two steps a second, counting the start.
            Assert.That(walked, Is.InRange(2, 6));
            Assert.That(footsteps.LastSurface, Is.EqualTo(GroundSurface.Fallback));
        }
    }
}
#endif
