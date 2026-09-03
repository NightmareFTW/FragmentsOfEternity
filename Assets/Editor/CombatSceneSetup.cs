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

            // Load the campaign (created via RPG → Create Starter Content). Panels
            // size to stage 1's encounter; the actual stage is resolved at runtime.
            var campaign   = AssetDatabase.LoadAssetAtPath<Data.CampaignData>(
                "Assets/ScriptableObjects/Campaign.asset");
            var allStages  = campaign != null ? campaign.AllStages() : null;
            var encounter  = (allStages != null && allStages.Length > 0)
                ? allStages[0].encounter
                : AssetDatabase.LoadAssetAtPath<Data.EncounterData>(
                    "Assets/ScriptableObjects/Encounters/Stage1.asset");

            BuildCamera();
            BuildEventSystem();
            BuildCombatController(encounter, campaign);

            var canvas = BuildCanvas();

            // Background is the first canvas child — Unity renders first sibling at the back.
            BuildBackground(canvas.transform);

            // Unit panels must be added before the HUD so they render behind it.
            BuildUnitPanels(canvas.transform, encounter);

            BuildTurnOrderQueue(canvas.transform);

            var hud = BuildHUD(canvas.transform,
                out var turnLabel,
                out var s1, out var s2, out var s3,
                out var auto, out var speed);

            WireHUD(hud, turnLabel, s1, s2, s3, auto, speed);

            // Tooltip must be the last canvas child so it renders on top of everything.
            var tooltip = BuildTooltip(canvas.transform);
            WireTooltip(hud, tooltip);

            // End-of-battle result overlay renders above even the tooltip.
            BuildResultPanel(canvas.transform, campaign);

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

        static void BuildCombatController(Data.EncounterData encounter, Data.CampaignData campaign)
        {
            var go         = new GameObject("CombatController");
            var controller = go.AddComponent<Combat.CombatController>();

            // Wire the fallback encounter, the campaign (selected stage wins), and
            // the gacha pool (resolves the player's saved team ids into HeroData).
            var pool = AssetDatabase.LoadAssetAtPath<Data.GachaPool>(
                "Assets/ScriptableObjects/GachaPool.asset");

            var so = new SerializedObject(controller);
            if (encounter != null) so.FindProperty("_encounter").objectReferenceValue = encounter;
            if (campaign  != null) so.FindProperty("_campaign").objectReferenceValue   = campaign;
            if (pool      != null) so.FindProperty("_heroPool").objectReferenceValue   = pool;
            so.ApplyModifiedPropertiesWithoutUndo();
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

            // ── Generated art (baked once as real assets; see ProceduralArt) ──
            var skyGrad    = ProceduralArt.VerticalGradient("Bg_CombatSky",
                new Color(0.10f, 0.04f, 0.22f), new Color(0.04f, 0.02f, 0.11f));
            var horizonGlow = ProceduralArt.RadialGlow("Bg_CombatHorizon", new Color(0.55f, 0.35f, 0.95f, 1f), 160, 1.6f);
            var coreOuter   = ProceduralArt.RadialGlow("Bg_CoreOuter",   new Color(0.85f, 0.65f, 0.20f, 1f), 128, 2.0f);
            var coreInner   = ProceduralArt.RadialGlow("Bg_CoreInner",   new Color(1.00f, 0.90f, 0.45f, 1f), 96,  2.6f);
            var crystalBlue = ProceduralArt.RadialGlow("Bg_CrystalBlue", new Color(0.35f, 0.65f, 1.00f, 1f), 96,  1.8f);
            var crystalGold = ProceduralArt.RadialGlow("Bg_CrystalGold", new Color(1.00f, 0.55f, 0.20f, 1f), 96,  1.8f);
            var floorGlow   = ProceduralArt.RadialGlow("Bg_FloorGlow",   Color.white, 128, 1.4f);
            var stars       = ProceduralArt.Speckle("Bg_CombatStars", new Color(0.85f, 0.85f, 1.00f, 1f), 512, 90, 1.2f, 2.6f, seed: 4242);
            var nebula1     = ProceduralArt.NebulaCloud("Bg_CombatNebula1", new Color(0.30f, 0.15f, 0.55f, 0.55f), 384, seed: 11, baseScale: 2.5f);
            var nebula2     = ProceduralArt.NebulaCloud("Bg_CombatNebula2", new Color(0.55f, 0.30f, 0.15f, 0.35f), 384, seed: 97, baseScale: 3.5f);
            var skyline     = ProceduralArt.JaggedSkyline("Bg_CombatSkyline", new Color(0.10f, 0.06f, 0.20f, 0.75f), 768, 300, seed: 55);

            // ── Backdrop layers ──────────────────────────────────────────
            MakeBGRect(root.transform, "Backdrop",
                Vector2.zero, Vector2.one,
                new Color(0.03f, 0.03f, 0.10f));

            ProceduralArt.Place(root.transform, "SkyLayer", skyGrad,
                new Vector2(0f, 0.52f), new Vector2(1f, 1f), new Color(1f, 1f, 1f, 0.85f));

            // Speckled stars over the sky, faded near the horizon.
            ProceduralArt.Place(root.transform, "SkyStars", stars,
                new Vector2(0f, 0.60f), new Vector2(1f, 1f), new Color(1f, 1f, 1f, 0.5f));

            // Organic nebula patches break up the flat gradient with real
            // colour variation instead of a uniform band.
            ProceduralArt.Place(root.transform, "SkyNebula1", nebula1,
                new Vector2(-0.2f, 0.55f), new Vector2(0.75f, 1.05f), new Color(1f, 1f, 1f, 0.8f));
            ProceduralArt.Place(root.transform, "SkyNebula2", nebula2,
                new Vector2(0.25f, 0.58f), new Vector2(1.2f, 1.0f), new Color(1f, 1f, 1f, 0.7f));

            MakeBGRect(root.transform, "GroundLayer",
                new Vector2(0f, 0f), new Vector2(1f, 0.20f),
                new Color(0.01f, 0.01f, 0.03f, 0.80f));

            // ── Horizon glow — a soft radial stretched into a wide band ───
            ProceduralArt.Place(root.transform, "Horizon", horizonGlow,
                new Vector2(-0.15f, 0.12f), new Vector2(1.15f, 0.60f), new Color(1f, 1f, 1f, 0.55f));

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

            // ── Ruined skyline — a jagged silhouette along the horizon, full
            // width, sitting low (below the formations, which start at
            // FormationYMin = 0.20) instead of flanking pillars that would
            // now sit behind the left/right unit columns.
            ProceduralArt.Place(root.transform, "RuinSkyline", skyline,
                new Vector2(0f, 0.10f), new Vector2(1f, 0.34f), new Color(1f, 1f, 1f, 1f));

            // ── Crystal shards — soft glowing blobs instead of flat chips ──
            ProceduralArt.PlaceFixed(root.transform, "CrystalL1", crystalBlue,
                new Vector2(0.09f, 0.66f), new Vector2(90f, 90f), new Color(1f, 1f, 1f, 0.65f));
            ProceduralArt.PlaceFixed(root.transform, "CrystalL2", crystalBlue,
                new Vector2(0.06f, 0.61f), new Vector2(60f, 60f), new Color(1f, 1f, 1f, 0.50f));
            ProceduralArt.PlaceFixed(root.transform, "CrystalR1", crystalGold,
                new Vector2(0.91f, 0.64f), new Vector2(90f, 90f), new Color(1f, 1f, 1f, 0.65f));
            ProceduralArt.PlaceFixed(root.transform, "CrystalR2", crystalGold,
                new Vector2(0.94f, 0.59f), new Vector2(60f, 60f), new Color(1f, 1f, 1f, 0.50f));

            // ── Eternity Core — a real soft glow instead of 3 fake-diamond squares ──
            ProceduralArt.PlaceFixed(root.transform, "EternityCoreOuter", coreOuter,
                new Vector2(0.50f, 0.67f), new Vector2(220f, 220f), new Color(1f, 1f, 1f, 0.28f));
            ProceduralArt.PlaceFixed(root.transform, "EternityCoreInner", coreInner,
                new Vector2(0.50f, 0.67f), new Vector2(110f, 110f), new Color(1f, 1f, 1f, 0.45f));

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
            ProceduralArt.Place(root.transform, "PlatformGlowHero", floorGlow,
                new Vector2(0.04f, 0.100f), new Vector2(0.44f, 0.255f), new Color(0.20f, 0.50f, 1.00f, 0.30f));
            ProceduralArt.Place(root.transform, "PlatformGlowEnemy", floorGlow,
                new Vector2(0.56f, 0.100f), new Vector2(0.96f, 0.255f), new Color(1.00f, 0.45f, 0.10f, 0.30f));
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

        // Classic side-on JRPG arrangement: player party on the left facing
        // right, enemies on the right facing left. The queue occupies the
        // strip left of the player column (see BuildTurnOrderQueue).
        static void BuildUnitPanels(Transform canvasTransform, Data.EncounterData encounter)
        {
            string[] allyLabels  = LabelsFrom(encounter != null ? encounter.allies  : null, "HERO");
            string[] enemyLabels = LabelsFrom(encounter != null ? encounter.enemies : null, "GOBLIN");

            BuildTeamColumn(canvasTransform, "Player", allyLabels,  0.100f, 0.490f, isPlayer: true);
            BuildTeamColumn(canvasTransform, "Enemy",  enemyLabels, 0.510f, 0.965f, isPlayer: false);
        }

        // Cross/diamond formation per slot: x = depth (0 furthest from the
        // opponent, 1 closest — the "front line" / tank spot), y = spread
        // across the formation's own vertical band. Slot 0 is always front,
        // the last slot is always back (healer spot); classes aren't
        // auto-detected — it's a positional formation, not a role one.
        static readonly Vector2[] Formation1 = { new Vector2(0.50f, 0.50f) };
        static readonly Vector2[] Formation2 = { new Vector2(0.62f, 0.34f), new Vector2(0.38f, 0.66f) };
        static readonly Vector2[] Formation3 = { new Vector2(0.68f, 0.50f), new Vector2(0.36f, 0.25f), new Vector2(0.36f, 0.75f) };
        static readonly Vector2[] Formation4 = { new Vector2(0.72f, 0.50f), new Vector2(0.46f, 0.25f), new Vector2(0.46f, 0.75f), new Vector2(0.22f, 0.50f) };

        const float PanelHalfW = 0.095f;
        const float PanelHalfH = 0.100f;

        // Shared vertical band both formations spread across — a mobile
        // portrait canvas has far more height than width to spend here.
        const float FormationYMin = 0.20f;
        const float FormationYMax = 0.86f;

        static Vector2[] FormationFor(int count) => count switch
        {
            1 => Formation1,
            2 => Formation2,
            3 => Formation3,
            4 => Formation4,
            _ => null,
        };

        // Lays out one team's panels in the cross formation within [xMin,xMax]
        // (falls back to an even vertical spread for team sizes the formation
        // table doesn't cover).
        static void BuildTeamColumn(Transform canvasTransform, string prefix, string[] labels,
            float xMin, float xMax, bool isPlayer)
        {
            int count     = Mathf.Max(1, labels.Length);
            var formation = FormationFor(count);

            for (int i = 0; i < count; i++)
            {
                float depthT, spreadT;
                if (formation != null) { depthT = formation[i].x; spreadT = formation[i].y; }
                else { depthT = 0.5f; spreadT = (i + 0.5f) / count; }

                // Depth 1 = nearest the opponent: toward xMax for the player
                // (facing right), toward xMin for enemies (facing left).
                float cx = isPlayer ? Mathf.Lerp(xMin, xMax, depthT) : Mathf.Lerp(xMax, xMin, depthT);
                float cy = Mathf.Lerp(FormationYMin, FormationYMax, spreadT);

                var aMin  = new Vector2(cx - PanelHalfW, cy - PanelHalfH);
                var aMax  = new Vector2(cx + PanelHalfW, cy + PanelHalfH);
                var spawn = new Vector2(cx, cy);

                MakeUnitPanel(canvasTransform, $"{prefix}Panel{i}", labels[i],
                    aMin, aMax, spawn, isPlayer, slotIndex: i);
            }
        }

        static string[] LabelsFrom(Data.HeroData[] team, string fallback)
        {
            if (team == null || team.Length == 0) return new[] { fallback };
            var labels = new string[team.Length];
            for (int i = 0; i < team.Length; i++)
                labels[i] = (team[i] != null && !string.IsNullOrEmpty(team[i].heroName))
                    ? team[i].heroName.ToUpper() : fallback;
            return labels;
        }

        // No frame: the panel root is an invisible click-catcher sized to the
        // formation slot; only the avatar, a thin HP bar and status icons are
        // actually drawn. The target highlight and hit-flash both live on the
        // portrait itself so they read against the character, not a box.
        static Combat.UnitVisual MakeUnitPanel(
            Transform canvasTransform, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 spawnAnchor,
            bool isPlayer, int slotIndex)
        {
            var go = new GameObject(name);
            go.transform.SetParent(canvasTransform, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            // Fully transparent — still catches taps (Image.raycastTarget
            // ignores alpha), but draws nothing.
            var img = go.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f);

            var hpFill = MakeHPBar(go.transform);

            // ── Character silhouette (fallback when no portrait art exists) ─
            var charArea = MakeContainer(go.transform, "CharacterArea",
                new Vector2(0.05f, 0.10f), new Vector2(0.95f, 0.84f));
            BuildCharacterSilhouette(charArea, isPlayer);

            // Portrait overlay (real art) — disabled until a Sprite is set at runtime.
            var portraitGO = new GameObject("Portrait");
            portraitGO.transform.SetParent(go.transform, false);
            var portraitRT = portraitGO.AddComponent<RectTransform>();
            portraitRT.anchorMin = new Vector2(0.05f, 0.10f);
            portraitRT.anchorMax = new Vector2(0.95f, 0.84f);
            portraitRT.offsetMin = portraitRT.offsetMax = Vector2.zero;
            var portraitImg = portraitGO.AddComponent<Image>();
            portraitImg.color          = Color.white;
            portraitImg.preserveAspect = true;
            portraitImg.raycastTarget  = false;
            portraitImg.enabled        = false;

            // Target highlight glows the portrait itself, not a bounding box.
            var highlight = portraitGO.AddComponent<Outline>();
            highlight.effectColor    = new Color(1f, 0.85f, 0.2f, 0.85f);
            highlight.effectDistance = new Vector2(4f, 4f);
            highlight.enabled        = false;

            // ── Status icons strip at bottom ──────────────────────────────
            var statusContainer = BuildStatusContainer(go.transform,
                new Vector2(0.05f, 0.00f), new Vector2(0.95f, 0.085f));

            // ── Wire UnitVisual ───────────────────────────────────────────
            var visual = go.AddComponent<Combat.UnitVisual>();
            var so     = new SerializedObject(visual);
            so.FindProperty("_canvasRoot").objectReferenceValue        = canvasTransform;
            so.FindProperty("_damageSpawnAnchor").vector2Value         = spawnAnchor;
            so.FindProperty("_isPlayerUnit").boolValue                 = isPlayer;
            so.FindProperty("_slotIndex").intValue                     = slotIndex;
            so.FindProperty("_targetHighlight").objectReferenceValue   = highlight;
            so.FindProperty("_statusContainer").objectReferenceValue   = statusContainer;
            so.FindProperty("_hpFill").objectReferenceValue            = hpFill;
            so.FindProperty("_portraitImage").objectReferenceValue     = portraitImg;
            so.FindProperty("_silhouette").objectReferenceValue        = charArea.gameObject;
            so.FindProperty("_hitFlashImage").objectReferenceValue     = portraitImg;
            so.ApplyModifiedPropertiesWithoutUndo();

            return visual;
        }

        // A slim bar just above the avatar — no framing box, no number label
        // (panels are compact now; a number here would be unreadably small).
        static RectTransform MakeHPBar(Transform parent)
        {
            var bgGO = new GameObject("HPBarBG");
            bgGO.transform.SetParent(parent, false);
            var bgRT = bgGO.AddComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0.10f, 0.87f);
            bgRT.anchorMax = new Vector2(0.90f, 0.95f);
            bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color         = new Color(0.06f, 0.02f, 0.02f, 0.85f);
            bgImg.raycastTarget = false;

            var fillGO = new GameObject("HPBarFill");
            fillGO.transform.SetParent(bgGO.transform, false);
            var fillRT = fillGO.AddComponent<RectTransform>();
            fillRT.anchorMin = new Vector2(0f, 0f);
            fillRT.anchorMax = new Vector2(1f, 1f);
            fillRT.offsetMin = fillRT.offsetMax = Vector2.zero;
            var fillImg = fillGO.AddComponent<Image>();
            fillImg.color         = new Color(0.30f, 0.85f, 0.35f);
            fillImg.raycastTarget = false;

            return fillRT;
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
            bg.color         = new Color(0f, 0f, 0f, 0.15f);
            bg.raycastTarget = false;

            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor    = new Color(0f, 0f, 0f, 0.25f);
            shadow.effectDistance = new Vector2(0f, -1f);

            return go.transform;
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
            img.color         = color;
            img.raycastTarget = false;   // decoration — let the panel root catch clicks
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
            txt.raycastTarget = false;   // labels never intercept unit/target clicks

            return (go, txt);
        }

        // ── Turn order queue ─────────────────────────────────────────────────
        // A lateral "who's next" strip along the left edge (same side as the
        // player party), replacing the old per-panel ATB fill bars (which read
        // as confusingly similar to the HP bars). Epic Seven-style:
        // nearest-to-act at the top, shrinking down.

        static global::UI.TurnOrderUI BuildTurnOrderQueue(Transform canvasTransform)
        {
            var go = new GameObject("TurnOrderQueue");
            go.transform.SetParent(canvasTransform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.010f, FormationYMin);
            rt.anchorMax = new Vector2(0.075f, FormationYMax);
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var bg = go.AddComponent<Image>();
            bg.color         = new Color(0.02f, 0.02f, 0.05f, 0.35f);
            bg.raycastTarget = false;

            // "NEXT ▼" header so the flow direction (top = soonest to act,
            // reading down from there) is obvious at a glance rather than
            // something the player has to infer from icon size alone.
            var (_, nextLabel) = MakeText(go.transform, "NextLabel", "NEXT",
                new Vector2(0f, 0.960f), new Vector2(1f, 1.000f),
                fontSize: 13, style: FontStyle.Bold);
            nextLabel.color = new Color(1f, 0.85f, 0.35f, 0.95f);

            var (_, nextArrow) = MakeText(go.transform, "NextArrow", "▼",
                new Vector2(0f, 0.900f), new Vector2(1f, 0.960f),
                fontSize: 16, style: FontStyle.Bold);
            nextArrow.color = new Color(1f, 0.85f, 0.35f, 0.80f);

            var iconArea = MakeContainer(go.transform, "IconArea",
                new Vector2(0f, 0f), new Vector2(1f, 0.895f));

            var ui = go.AddComponent<global::UI.TurnOrderUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("_container").objectReferenceValue = iconArea;
            so.ApplyModifiedPropertiesWithoutUndo();

            return ui;
        }

        // ── HUD ────────────────────────────────────────────────────────────

        static global::UI.CombatHUD BuildHUD(
            Transform parent,
            out Text   turnLabel,
            out Button s1, out Button s2, out Button s3,
            out Button auto, out Button speed)
        {
            var hudGO = new GameObject("CombatHUD");
            hudGO.transform.SetParent(parent, false);
            FullStretch(hudGO.AddComponent<RectTransform>());
            var hud = hudGO.AddComponent<global::UI.CombatHUD>();

            // A thin band just above the skill buttons and below the formation
            // (FormationYMin) — with both teams now sharing the centre of the
            // screen (left/right, not top/bottom) there's no more open middle
            // gap to put this in.
            var (_, tl) = MakeText(
                hudGO.transform, "TurnLabel", "–",
                new Vector2(0.15f, 0.155f), new Vector2(0.85f, 0.195f),
                fontSize: 30, style: FontStyle.Normal);
            tl.color  = Color.white;
            turnLabel = tl;

            s1 = MakeSkillButton(hudGO.transform, "Skill1Button", "Skill 1",
                new Vector2(0.04f, 0.04f), new Vector2(0.34f, 0.14f));
            s2 = MakeSkillButton(hudGO.transform, "Skill2Button", "Skill 2",
                new Vector2(0.38f, 0.04f), new Vector2(0.62f, 0.14f));
            s3 = MakeSkillButton(hudGO.transform, "Skill3Button", "Skill 3",
                new Vector2(0.66f, 0.04f), new Vector2(0.96f, 0.14f));

            // Battle controls — top band (where the title used to sit), clearly
            // visible and selectable as their own options rather than tucked in.
            auto = MakeSkillButton(hudGO.transform, "AutoButton", "Auto: OFF",
                new Vector2(0.55f, 0.885f), new Vector2(0.76f, 0.955f));
            speed = MakeSkillButton(hudGO.transform, "SpeedButton", "1x",
                new Vector2(0.77f, 0.885f), new Vector2(0.98f, 0.955f));

            return hud;
        }

        static void WireHUD(
            global::UI.CombatHUD hud,
            Text turnLabel,
            Button s1, Button s2, Button s3,
            Button auto, Button speed)
        {
            var so = new SerializedObject(hud);
            so.FindProperty("_turnLabel").objectReferenceValue      = turnLabel;
            so.FindProperty("_skill1Button").objectReferenceValue   = s1;
            so.FindProperty("_skill2Button").objectReferenceValue   = s2;
            so.FindProperty("_skill3Button").objectReferenceValue   = s3;
            so.FindProperty("_autoButton").objectReferenceValue     = auto;
            so.FindProperty("_speedButton").objectReferenceValue    = speed;
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

        // ── Result overlay ─────────────────────────────────────────────────

        static void BuildResultPanel(Transform canvasTransform, Data.CampaignData campaign)
        {
            // Controller sits on an always-active object so it hears CombatEndEvent.
            var ctrlGO = new GameObject("CombatResult");
            ctrlGO.transform.SetParent(canvasTransform, false);
            FullStretch(ctrlGO.AddComponent<RectTransform>());
            var ui = ctrlGO.AddComponent<global::UI.CombatResultUI>();

            // Hidden overlay panel (child), switched on at battle end.
            var panel = new GameObject("Panel");
            panel.transform.SetParent(ctrlGO.transform, false);
            FullStretch(panel.AddComponent<RectTransform>());
            var dim = panel.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.80f);   // blocks input to the finished board

            var (_, outcome) = MakeText(panel.transform, "Outcome", "VICTORY",
                new Vector2(0.08f, 0.55f), new Vector2(0.92f, 0.68f), 92, FontStyle.Bold);
            outcome.color = new Color(1f, 0.85f, 0.3f);

            var (_, reward) = MakeText(panel.transform, "Reward", "+0 Gems",
                new Vector2(0.08f, 0.46f), new Vector2(0.92f, 0.54f), 46, FontStyle.Bold);
            reward.color = new Color(0.6f, 0.9f, 1f);

            var cont = MakeSkillButton(panel.transform, "ContinueButton", "CONTINUE",
                new Vector2(0.25f, 0.30f), new Vector2(0.75f, 0.38f));

            var so = new SerializedObject(ui);
            so.FindProperty("_panel").objectReferenceValue          = panel;
            so.FindProperty("_outcomeLabel").objectReferenceValue   = outcome;
            so.FindProperty("_rewardLabel").objectReferenceValue    = reward;
            so.FindProperty("_continueButton").objectReferenceValue = cont;
            if (campaign != null) so.FindProperty("_campaign").objectReferenceValue = campaign;
            so.ApplyModifiedPropertiesWithoutUndo();

            panel.SetActive(false);
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
