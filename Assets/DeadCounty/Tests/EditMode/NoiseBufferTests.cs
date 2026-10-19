using DeadCounty.Perception;
using NUnit.Framework;
using UnityEngine;

namespace DeadCounty.Tests
{
    public class NoiseBufferTests
    {
        static void EmitMany(NoiseBuffer buffer, int count)
        {
            for (var i = 0; i < count; i++) buffer.Emit(new Vector3(i, 0f, 0f), 3f, NoiseKind.Footstep, i, 7);
        }

        [TearDown]
        public void TearDown() => NoiseSystem.ResetStatics();

        [Test]
        public void CopySinceReturnsNewerEventsOldestFirst()
        {
            var buffer = new NoiseBuffer();
            Assert.That(buffer.Emit(Vector3.zero, 3f, NoiseKind.Footstep, 1f, 7), Is.EqualTo(1));
            Assert.That(buffer.Emit(Vector3.one, 6f, NoiseKind.Landing, 2f, 7), Is.EqualTo(2));
            Assert.That(buffer.Emit(Vector3.up, 12f, NoiseKind.Glass, 3f, 9), Is.EqualTo(3));
            Assert.That(buffer.LatestId, Is.EqualTo(3));

            var events = new NoiseEvent[8];
            Assert.That(buffer.CopySince(0, events), Is.EqualTo(3));
            Assert.That(new[] { events[0].Id, events[1].Id, events[2].Id }, Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(events[1].Position, Is.EqualTo(Vector3.one));
            Assert.That(events[1].Radius, Is.EqualTo(6f));
            Assert.That(events[1].Kind, Is.EqualTo(NoiseKind.Landing));
            Assert.That(events[1].Time, Is.EqualTo(2f));
            Assert.That(events[2].SourceId, Is.EqualTo(9));

            Assert.That(buffer.CopySince(2, events), Is.EqualTo(1));
            Assert.That(events[0].Id, Is.EqualTo(3));
            Assert.That(buffer.CopySince(3, events), Is.Zero);
        }

        [Test]
        public void CopySinceStopsAtDestinationLength()
        {
            var buffer = new NoiseBuffer();
            EmitMany(buffer, 5);
            var events = new NoiseEvent[2];
            Assert.That(buffer.CopySince(0, events), Is.EqualTo(2));
            Assert.That(events[0].Id, Is.EqualTo(1));
            Assert.That(events[1].Id, Is.EqualTo(2));
            Assert.That(buffer.CopySince(events[1].Id, events), Is.EqualTo(2));
            Assert.That(events[0].Id, Is.EqualTo(3));
        }

        [Test]
        public void WraparoundKeepsTheLatestEventsInOrder()
        {
            var buffer = new NoiseBuffer(64);
            EmitMany(buffer, 100);
            var events = new NoiseEvent[128];
            Assert.That(buffer.CopySince(0, events), Is.EqualTo(64));
            for (var i = 0; i < 64; i++)
            {
                Assert.That(events[i].Id, Is.EqualTo(37 + i));
                Assert.That(events[i].Position.x, Is.EqualTo(36 + i));
            }
            Assert.That(buffer.CopySince(90, events), Is.EqualTo(10));
            Assert.That(events[0].Id, Is.EqualTo(91));
        }

        [Test]
        public void ClearDropsEventsButKeepsIdsIncreasing()
        {
            var buffer = new NoiseBuffer();
            EmitMany(buffer, 3);
            buffer.Clear();
            var events = new NoiseEvent[8];
            Assert.That(buffer.CopySince(0, events), Is.Zero);
            Assert.That(buffer.Emit(Vector3.zero, 3f, NoiseKind.Door, 0f, 1), Is.EqualTo(4));
            Assert.That(buffer.CopySince(0, events), Is.EqualTo(1));
            Assert.That(events[0].Kind, Is.EqualTo(NoiseKind.Door));
        }

        [Test]
        public void NoiseSystemResetClearsTheBuffer()
        {
            var id = NoiseSystem.Emit(Vector3.zero, 8f, NoiseKind.Debug, 1);
            var events = new NoiseEvent[8];
            Assert.That(NoiseSystem.Buffer.CopySince(id - 1, events), Is.EqualTo(1));
            NoiseSystem.ResetStatics();
            Assert.That(NoiseSystem.Buffer.CopySince(0, events), Is.Zero);
        }
    }
}
