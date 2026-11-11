using System;
using DeadCounty.Animation;
using UnityEditor;
using UnityEngine;

namespace DeadCounty.EditorTools
{
    public static partial class TestSceneBuilder
    {
        const string DataDir = "Assets/DeadCounty/Data";
        const string SurvivorGaitPath = DataDir + "/GP_Survivor.asset";
        const string ShamblerGaitPath = DataDir + "/GP_Shambler.asset";

        // Generated only when missing, so Inspector tuning survives scene rebuilds. Delete one to regenerate it.
        static void EnsureGaitProfiles()
        {
            SurvivorGait();
            ShamblerGait();
        }

        static GaitProfile SurvivorGait() => LoadOrCreate(SurvivorGaitPath, ScriptableObject.CreateInstance<GaitProfile>);

        static GaitProfile ShamblerGait() => LoadOrCreate(ShamblerGaitPath, GaitProfile.CreateShambler);

        static T LoadOrCreate<T>(string path, Func<T> create) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            if (!AssetDatabase.IsValidFolder(DataDir)) AssetDatabase.CreateFolder("Assets/DeadCounty", "Data");
            asset = create();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
