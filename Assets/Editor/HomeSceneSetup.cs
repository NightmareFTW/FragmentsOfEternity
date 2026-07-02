#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace RPG.EditorTools
{
    // Builds the Home hub scene (gems, summon, collection, battle entry) and
    // registers Boot/Home/Combat in Build Settings so scene loads work at runtime.
    public static class HomeSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/Home.unity";
        private const string PoolPath  = "Assets/ScriptableObjects/GachaPool.asset";

        [MenuItem("RPG/Setup Home Scene", priority = 3)]
        public static void Setup()
        {
            var scene = File.Exists(ScenePath)
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            foreach (var go in scene.GetRootGameObjects())
                Object.DestroyImmediate(go);

            BuildCamera();
            BuildEventSystem();
            var canvas = BuildCanvas();

            BuildBackground(canvas.transform);

            MakeText(canvas.transform, "Title", "Fragments of Eternity",
                new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.96f), 54, FontStyle.Bold,
                new Color(1f, 0.85f, 0.4f));

            var gems = MakeText(canvas.transform, "GemsLabel", "Gems: 0",
                new Vector2(0.05f, 0.815f), new Vector2(0.95f, 0.87f), 40, FontStyle.Bold,
                new Color(0.6f, 0.9f, 1f));

            var result = MakeText(canvas.transform, "ResultLabel", "",
                new Vector2(0.05f, 0.725f), new Vector2(0.95f, 0.79f), 44, FontStyle.Bold, Color.white);

            var team = MakeText(canvas.transform, "TeamLabel", "Team: 0/4",
                new Vector2(0.05f, 0.665f), new Vector2(0.95f, 0.712f), 28, FontStyle.Bold,
                new Color(1f, 0.85f, 0.4f));

            var grid = MakeContainer(canvas.transform, "CollectionGrid",
                new Vector2(0.06f, 0.30f), new Vector2(0.94f, 0.655f));

            var summon = MakeButton(canvas.transform, "SummonButton", "SUMMON (300)",
                new Vector2(0.06f, 0.205f), new Vector2(0.48f, 0.28f),
                new Color(0.45f, 0.30f, 0.65f));

            var summon10 = MakeButton(canvas.transform, "Summon10Button", "SUMMON x10",
                new Vector2(0.52f, 0.205f), new Vector2(0.94f, 0.28f),
                new Color(0.35f, 0.24f, 0.55f));

            var battle = MakeButton(canvas.transform, "BattleButton", "BATTLE",
                new Vector2(0.18f, 0.10f), new Vector2(0.82f, 0.18f),
                new Color(0.20f, 0.42f, 0.60f));

            var reset = MakeButton(canvas.transform, "ResetButton", "Reset",
                new Vector2(0.72f, 0.02f), new Vector2(0.97f, 0.07f),
                new Color(0.30f, 0.16f, 0.18f));

            // ── Wire controller ───────────────────────────────────────────
            var ctrlGO = new GameObject("HomeController");
            var ctrl   = ctrlGO.AddComponent<global::UI.HomeController>();
            var pool   = AssetDatabase.LoadAssetAtPath<Data.GachaPool>(PoolPath);

            var so = new SerializedObject(ctrl);
            if (pool != null) so.FindProperty("_pool").objectReferenceValue = pool;
            so.FindProperty("_gemsLabel").objectReferenceValue      = gems;
            so.FindProperty("_teamLabel").objectReferenceValue      = team;
            so.FindProperty("_resultLabel").objectReferenceValue    = result;
            so.FindProperty("_summonButton").objectReferenceValue   = summon;
            so.FindProperty("_summon10Button").objectReferenceValue = summon10;
            so.FindProperty("_battleButton").objectReferenceValue   = battle;
            so.FindProperty("_resetButton").objectReferenceValue    = reset;
            so.FindProperty("_gridContainer").objectReferenceValue  = grid;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            EnsureBuildScenes();
            PatchBootFirstScene("Home");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);   // leave the user on Home
            Debug.Log(saved
                ? "[RPG] Home scene built; Boot→Home wired; Boot/Home/Combat in Build Settings."
                : "[RPG] ERROR: Home scene save failed.");
        }

        // Point the Boot scene's Bootstrap at Home so a full boot lands on the hub.
        static void PatchBootFirstScene(string sceneName)
        {
            const string bootPath = "Assets/Scenes/Boot.unity";
            if (!File.Exists(bootPath)) return;

            var boot    = EditorSceneManager.OpenScene(bootPath, OpenSceneMode.Single);
            bool changed = false;
            foreach (var go in boot.GetRootGameObjects())
            {
                var bs = go.GetComponentInChildren<global::Core.Bootstrap>(true);
                if (bs == null) continue;
                var so   = new SerializedObject(bs);
                var prop = so.FindProperty("_firstScene");
                if (prop != null && prop.stringValue != sceneName)
                {
                    prop.stringValue = sceneName;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }
                break;
            }
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(boot);
                EditorSceneManager.SaveScene(boot);
            }
        }

        [MenuItem("RPG/Setup Home Scene", validate = true)]
        static bool Validate() => !EditorApplication.isPlaying;

        // ── Scene objects ───────────────────────────────────────────────────

        static void BuildCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 0f, -10f);
            var cam = go.AddComponent<Camera>();
            cam.orthographic    = true;
            cam.clearFlags      = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.04f, 0.10f);
            go.AddComponent<AudioListener>();
        }

        static void BuildEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        static GameObject BuildCanvas()
        {
            var go     = new GameObject("Canvas");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight  = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return go;
        }

        static void BuildBackground(Transform canvas)
        {
            MakeImage(canvas, "Backdrop",   Vector2.zero,               Vector2.one,               new Color(0.05f, 0.04f, 0.10f));
            MakeImage(canvas, "TopBand",    new Vector2(0f, 0.80f),     new Vector2(1f, 1f),       new Color(0.10f, 0.06f, 0.20f, 0.60f));
            MakeImage(canvas, "MidGlow",    new Vector2(0f, 0.58f),     new Vector2(1f, 0.72f),    new Color(0.16f, 0.10f, 0.30f, 0.35f));
            MakeImage(canvas, "BottomBand", new Vector2(0f, 0f),        new Vector2(1f, 0.30f),    new Color(0.03f, 0.03f, 0.08f, 0.70f));
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        static RectTransform MakeContainer(Transform parent, string name, Vector2 aMin, Vector2 aMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        static void MakeImage(Transform parent, string name, Vector2 aMin, Vector2 aMax, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = color; img.raycastTarget = false;
        }

        static Text MakeText(Transform parent, string name, string content,
            Vector2 aMin, Vector2 aMax, int fontSize, FontStyle style, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = rt.offsetMax = Vector2.zero;

            var txt = go.AddComponent<Text>();
            txt.text               = content;
            txt.font               = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize           = fontSize;
            txt.fontStyle          = style;
            txt.alignment          = TextAnchor.MiddleCenter;
            txt.color              = color;
            txt.raycastTarget      = false;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow   = VerticalWrapMode.Overflow;
            return txt;
        }

        static Button MakeButton(Transform parent, string name, string label,
            Vector2 aMin, Vector2 aMax, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = rt.offsetMax = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.color = color;

            var btn    = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor      = color;
            colors.highlightedColor = new Color(color.r + 0.12f, color.g + 0.12f, color.b + 0.12f);
            colors.pressedColor     = new Color(color.r * 0.8f, color.g * 0.8f, color.b * 0.8f);
            colors.disabledColor    = new Color(0.20f, 0.20f, 0.22f, 0.70f);
            colors.fadeDuration     = 0.1f;
            btn.colors = colors;

            MakeText(go.transform, "Label", label, Vector2.zero, Vector2.one, 32, FontStyle.Bold, Color.white);
            return btn;
        }

        static void EnsureBuildScenes()
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            void Ensure(string path)
            {
                if (!File.Exists(path)) return;
                foreach (var s in list) if (s.path == path) return;
                list.Add(new EditorBuildSettingsScene(path, true));
            }
            Ensure("Assets/Scenes/Boot.unity");
            Ensure(ScenePath);
            Ensure("Assets/Scenes/Combat.unity");
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
#endif
