using DeadCounty.Audio;
using NUnit.Framework;

namespace DeadCounty.Tests
{
    // Builder materials are named M_Proto_<palette key> (TestSceneBuilder.Materials).
    public class GroundSurfaceTests
    {
        [TestCase("M_Proto_Grass", Surface.Grass)]
        [TestCase("M_Proto_GrassTall", Surface.Grass)]
        [TestCase("M_Proto_Wheat", Surface.Grass)]
        [TestCase("M_Proto_Dirt", Surface.Dirt)]
        [TestCase("M_Proto_Gravel", Surface.Gravel)]
        [TestCase("M_Proto_Road", Surface.Asphalt)]
        [TestCase("M_Proto_RoadLineYellow", Surface.Asphalt)]
        [TestCase("M_Proto_RoadPatch", Surface.Asphalt)]
        [TestCase("M_Proto_Sidewalk", Surface.Concrete)]
        [TestCase("M_Proto_Concrete", Surface.Concrete)]
        [TestCase("M_Proto_StoreFloor", Surface.Concrete)]
        [TestCase("M_Proto_TestPad", Surface.Concrete)]
        [TestCase("M_Proto_Floor", Surface.Wood)]
        [TestCase("M_Proto_Wood", Surface.Wood)]
        [TestCase("M_Proto_RugBlue", Surface.Wood)]
        [TestCase("M_Proto_Metal", Surface.Metal)]
        [TestCase("M_Proto_MetalLight", Surface.Metal)]
        [TestCase("M_Proto_CarRed", Surface.Metal)]
        [TestCase("M_Proto_Concrete (Instance)", Surface.Concrete)]
        [TestCase("Gravel", Surface.Gravel)]
        public void PaletteKeysMapToTheirSurface(string material, Surface expected) =>
            Assert.That(GroundSurface.FromMaterialName(material), Is.EqualTo(expected));

        [TestCase("Default-Material")]
        [TestCase("Lit")]
        [TestCase("M_Proto_Glass")]
        public void UnknownMaterialsFallBack(string material) =>
            Assert.That(GroundSurface.FromMaterialName(material), Is.EqualTo(GroundSurface.Fallback));
    }
}
