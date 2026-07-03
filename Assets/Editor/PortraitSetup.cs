#if UNITY_EDITOR
using System.IO;
using UnityEngine;
using UnityEditor;
using Data;

namespace RPG.EditorTools
{
    // Drop hero portrait images into Assets/Art/Portraits named after the hero
    // asset (e.g. Hero.png, Mage.png, GoblinGrunt.png), then run this to import
    // them as sprites and assign them to the matching HeroData.
    public static class PortraitSetup
    {
        private const string PortraitDir = "Assets/Art/Portraits";
        private const string HeroDir     = "Assets/ScriptableObjects/Heroes";

        [MenuItem("RPG/Import Portraits", priority = 5)]
        public static void Import()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Art"))
                AssetDatabase.CreateFolder("Assets", "Art");
            if (!AssetDatabase.IsValidFolder(PortraitDir))
                AssetDatabase.CreateFolder("Assets/Art", "Portraits");

            int assigned = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { PortraitDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                // Force sprite import settings so the texture can be assigned as a Sprite.
                if (AssetImporter.GetAtPath(path) is TextureImporter importer &&
                    importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.SaveAndReimport();
                }

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) continue;

                string name = Path.GetFileNameWithoutExtension(path);
                var hero = AssetDatabase.LoadAssetAtPath<HeroData>($"{HeroDir}/{name}.asset");
                if (hero != null)
                {
                    hero.portrait = sprite;
                    EditorUtility.SetDirty(hero);
                    assigned++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[RPG] Import Portraits: assigned {assigned} portrait(s) from {PortraitDir}. " +
                      "Name files after the hero asset (Hero.png, Mage.png, GoblinGrunt.png) and re-run. " +
                      "Then run RPG → Setup Combat Scene to show them in battle.");
        }

        [MenuItem("RPG/Import Portraits", validate = true)]
        static bool Validate() => !EditorApplication.isPlaying;
    }
}
#endif
