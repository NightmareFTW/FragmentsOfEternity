#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RPG.EditorTools
{
    public static class CombatSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/Combat.unity";

        [MenuItem("RPG/Setup Combat Scene", priority = 1)]
        public static void Setup()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            foreach (var go in scene.GetRootGameObjects())
                Object.DestroyImmediate(go);

            BuildCamera();
            BuildEventSystem();
            BuildCombatController();

            var canvas = BuildCanvas();

            // Unit panels must be added first so they render behind the HUD overlay.
            BuildUnitPanels(canvas.transform,
                out var playerVisual, out var enemyVisual);

            BuildTitle(canvas.transform);

            var hud = BuildHUD(canvas.transform,
                out var turnLabel,
                out var playerHP, out var enemyHP,
                out var s1, out var s2, out var s3);

            WireHUD(hud, turnLabel, playerHP, enemyHP,
                    s1, s2, s3, playerVisual, enemyVisual);

            // Tooltip must be the last canvas child so it renders on top of everything.
            var tooltip = BuildTooltip(canvas.transform);
            WireTooltip(hud, tooltip);

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log(saved
                ? "[RPG] Combat scene built and saved."
                : "[RPG] ERROR: scene save failed.");
        }

        [MenuItem("RPG/Setup Combat Scene", validate = true)]
        static bool ValidateSetup() => !EditorApplication.isPlaying;

        // ── Scene objects ──────────────────────────────────────────────────

        static void BuildCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 0f, -10f);

            var cam = go.AddComponent<Camera>();
            cam.orthographic     = true;
            cam.orthographicSize = 5f;
            cam.clearFlags       = CameraClearFlags.SolidColor;
            cam.backgroundColor  = new Color(0.08f, 0.08f, 0.14f);
            cam.depth            = -1;

            go.AddComponent<AudioListener>();
        }

        static void BuildEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        static void BuildCombatController()
        {
            var go = new GameObject("CombatController");
            go.AddComponent<Combat.CombatController>();
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

        // ── Unit panels ────────────────────────────────────────────────────

        static void BuildUnitPanels(Transform canvasTransform,
            out Combat.UnitVisual playerVisual, out Combat.UnitVisual enemyVisual)
        {
            playerVisual = MakeUnitPanel(
                canvasTransform, "PlayerPanel",
                color:        new Color(0.18f, 0.32f, 0.65f),   // blue
                label:        "HERO",
                anchorMin:    new Vector2(0.04f, 0.17f),
                anchorMax:    new Vector2(0.44f, 0.54f),
                spawnAnchor:  new Vector2(0.24f, 0.57f),
                isPlayer:     true,
                barColor:     new Color(0.3f, 0.9f, 0.4f));     // green

            enemyVisual = MakeUnitPanel(
                canvasTransform, "EnemyPanel",
                color:        new Color(0.65f, 0.18f, 0.18f),   // red
                label:        "GOBLIN",
                anchorMin:    new Vector2(0.56f, 0.17f),
                anchorMax:    new Vector2(0.96f, 0.54f),
                spawnAnchor:  new Vector2(0.76f, 0.57f),
                isPlayer:     false,
                barColor:     new Color(1.0f, 0.6f, 0.1f));     // orange
        }

        static Combat.UnitVisual MakeUnitPanel(
            Transform canvasTransform, string name,
            Color color, string label,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 spawnAnchor,
            bool isPlayer, Color barColor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(canvasTransform, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.color = color;

            MakeText(go.transform, "UnitLabel", label,
                new Vector2(0.1f, 0.35f), new Vector2(0.9f, 0.65f),
                fontSize: 32, style: FontStyle.Bold);

            var fillRT          = MakeTurnMeterBar(go.transform, barColor);
            var highlight       = MakeTargetHighlight(go);
            var statusContainer = BuildStatusContainer(go.transform);

            var visual = go.AddComponent<Combat.UnitVisual>();
            var so     = new SerializedObject(visual);
            so.FindProperty("_canvasRoot").objectReferenceValue        = canvasTransform;
            so.FindProperty("_damageSpawnAnchor").vector2Value         = spawnAnchor;
            so.FindProperty("_turnMeterFill").objectReferenceValue     = fillRT;
            so.FindProperty("_isPlayerUnit").boolValue                 = isPlayer;
            so.FindProperty("_targetHighlight").objectReferenceValue   = highlight;
            so.FindProperty("_statusContainer").objectReferenceValue   = statusContainer;
            so.ApplyModifiedPropertiesWithoutUndo();

            return visual;
        }

        // Returns the fill RectTransform — UnitVisual drives width via anchorMax.x (0→1).
        static RectTransform MakeTurnMeterBar(Transform parent, Color barColor)
        {
            // Background track
            var bgGO = new GameObject("TurnMeterBG");
            bgGO.transform.SetParent(parent, false);
            var bgRT = bgGO.AddComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0.05f, 0.82f);
            bgRT.anchorMax = new Vector2(0.95f, 0.95f);
            bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);

            // Fill bar — starts empty (anchorMax.x = 0), UnitVisual.Update() drives it.
            var fillGO = new GameObject("TurnMeterFill");
            fillGO.transform.SetParent(bgGO.transform, false);
            var fillRT = fillGO.AddComponent<RectTransform>();
            fillRT.anchorMin = new Vector2(0f, 0f);
            fillRT.anchorMax = new Vector2(0f, 1f);   // x=0 → completely empty at start
            fillRT.offsetMin = new Vector2(2f, 2f);
            fillRT.offsetMax = new Vector2(2f, -2f);  // right edge tracks anchor, not offset
            var fillImg = fillGO.AddComponent<Image>();
            fillImg.color = barColor;

            return fillRT;
        }

        // Strip at the bottom of the panel where status effect icons are spawned at runtime.
        static Transform BuildStatusContainer(Transform panelTransform)
        {
            var go = new GameObject("StatusContainer");
            go.transform.SetParent(panelTransform, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.2f, 0.04f);
            rt.anchorMax = new Vector2(0.8f, 0.30f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.18f);

            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor    = new Color(0f, 0f, 0f, 0.25f);
            shadow.effectDistance = new Vector2(0f, -1f);

            return go.transform;
        }

        // Gold border that UnitVisual toggles on/off via enable.
        static Outline MakeTargetHighlight(GameObject panelGO)
        {
            var outline = panelGO.AddComponent<Outline>();
            outline.effectColor    = new Color(1f, 0.85f, 0.2f, 0.9f);
            outline.effectDistance = new Vector2(4f, 4f);
            outline.enabled        = false;
            return outline;
        }

        // ── Title ──────────────────────────────────────────────────────────

        static void BuildTitle(Transform parent)
        {
            var (_, txt) = MakeText(
                parent, "Title", "Fragments of Eternity",
                new Vector2(0f, 0.88f), new Vector2(1f, 0.96f),
                fontSize: 52, style: FontStyle.Bold);

            txt.color = new Color(1f, 0.85f, 0.4f);
        }

        // ── HUD ────────────────────────────────────────────────────────────

        static global::UI.CombatHUD BuildHUD(
            Transform parent,
            out Text   turnLabel,
            out Text   playerHP, out Text enemyHP,
            out Button s1, out Button s2, out Button s3)
        {
            var hudGO = new GameObject("CombatHUD");
            hudGO.transform.SetParent(parent, false);
            FullStretch(hudGO.AddComponent<RectTransform>());
            var hud = hudGO.AddComponent<global::UI.CombatHUD>();

            var (_, eh) = MakeText(
                hudGO.transform, "EnemyHPLabel", "Enemy HP",
                new Vector2(0.02f, 0.79f), new Vector2(0.98f, 0.87f),
                fontSize: 30, style: FontStyle.Normal);
            eh.alignment = TextAnchor.MiddleRight;
            eh.color     = new Color(1f, 0.45f, 0.45f);
            enemyHP      = eh;

            var (_, ph) = MakeText(
                hudGO.transform, "PlayerHPLabel", "Player HP",
                new Vector2(0.02f, 0.67f), new Vector2(0.98f, 0.75f),
                fontSize: 30, style: FontStyle.Normal);
            ph.alignment = TextAnchor.MiddleLeft;
            ph.color     = new Color(0.45f, 1f, 0.6f);
            playerHP     = ph;

            var (_, tl) = MakeText(
                hudGO.transform, "TurnLabel", "–",
                new Vector2(0.3f, 0.56f), new Vector2(0.7f, 0.64f),
                fontSize: 36, style: FontStyle.Normal);
            tl.color  = Color.white;
            turnLabel = tl;

            s1 = MakeSkillButton(hudGO.transform, "Skill1Button", "Skill 1",
                new Vector2(0.04f, 0.04f), new Vector2(0.34f, 0.14f));
            s2 = MakeSkillButton(hudGO.transform, "Skill2Button", "Skill 2",
                new Vector2(0.38f, 0.04f), new Vector2(0.62f, 0.14f));
            s3 = MakeSkillButton(hudGO.transform, "Skill3Button", "Skill 3",
                new Vector2(0.66f, 0.04f), new Vector2(0.96f, 0.14f));

            return hud;
        }

        static void WireHUD(
            global::UI.CombatHUD hud,
            Text turnLabel, Text playerHP, Text enemyHP,
            Button s1, Button s2, Button s3,
            Combat.UnitVisual playerVisual, Combat.UnitVisual enemyVisual)
        {
            var so = new SerializedObject(hud);
            so.FindProperty("_turnLabel").objectReferenceValue      = turnLabel;
            so.FindProperty("_playerHPLabel").objectReferenceValue  = playerHP;
            so.FindProperty("_enemyHPLabel").objectReferenceValue   = enemyHP;
            so.FindProperty("_playerVisual").objectReferenceValue   = playerVisual;
            so.FindProperty("_enemyVisual").objectReferenceValue    = enemyVisual;
            so.FindProperty("_skill1Button").objectReferenceValue   = s1;
            so.FindProperty("_skill2Button").objectReferenceValue   = s2;
            so.FindProperty("_skill3Button").objectReferenceValue   = s3;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── Helpers ────────────────────────────────────────────────────────

        static (GameObject, Text) MakeText(
            Transform parent, string name, string content,
            Vector2 anchorMin, Vector2 anchorMax,
            int fontSize, FontStyle style)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var txt = go.AddComponent<Text>();
            txt.text      = content;
            txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize  = fontSize;
            txt.fontStyle = style;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color     = Color.white;

            return (go, txt);
        }

        static Button MakeSkillButton(
            Transform parent, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.18f, 0.28f, 0.48f);

            var btn    = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor      = new Color(0.18f, 0.28f, 0.48f);
            colors.highlightedColor = new Color(0.28f, 0.42f, 0.70f);
            colors.pressedColor     = new Color(0.10f, 0.18f, 0.32f);
            colors.selectedColor    = new Color(0.28f, 0.42f, 0.70f);
            colors.disabledColor    = new Color(0.09f, 0.11f, 0.17f, 0.85f);
            colors.fadeDuration     = 0.12f;
            btn.colors = colors;

            MakeText(go.transform, "Label", label,
                Vector2.zero, Vector2.one, fontSize: 28, style: FontStyle.Bold);

            return btn;
        }

        // ── Tooltip ────────────────────────────────────────────────────────

        static global::UI.TooltipUI BuildTooltip(Transform canvasTransform)
        {
            var go = new GameObject("Tooltip");
            go.transform.SetParent(canvasTransform, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0f);   // bottom-center: grows upward
            rt.sizeDelta        = new Vector2(300f, 165f);
            rt.anchoredPosition = Vector2.zero;

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.10f, 0.93f);

            var textGO = new GameObject("TooltipText");
            textGO.transform.SetParent(go.transform, false);
            var textRT = textGO.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(12f,  10f);
            textRT.offsetMax = new Vector2(-12f, -10f);

            var txt = textGO.AddComponent<Text>();
            txt.font            = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize        = 22;
            txt.alignment       = TextAnchor.UpperLeft;
            txt.color           = Color.white;
            txt.supportRichText = true;

            var tooltip = go.AddComponent<global::UI.TooltipUI>();
            var so      = new SerializedObject(tooltip);
            so.FindProperty("_content").objectReferenceValue = txt;
            so.ApplyModifiedPropertiesWithoutUndo();

            go.SetActive(false);
            return tooltip;
        }

        static void WireTooltip(global::UI.CombatHUD hud, global::UI.TooltipUI tooltip)
        {
            var so = new SerializedObject(hud);
            so.FindProperty("_tooltip").objectReferenceValue = tooltip;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void FullStretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
#endif
