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
                    // A long thin item (a bat, a crowbar) covers little of the square: what counts is that it
                    // spans it.
                    var covered = 0;
                    var brightness = 0f;
                    int left = picture.width, right = -1, bottom = picture.height, top = -1;
                    var pixels = picture.GetPixels32();
                    for (var i = 0; i < pixels.Length; i++)
                    {
                        var pixel = pixels[i];
                        if (pixel.a < 128) continue;
                        covered++;
                        brightness += (pixel.r + pixel.g + pixel.b) / 3f;
                        var x = i % picture.width;
                        var y = i / picture.width;
                        left = Mathf.Min(left, x);
                        right = Mathf.Max(right, x);
                        bottom = Mathf.Min(bottom, y);
                        top = Mathf.Max(top, y);
                    }
                    Assert.That(covered, Is.GreaterThan(picture.width * picture.height / 50), $"{item.name}: the model is barely visible");
                    Assert.That(Mathf.Max(right - left, top - bottom), Is.GreaterThan(picture.width * 0.6f), $"{item.name}: the model must fill the picture");
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
