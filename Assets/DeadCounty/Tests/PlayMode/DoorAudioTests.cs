using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DeadCounty.Audio;
using DeadCounty.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DeadCounty.Tests
{
    // A code-built door (hinge at (-0.5, 0, 0), opening along X) with one cue per DoorAudio slot.
    public class DoorAudioTests
    {
        static readonly string[] Slots = { "latch", "creak", "shut", "slam", "burst" };

        Door door;
        Transform user;
        readonly Dictionary<string, SoundCue> cues = new();
        readonly List<string> played = new();

        [UnitySetUp]
        public IEnumerator SetUpDoor()
        {
            door = TestDoor.Create(new Vector3(-0.5f, 0f, 0f), 90f);
            var audio = door.gameObject.AddComponent<DoorAudio>();
            foreach (var slot in Slots)
            {
                var cue = ScriptableObject.CreateInstance<SoundCue>();
                cue.name = slot;
                cue.clips = new[] { AudioClip.Create(slot, 3200, 1, 32000, false) };
                typeof(DoorAudio).GetField(slot, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(audio, cue);
                cues[slot] = cue;
            }
            played.Clear();
            audio.Played += (cue, _) => played.Add(cue.name);
            user = new GameObject("TestUser").transform;
            user.position = new Vector3(0f, 0f, -1f);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDownDoor()
        {
            Object.Destroy(door.gameObject);
            Object.Destroy(user.gameObject);
            foreach (var cue in cues.Values)
            {
                Object.Destroy(cue.clips[0]);
                Object.Destroy(cue);
            }
            cues.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator OpeningUnlatchesAndCreaksClosingCreaksAndShuts()
        {
            door.Interact(user, false);
            yield return new WaitForSeconds(0.7f);
            Assert.That(played, Is.EqualTo(new[] { "latch", "creak" }));

            played.Clear();
            door.Interact(user, false);
            yield return new WaitForSeconds(0.7f);
            Assert.That(played, Is.EqualTo(new[] { "creak", "shut" }));
        }

        [UnityTest]
        public IEnumerator AHurriedPlayerSlamsTheDoorShut()
        {
            door.Interact(user, false);
            yield return new WaitForSeconds(0.7f);
            played.Clear();
            door.Interact(user, true);
            yield return new WaitForSeconds(0.3f);
            Assert.That(played, Is.EqualTo(new[] { "slam" }));
        }

        [UnityTest]
        public IEnumerator AForcedDoorBurstsOpenAndBangsIntoItsStop()
        {
            var pusher = new Vector3(0f, 0f, 1f);
            for (var t = 0f; t < 5.5f && door.IsShut; t += Time.deltaTime)
            {
                door.Push(pusher, Time.deltaTime);
                yield return null;
            }
            yield return new WaitForSeconds(0.5f);
            Assert.That(played, Is.EqualTo(new[] { "burst", "slam" }));
        }

        [UnityTest]
        public IEnumerator AStillDoorIsSilent()
        {
            yield return new WaitForSeconds(0.3f);
            Assert.That(played, Is.Empty);
        }
    }
}
