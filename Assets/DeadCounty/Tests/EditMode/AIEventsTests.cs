using DeadCounty.AI;
using NUnit.Framework;
using UnityEngine;

namespace DeadCounty.Tests
{
    public class AIEventsTests
    {
        [TearDown]
        public void TearDown() => AIEvents.ResetStatics();

        [Test]
        public void RaisedEventsReachListenersUntilReset()
        {
            var transitions = 0;
            var strikes = 0;
            ZombieTransitionEvent transition = default;
            ZombieStrikeEvent strike = default;
            AIEvents.Transition += e =>
            {
                transitions++;
                transition = e;
            };
            AIEvents.Strike += e =>
            {
                strikes++;
                strike = e;
            };

            var change = new ZombieTransition { From = ZombieState.Wander, To = ZombieState.Alert, Reason = TransitionReason.HeardNoise, Time = 2f };
            AIEvents.RaiseTransition(3, change, new Vector3(1f, 0f, 2f));
            AIEvents.RaiseStrike(4, 5f, Vector3.one, Vector3.forward, true);
            Assert.That(transitions, Is.EqualTo(1));
            Assert.That(transition.AgentId, Is.EqualTo(3));
            Assert.That(transition.Time, Is.EqualTo(2f));
            Assert.That(transition.From, Is.EqualTo(ZombieState.Wander));
            Assert.That(transition.To, Is.EqualTo(ZombieState.Alert));
            Assert.That(transition.Reason, Is.EqualTo(TransitionReason.HeardNoise));
            Assert.That(transition.Position, Is.EqualTo(new Vector3(1f, 0f, 2f)));
            Assert.That(strikes, Is.EqualTo(1));
            Assert.That(strike.AgentId, Is.EqualTo(4));
            Assert.That(strike.Time, Is.EqualTo(5f));
            Assert.That(strike.Origin, Is.EqualTo(Vector3.one));
            Assert.That(strike.Direction, Is.EqualTo(Vector3.forward));
            Assert.That(strike.Hit);

            AIEvents.ResetStatics();
            AIEvents.RaiseTransition(3, change, Vector3.zero);
            AIEvents.RaiseStrike(4, 6f, Vector3.zero, Vector3.forward, false);
            Assert.That(transitions, Is.EqualTo(1));
            Assert.That(strikes, Is.EqualTo(1));
        }
    }
}
