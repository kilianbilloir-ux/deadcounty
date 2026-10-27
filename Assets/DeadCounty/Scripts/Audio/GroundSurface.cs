using System;
using System.Collections.Generic;
using DeadCounty.Core;
using UnityEngine;

namespace DeadCounty.Audio
{
    public enum Surface { Grass, Dirt, Gravel, Asphalt, Concrete, Wood, Metal }

    // What is underfoot, read from the material of the collider below: the builder names its materials
    // M_Proto_<palette key>, so the palette key decides the surface.
    public static class GroundSurface
    {
        const float ProbeHeight = 0.5f;
        const float ProbeDepth = 1.5f;
        const int Mask = ~((1 << GameLayers.Player) | (1 << GameLayers.Zombie));
        const string BuilderPrefix = "M_Proto_";
        public const Surface Fallback = Surface.Dirt;

        static readonly Dictionary<Material, Surface> Cache = new();

        public static Surface At(Vector3 position)
        {
            if (!Physics.Raycast(position + Vector3.up * ProbeHeight, Vector3.down, out var hit, ProbeHeight + ProbeDepth, Mask, QueryTriggerInteraction.Ignore))
                return Fallback;
            if (!hit.collider.TryGetComponent<Renderer>(out var renderer)) return Fallback;
            var material = renderer.sharedMaterial;
            if (material == null) return Fallback;
            if (Cache.TryGetValue(material, out var surface)) return surface;
            surface = FromMaterialName(material.name);
            Cache[material] = surface;
            return surface;
        }

        public static Surface FromMaterialName(string materialName)
        {
            var key = materialName.StartsWith(BuilderPrefix, StringComparison.Ordinal) ? materialName.Substring(BuilderPrefix.Length) : materialName;
            var instance = key.IndexOf(" (Instance)", StringComparison.Ordinal);
            if (instance >= 0) key = key.Substring(0, instance);
            switch (key)
            {
                case "StoreFloor": case "Sidewalk": case "Concrete": case "TestPad": case "Headstone": case "Brick":
                case "Countertop": case "Porcelain": case "Wall": case "Interior": case "Obstacle": case "Ramp": case "Stairs":
                    return Surface.Concrete;
                case "Floor": case "Crate": case "Furniture": case "Mattress": case "Linen": case "Trunk":
                    return Surface.Wood;
                case "Gravel": case "Rock":
                    return Surface.Gravel;
                case "Dirt": case "Burlap": case "Tire":
                    return Surface.Dirt;
                case "Hay": case "Wheat": case "WheatDark": case "Corn": case "Bush": case "Pine":
                    return Surface.Grass;
            }
            if (Starts(key, "Road")) return Surface.Asphalt;
            if (Starts(key, "Wood") || Starts(key, "Rug") || Starts(key, "Fabric") || Starts(key, "Blanket")) return Surface.Wood;
            if (Starts(key, "Metal") || Starts(key, "Car") || Starts(key, "Bus") || Starts(key, "Tractor") || Starts(key, "Roof")) return Surface.Metal;
            if (Starts(key, "Grass") || Starts(key, "Foliage") || Starts(key, "Flower")) return Surface.Grass;
            return Fallback;
        }

        static bool Starts(string key, string prefix) => key.StartsWith(prefix, StringComparison.Ordinal);

        // Domain reload is disabled on entering Play mode: drop materials of the previous session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Cache.Clear();
    }
}
