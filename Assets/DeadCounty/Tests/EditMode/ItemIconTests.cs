using System.IO;
using System.Linq;
using DeadCounty.Items;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DeadCounty.Tests
{
    // Inventory pictures rendered by the scene builder from the items' ground models.
    public class ItemIconTests
    {
        const string ItemsDir = "Assets/DeadCounty/Data/Items";

        [Test]
        public void EveryItemHasALitPictureOnAClearBackground()
        {
            var items = AssetDatabase.FindAssets("t:ItemDefinition", new[] { ItemsDir })
                .Select(guid => AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .ToArray();
            Assert.That(items, Is.Not.Empty);
            foreach (var item in items)
            {
                Assert.That(item.Icon, Is.Not.Null, $"{item.name} has no picture: rebuild the test scene");
                // The imported texture is not readable: decode the file itself.
                var picture = new Texture2D(2, 2);
                try
                {
                    Assert.That(picture.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(item.Icon))));
                    Assert.That(picture.GetPixel(0, 0).a, Is.Zero, $"{item.name}: the background must be clear");
                    var covered = 0;
                    var brightness = 0f;
                    foreach (var pixel in picture.GetPixels32())
                    {
                        if (pixel.a < 128) continue;
                        covered++;
                        brightness += (pixel.r + pixel.g + pixel.b) / 3f;
                    }
                    Assert.That(covered, Is.GreaterThan(picture.width * picture.height / 10), $"{item.name}: the model must fill the picture");
                    Assert.That(brightness / covered, Is.GreaterThan(40f), $"{item.name}: the model must be lit");
                }
                finally
                {
                    Object.DestroyImmediate(picture);
                }
            }
        }
    }
}
