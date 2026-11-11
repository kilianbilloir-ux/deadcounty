using DeadCounty.Player;
using NUnit.Framework;

namespace DeadCounty.Tests
{
    public class AssemblySetupTests
    {
        [Test]
        public void RuntimeAssemblyIsReachable() => Assert.That(typeof(ThirdPersonController).Assembly.GetName().Name, Is.EqualTo("DeadCounty.Runtime"));
    }
}
