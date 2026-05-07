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

            // Background is the first canvas child — Unity renders first sibling at the back.
            BuildBackground(canvas.transform);

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
            cam.backgroundColor  = new Color(0.05f, 0.05f, 0.10f);
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

        // ── Background ────────────────────────────────────────────────────

        static void BuildBackground(Transform canvasTransform)
        {
            var root = new GameObject("BackgroundRoot");
            root.transform.SetParent(canvasTransform, false);
            FullStretch(root.AddComponent<RectTransform>());

            // ── Backdrop layers ──────────────────────────────────────────
            MakeBGRect(root.transform, "Backdrop",
                Vector2.zero, Vector2.one,
                new Color(0.03f, 0.03f, 0.10f));

            MakeBGRect(root.transform, "SkyLayer",
                new Vector2(0f, 0.52f), new Vector2(1f, 1f),
                new Color(0.06f, 0.02f, 0.16f, 0.75f));

            MakeBGRect(root.transform, "GroundLayer",
                new Vector2(0f, 0f), new Vector2(1f, 0.20f),
                new Color(0.01f, 0.01f, 0.03f, 0.80f));

            // ── Horizon glow ─────────────────────────────────────────────
            // Raised to 0.60 alpha so the arena mid-section reads purple.
            MakeBGRect(root.transform, "Horizon",
                new Vector2(0f, 0.12f), new Vector2(1f, 0.60f),
                new Color(0.12f, 0.05f, 0.28f, 0.60f));

            // ── Fog bands ────────────────────────────────────────────────
            MakeBGRect(root.transform, "FogBand1",
                new Vector2(0f, 0.30f), new Vector2(1f, 0.42f),
                new Color(0.10f, 0.05f, 0.22f, 0.28f));
            MakeBGRect(root.transform, "FogBand2",
                new Vector2(0f, 0.15f), new Vector2(1f, 0.22f),
                new Color(0.08f, 0.05f, 0.18f, 0.35f));

            // ── Light beams ──────────────────────────────────────────────
            MakeBGBeam(root.transform, "LightBeam1",
                new Vector2(0.10f, 0.50f), new Vector2(48f, 1650f),
                new Color(0.35f, 0.60f, 1.00f, 0.025f), -5f);
            MakeBGBeam(root.transform, "LightBeam2",
                new Vector2(0.90f, 0.50f), new Vector2(40f, 1450f),
                new Color(0.90f, 0.60f, 0.15f, 0.020f), 7f);

            // ── Pillars / ruins — kept above panel zone (y > 0.55) ──────
            MakeBGRect(root.transform, "PillarL1",
                new Vector2(0.06f, 0.55f), new Vector2(0.12f, 0.82f),
                new Color(0.13f, 0.08f, 0.28f, 0.65f));
            MakeBGRect(root.transform, "PillarL2",
                new Vector2(0.16f, 0.55f), new Vector2(0.20f, 0.74f),
                new Color(0.11f, 0.07f, 0.24f, 0.50f));
            MakeBGRect(root.transform, "PillarR1",
                new Vector2(0.80f, 0.55f), new Vector2(0.86f, 0.80f),
                new Color(0.13f, 0.08f, 0.28f, 0.62f));
            MakeBGRect(root.transform, "PillarR2",
                new Vector2(0.88f, 0.55f), new Vector2(0.93f, 0.72f),
                new Color(0.11f, 0.07f, 0.24f, 0.48f));

            // Centre column visible in the gap between the two unit panels
            MakeBGRect(root.transform, "CenterPillar",
                new Vector2(0.46f, 0.55f), new Vector2(0.54f, 0.80f),
                new Color(0.11f, 0.07f, 0.24f, 0.55f));

            // ── Crystal shards — bigger and brighter ─────────────────────
            MakeBGBeam(root.transform, "CrystalL1",
                new Vector2(0.09f, 0.66f), new Vector2(28f, 110f),
                new Color(0.20f, 0.52f, 0.95f, 0.55f), 28f);
            MakeBGBeam(root.transform, "CrystalL2",
                new Vector2(0.06f, 0.61f), new Vector2(18f, 80f),
                new Color(0.30f, 0.58f, 1.00f, 0.40f), 18f);
            MakeBGBeam(root.transform, "CrystalR1",
                new Vector2(0.91f, 0.64f), new Vector2(28f, 110f),
                new Color(0.95f, 0.42f, 0.12f, 0.55f), -28f);
            MakeBGBeam(root.transform, "CrystalR2",
                new Vector2(0.94f, 0.59f), new Vector2(18f, 80f),
                new Color(1.00f, 0.48f, 0.15f, 0.40f), -18f);

            // ── Eternity Core — subtle ambient diamond between units ──────
            MakeBGBeam(root.transform, "EternityCoreGlow",
                new Vector2(0.50f, 0.67f), new Vector2(100f, 100f),
                new Color(0.55f, 0.38f, 0.08f, 0.05f), 45f);
            MakeBGBeam(root.transform, "EternityCoreOuter",
                new Vector2(0.50f, 0.67f), new Vector2(60f, 60f),
                new Color(0.65f, 0.48f, 0.12f, 0.09f), 45f);
            MakeBGBeam(root.transform, "EternityCoreInner",
                new Vector2(0.50f, 0.67f), new Vector2(36f, 36f),
                new Color(0.90f, 0.72f, 0.25f, 0.13f), 45f);

            // ── Battle platform ──────────────────────────────────────────
            MakeBGRect(root.transform, "PlatformShadow",
                new Vector2(0.02f, 0.122f), new Vector2(0.98f, 0.148f),
                new Color(0.00f, 0.00f, 0.01f, 0.70f));
            MakeBGRect(root.transform, "BattlePlatform",
                new Vector2(0.01f, 0.148f), new Vector2(0.99f, 0.198f),
                new Color(0.08f, 0.05f, 0.16f, 0.80f));
            // Soft edge line — toned down to avoid cutting the scene
            MakeBGRect(root.transform, "PlatformEdge",
                new Vector2(0.01f, 0.196f), new Vector2(0.99f, 0.202f),
                new Color(0.35f, 0.24f, 0.65f, 0.45f));
            MakeBGRect(root.transform, "PlatformGlowHero",
                new Vector2(0.04f, 0.140f), new Vector2(0.44f, 0.215f),
                new Color(0.15f, 0.42f, 0.92f, 0.15f));
            MakeBGRect(root.transform, "PlatformGlowEnemy",
                new Vector2(0.56f, 0.140f), new Vector2(0.96f, 0.215f),
                new Color(0.92f, 0.42f, 0.08f, 0.15f));
        }

        // Anchor-based solid rect — raycastTarget always false for background shapes.
        static RectTransform MakeBGRect(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color         = color;
            img.raycastTarget = false;
            return rt;
        }

        // Pivot-centred fixed-pixel rect with optional rotation — used for beams and crystals.
        static void MakeBGBeam(Transform parent, string name,
            Vector2 anchorPos, Vector2 sizeDelta, Color color, float rotation)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchorPos;
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = sizeDelta;
            if (rotation != 0f) rt.localRotation = Quaternion.Euler(0f, 0f, rotation);
            var img = go.AddComponent<Image>();
            img.color         = color;
            img.raycastTarget = false;
        }

        // ── Unit panels ────────────────────────────────────────────────────

        static void BuildUnitPanels(Transform canvasTransform,
            out Combat.UnitVisual playerVisual, out Combat.UnitVisual enemyVisual)
        {
            playerVisual = MakeUnitPanel(
                canvasTransform, "PlayerPanel",
                bgColor:     new Color(0.04f, 0.07f, 0.18f, 0.94f),  // very dark navy
                accentColor: new Color(0.35f, 0.65f, 1.00f, 0.80f),  // blue stripe
                label:       "HERO",
                anchorMin:   new Vector2(0.04f, 0.17f),
                anchorMax:   new Vector2(0.44f, 0.54f),
                spawnAnchor: new Vector2(0.24f, 0.50f),               // upper panel / head level
                isPlayer:    true,
                barColor:    new Color(0.25f, 0.85f, 0.40f));         // green ATB bar

            enemyVisual = MakeUnitPanel(
                canvasTransform, "EnemyPanel",
                bgColor:     new Color(0.16f, 0.04f, 0.04f, 0.94f),  // very dark burgundy
                accentColor: new Color(1.00f, 0.42f, 0.12f, 0.80f),  // orange stripe
                label:       "GOBLIN",
                anchorMin:   new Vector2(0.56f, 0.17f),
                anchorMax:   new Vector2(0.96f, 0.54f),
                spawnAnchor: new Vector2(0.76f, 0.50f),
                isPlayer:    false,
                barColor:    new Color(1.00f, 0.55f, 0.10f));         // orange ATB bar
        }

        static Combat.UnitVisual MakeUnitPanel(
            Transform canvasTransform, string name,
            Color bgColor, Color accentColor, string label,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 spawnAnchor,
            bool isPlayer, Color barColor)
        {
            // ── Root panel (dark background) ──────────────────────────────
            var go = new GameObject(name);
            go.transform.SetParent(canvasTransform, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.color = bgColor;

            // ── Left accent stripe ────────────────────────────────────────
            MakeRect(go.transform, "AccentStripe",
                new Vector2(0f, 0f), new Vector2(0.025f, 1f),
                accentColor);

            // ── ATB turn meter at top ─────────────────────────────────────
            var fillRT = MakeTurnMeterBar(go.transform, barColor);

            // ── Character silhouette area (y: 0.22–0.87 of panel) ─────────
            var charArea = MakeContainer(go.transform, "CharacterArea",
                new Vector2(0.045f, 0.22f), new Vector2(1.00f, 0.87f));
            BuildCharacterSilhouette(charArea, isPlayer);

            // ── Unit name label (y: 0.12–0.22) ───────────────────────────
            var (_, lbl) = MakeText(go.transform, "UnitLabel", label,
                new Vector2(0.045f, 0.12f), new Vector2(0.96f, 0.22f),
                fontSize: 24, style: FontStyle.Bold);
            lbl.color = new Color(0.82f, 0.90f, 1.00f);

            // ── Status icons strip at bottom (y: 0.01–0.10) ──────────────
            var statusContainer = BuildStatusContainer(go.transform,
                new Vector2(0.045f, 0.01f), new Vector2(0.96f, 0.10f));

            // ── Target highlight outline ───────────────────────────────────
            var highlight = MakeTargetHighlight(go);

            // ── Wire UnitVisual ───────────────────────────────────────────
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

        // ── Character silhouettes ──────────────────────────────────────────
        // All shapes use anchor coordinates within the CharacterArea container.
        // Silhouette children are parented to the panel so they animate with it
        // (idle breathing, lunge, hit reaction all move the panel RectTransform).

        static void BuildCharacterSilhouette(RectTransform charArea, bool isPlayer)
        {
            if (isPlayer) BuildHeroSilhouette(charArea.transform);
            else          BuildGoblinSilhouette(charArea.transform);
        }

        // Hero — blue/cyan warrior, narrow upright silhouette
        static void BuildHeroSilhouette(Transform p)
        {
            MakeRect(p, "StageBG",
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Color(0.08f, 0.14f, 0.28f, 0.30f));

            // Sword — very thin stripe, behind body, minimal tilt
            var sword = MakeRect(p, "Sword",
                new Vector2(0.66f, 0.33f), new Vector2(0.70f, 0.86f),
                new Color(0.70f, 0.80f, 0.95f, 0.85f));
            sword.localRotation = Quaternion.Euler(0f, 0f, -10f);

            // Legs — narrow, clearly separated
            MakeRect(p, "LegL",
                new Vector2(0.41f, 0.08f), new Vector2(0.49f, 0.43f),
                new Color(0.12f, 0.27f, 0.60f));
            MakeRect(p, "LegR",
                new Vector2(0.51f, 0.08f), new Vector2(0.59f, 0.43f),
                new Color(0.12f, 0.27f, 0.60f));

            // Left arm — tight against torso
            MakeRect(p, "ArmL",
                new Vector2(0.33f, 0.47f), new Vector2(0.40f, 0.66f),
                new Color(0.16f, 0.36f, 0.70f));

            // Torso — 20 % wide, tall central column
            MakeRect(p, "Body",
                new Vector2(0.40f, 0.43f), new Vector2(0.60f, 0.76f),
                new Color(0.20f, 0.44f, 0.82f));

            // Right arm — tight against torso
            MakeRect(p, "ArmR",
                new Vector2(0.60f, 0.47f), new Vector2(0.67f, 0.66f),
                new Color(0.16f, 0.36f, 0.70f));

            // Head — 10 % wide, clearly above torso
            MakeRect(p, "Head",
                new Vector2(0.45f, 0.78f), new Vector2(0.55f, 0.96f),
                new Color(0.68f, 0.85f, 1.00f));

            // Helmet visor
            MakeRect(p, "Visor",
                new Vector2(0.46f, 0.84f), new Vector2(0.54f, 0.89f),
                new Color(0.25f, 0.50f, 0.85f));
        }

        // Goblin — red/orange stocky creature with horns and club
        static void BuildGoblinSilhouette(Transform p)
        {
            MakeRect(p, "StageBG",
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Color(0.22f, 0.07f, 0.05f, 0.35f));

            // Horns — small accents, rendered behind head
            var hornL = MakeRect(p, "HornL",
                new Vector2(0.35f, 0.82f), new Vector2(0.44f, 0.97f),
                new Color(0.22f, 0.10f, 0.04f));
            hornL.localRotation = Quaternion.Euler(0f, 0f, 20f);

            var hornR = MakeRect(p, "HornR",
                new Vector2(0.56f, 0.82f), new Vector2(0.65f, 0.97f),
                new Color(0.22f, 0.10f, 0.04f));
            hornR.localRotation = Quaternion.Euler(0f, 0f, -20f);

            // Legs
            MakeRect(p, "LegL",
                new Vector2(0.36f, 0.05f), new Vector2(0.48f, 0.36f),
                new Color(0.38f, 0.10f, 0.05f));
            MakeRect(p, "LegR",
                new Vector2(0.52f, 0.05f), new Vector2(0.64f, 0.36f),
                new Color(0.38f, 0.10f, 0.05f));

            // Club — outside body, slightly tilted
            var club = MakeRect(p, "Club",
                new Vector2(0.76f, 0.28f), new Vector2(0.83f, 0.66f),
                new Color(0.24f, 0.12f, 0.04f, 0.90f));
            club.localRotation = Quaternion.Euler(0f, 0f, -10f);

            // Left arm
            MakeRect(p, "ArmL",
                new Vector2(0.24f, 0.40f), new Vector2(0.36f, 0.58f),
                new Color(0.42f, 0.12f, 0.06f));

            // Body — stocky (28 % wide, 30 % tall)
            MakeRect(p, "Body",
                new Vector2(0.36f, 0.36f), new Vector2(0.64f, 0.66f),
                new Color(0.50f, 0.16f, 0.08f));

            // Right arm
            MakeRect(p, "ArmR",
                new Vector2(0.64f, 0.40f), new Vector2(0.76f, 0.58f),
                new Color(0.42f, 0.12f, 0.06f));

            // Head — wider than hero for goblin proportions, clearly above body
            MakeRect(p, "Head",
                new Vector2(0.34f, 0.66f), new Vector2(0.66f, 0.92f),
                new Color(0.76f, 0.30f, 0.10f));

            // Eyes
            MakeRect(p, "EyeL",
                new Vector2(0.38f, 0.76f), new Vector2(0.47f, 0.84f),
                new Color(0.04f, 0.02f, 0.02f));
            MakeRect(p, "EyeR",
                new Vector2(0.53f, 0.76f), new Vector2(0.62f, 0.84f),
                new Color(0.04f, 0.02f, 0.02f));
        }

        // ── Turn meter ─────────────────────────────────────────────────────
        // Positioned at the TOP of the panel (y: 0.88–0.97).
        // UnitVisual.Update() drives fill via anchorMax.x (0 → 1).

        static RectTransform MakeTurnMeterBar(Transform parent, Color barColor)
        {
            var bgGO = new GameObject("TurnMeterBG");
            bgGO.transform.SetParent(parent, false);
            var bgRT = bgGO.AddComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0.045f, 0.88f);
            bgRT.anchorMax = new Vector2(0.960f, 0.97f);
            bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0.05f, 0.05f, 0.08f, 0.92f);

            var fillGO = new GameObject("TurnMeterFill");
            fillGO.transform.SetParent(bgGO.transform, false);
            var fillRT = fillGO.AddComponent<RectTransform>();
            fillRT.anchorMin = new Vector2(0f, 0f);
            fillRT.anchorMax = new Vector2(0f, 1f);
            fillRT.offsetMin = new Vector2(2f,  2f);
            fillRT.offsetMax = new Vector2(2f, -2f);
            var fillImg = fillGO.AddComponent<Image>();
            fillImg.color = barColor;

            return fillRT;
        }

        // ── Status container ───────────────────────────────────────────────

        static Transform BuildStatusContainer(Transform panelTransform,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject("StatusContainer");
            go.transform.SetParent(panelTransform, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.15f);

            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor    = new Color(0f, 0f, 0f, 0.25f);
            shadow.effectDistance = new Vector2(0f, -1f);

            return go.transform;
        }

        // ── Target highlight ───────────────────────────────────────────────

        static Outline MakeTargetHighlight(GameObject panelGO)
        {
            var outline = panelGO.AddComponent<Outline>();
            outline.effectColor    = new Color(1f, 0.85f, 0.2f, 0.70f);
            outline.effectDistance = new Vector2(3f, 3f);
            outline.enabled        = false;
            return outline;
        }

        // ── Helpers ────────────────────────────────────────────────────────

        // Container: RectTransform with no Image — just a layout anchor.
        static RectTransform MakeContainer(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        // Solid-colour rectangle — the building block for all generated shapes.
        static RectTransform MakeRect(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = color;
            return rt;
        }

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
            rt.pivot            = new Vector2(0.5f, 0f);
            rt.sizeDelta        = new Vector2(300f, 120f);
            rt.anchoredPosition = Vector2.zero;

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.10f, 0.93f);

            var textGO = new GameObject("TooltipText");
            textGO.transform.SetParent(go.transform, false);
            var textRT = textGO.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(8f,  6f);
            textRT.offsetMax = new Vector2(-8f, -6f);

            var txt = textGO.AddComponent<Text>();
            txt.font            = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize        = 18;
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
