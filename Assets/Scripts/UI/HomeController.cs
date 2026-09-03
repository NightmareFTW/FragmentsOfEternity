using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Core;
using Data;

namespace UI
{
    // Drives the Home screen: summon (x1 / x10), a tappable collection grid that
    // doubles as team selection, and entering battle with the chosen team.
    public class HomeController : MonoBehaviour
    {
        private const int TeamSize = 4;

        [SerializeField] private GachaPool     _pool;
        [SerializeField] private CampaignData  _campaign;
        [SerializeField] private Text          _gemsLabel;
        [SerializeField] private Text          _teamLabel;
        [SerializeField] private Text          _resultLabel;
        [SerializeField] private Button        _summonButton;
        [SerializeField] private Button        _summon10Button;
        [SerializeField] private Button        _resetButton;
        [SerializeField] private RectTransform _gridContainer;
        [SerializeField] private RectTransform _stageContainer;

        [Header("Hero detail overlay")]
        [SerializeField] private GameObject _detailPanel;
        [SerializeField] private Image      _detailPortrait;
        [SerializeField] private Text       _detailName;
        [SerializeField] private Text       _detailSubtitle;
        [SerializeField] private Text       _detailLevel;
        [SerializeField] private Text       _detailStats;
        [SerializeField] private Text       _detailSkills;
        [SerializeField] private Text       _detailGear;
        [SerializeField] private Button     _detailLevelUpButton;
        [SerializeField] private Button     _detailTeamButton;
        [SerializeField] private Button     _detailAutoEquipButton;
        [SerializeField] private Button     _detailUnequipButton;
        [SerializeField] private Button     _detailEnhanceButton;
        [SerializeField] private Button     _detailAscendButton;
        [SerializeField] private Button     _detailCloseButton;

        private string _detailHeroId;

        private void Start()
        {
            _summonButton?.onClick.AddListener(OnSummon);
            _summon10Button?.onClick.AddListener(OnSummon10);
            _resetButton?.onClick.AddListener(OnReset);

            _detailLevelUpButton?.onClick.AddListener(OnDetailLevelUp);
            _detailTeamButton?.onClick.AddListener(OnDetailTeamToggle);
            _detailAutoEquipButton?.onClick.AddListener(OnDetailAutoEquip);
            _detailUnequipButton?.onClick.AddListener(OnDetailUnequip);
            _detailEnhanceButton?.onClick.AddListener(OnDetailEnhance);
            _detailAscendButton?.onClick.AddListener(OnDetailAscend);
            _detailCloseButton?.onClick.AddListener(CloseDetail);
            if (_detailPanel) _detailPanel.SetActive(false);

            if (_resultLabel) _resultLabel.text = "";
            RebuildAll();
        }

        // ── Summoning ───────────────────────────────────────────────────────

        private void OnSummon()
        {
            AudioManager.Instance.Play(Sfx.Click);
            var result = GachaService.Summon(_pool);
            if (_resultLabel)
            {
                if (result.Success)
                {
                    string tag = result.IsNew ? "   NEW!" : "   (Dupe -> Ascension material)";
                    _resultLabel.text  = $"{result.Hero.heroName}  {Stars(result.Hero.rarity)}{tag}";
                    _resultLabel.color = RarityColor(result.Hero.rarity);
                }
                else SetError(result.Message);
            }
            RebuildAll();
        }

        private void OnSummon10()
        {
            AudioManager.Instance.Play(Sfx.Click);
            var results = GachaService.SummonMany(_pool, 10);
            if (results.Count == 1 && !results[0].Success)
            {
                SetError(results[0].Message);
                RebuildAll();
                return;
            }

            int best = 0, newCount = 0;
            foreach (var r in results)
            {
                if (!r.Success) continue;
                if (r.Hero.rarity > best) best = r.Hero.rarity;
                if (r.IsNew) newCount++;
            }
            if (_resultLabel)
            {
                _resultLabel.text  = $"x{results.Count} pull — best {Stars(best)}, {newCount} new";
                _resultLabel.color = RarityColor(best);
            }
            RebuildAll();
        }

        private void OnReset()
        {
            SaveSystem.Reset();
            if (_resultLabel) { _resultLabel.text = "Progress reset."; _resultLabel.color = Color.white; }
            RebuildAll();
        }

        // ── Refresh ─────────────────────────────────────────────────────────

        private void RebuildAll()
        {
            var p    = SaveSystem.Profile;
            int cost = _pool != null ? _pool.summonCost : 300;

            if (_gemsLabel) _gemsLabel.text =
                $"Gems: {p.gems}   Stamina: {StaminaService.Current()}/{StaminaService.MaxStamina}   Pity: {p.pityCounter}/{GachaService.PityThreshold}";
            if (_teamLabel) _teamLabel.text = $"Team: {p.teamHeroIds.Count}/{TeamSize}   (tap a hero to add/remove)";
            if (_summonButton)   _summonButton.interactable   = p.gems >= cost;
            if (_summon10Button) _summon10Button.interactable = p.gems >= cost;

            BuildGrid(p);
            BuildStages(p);
        }

        // ── Campaign stages ─────────────────────────────────────────────────

        private void BuildStages(PlayerProfile p)
        {
            if (_stageContainer == null || _campaign == null || _campaign.chapters == null) return;
            for (int i = _stageContainer.childCount - 1; i >= 0; i--)
                Destroy(_stageContainer.GetChild(i).gameObject);

            var stages = _campaign.AllStages();
            int n = stages.Length;
            if (n == 0) return;

            const float gap = 0.02f;
            float w = (1f - gap * (n - 1)) / n;
            for (int i = 0; i < n; i++)
            {
                float xMin = i * (w + gap);
                MakeStageButton(i, stages[i], p.campaignProgress,
                    new Vector2(xMin, 0f), new Vector2(xMin + w, 1f));
            }
        }

        // "1-2" style label: chapter number - stage-within-chapter number, both
        // 1-based, derived from the same flattened order AllStages() uses.
        private string StageLabel(int globalIndex)
        {
            if (_campaign == null || _campaign.chapters == null) return $"Stage {globalIndex + 1}";
            int offset = 0;
            for (int c = 0; c < _campaign.chapters.Length; c++)
            {
                var chapter = _campaign.chapters[c];
                int len = chapter != null && chapter.stages != null ? chapter.stages.Length : 0;
                if (globalIndex < offset + len) return $"{c + 1}-{globalIndex - offset + 1}";
                offset += len;
            }
            return $"Stage {globalIndex + 1}";
        }

        private void MakeStageButton(int index, CampaignStage stage, int progress, Vector2 aMin, Vector2 aMax)
        {
            bool locked  = index > progress;
            bool cleared = index < progress;

            var go = new GameObject($"Stage_{index}");
            go.transform.SetParent(_stageContainer, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = rt.offsetMax = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.color = locked ? new Color(0.15f, 0.15f, 0.18f, 0.90f)
                               : new Color(0.20f, 0.42f, 0.60f, 0.95f);

            var btn = go.AddComponent<Button>();
            btn.interactable = !locked;
            int captured = index;
            // A cleared stage sweeps instantly instead of replaying it — the
            // frontier stage (the one actually worth playing) still enters combat.
            if (cleared) btn.onClick.AddListener(() => SweepStage(captured));
            else         btn.onClick.AddListener(() => LoadStage(captured));

            var txt = MakeCellText(go.transform, Vector2.zero, Vector2.one);
            txt.alignment = TextAnchor.MiddleCenter;
            string line2 = locked ? "Locked" : (cleared ? "SWEEP" : $"+{stage.gemReward}");
            txt.text  = $"{StageLabel(index)}\n{line2}";
            txt.color = locked ? new Color(0.6f, 0.6f, 0.65f) : Color.white;
        }

        private void LoadStage(int index)
        {
            if (!StaminaService.CanAfford(StaminaService.StageCost))
            {
                SetError($"Not enough stamina ({StaminaService.Current()}/{StaminaService.StageCost} needed).");
                return;
            }
            StaminaService.Spend(StaminaService.StageCost);
            AudioManager.Instance.Play(Sfx.Click);
            CampaignState.SelectedStage = index;
            SceneManager.LoadScene("Combat");
        }

        // Instantly grants a stage already cleared — same stamina cost as
        // playing it, no star-rating gate (the campaign doesn't track those).
        private void SweepStage(int index)
        {
            var stages = _campaign != null ? _campaign.AllStages() : null;
            if (stages == null || index < 0 || index >= stages.Length) return;
            if (!StaminaService.CanAfford(StaminaService.StageCost))
            {
                SetError($"Not enough stamina ({StaminaService.Current()}/{StaminaService.StageCost} needed).");
                return;
            }
            StaminaService.Spend(StaminaService.StageCost);
            AudioManager.Instance.Play(Sfx.Click);

            int gems = stages[index].gemReward;
            SaveSystem.Profile.gems += gems;
            var drop = GearService.RollDrop(index);
            SaveSystem.Save();

            if (_resultLabel)
            {
                string dropText = drop != null ? $"  +{drop.slot} ({GearService.Describe(drop)})" : "";
                _resultLabel.text  = $"Swept Stage {index + 1}: +{gems} Gems{dropText}";
                _resultLabel.color = new Color(0.6f, 0.9f, 1f);
            }
            RebuildAll();
        }

        private void BuildGrid(PlayerProfile p)
        {
            if (_gridContainer == null) return;
            for (int i = _gridContainer.childCount - 1; i >= 0; i--)
                Destroy(_gridContainer.GetChild(i).gameObject);

            // Distinct owned heroes, first-seen order, with counts.
            var order  = new List<string>();
            var counts = new Dictionary<string, int>();
            foreach (var id in p.ownedHeroIds)
            {
                if (!counts.ContainsKey(id)) order.Add(id);
                counts[id] = counts.TryGetValue(id, out var c) ? c + 1 : 1;
            }

            if (order.Count == 0)
            {
                MakeGridLabel("Summon a hero to start your collection!");
                return;
            }

            const float rowH = 0.23f, step = 0.25f;
            for (int i = 0; i < order.Count && i < 4; i++)
            {
                string id   = order[i];
                var    hero = HeroById(id);
                bool   inTeam = p.teamHeroIds.Contains(id);
                float  yMax = 1f - i * step;
                MakeHeroCell(id, hero, counts[id], inTeam,
                    new Vector2(0.02f, yMax - rowH), new Vector2(0.98f, yMax));
            }
        }

        // ── Team selection ──────────────────────────────────────────────────

        private void ToggleTeam(string heroId)
        {
            var team = SaveSystem.Profile.teamHeroIds;
            if (team.Contains(heroId))
                team.Remove(heroId);
            else if (team.Count < TeamSize)
                team.Add(heroId);
            else
            {
                SetError($"Team is full ({TeamSize}). Remove one first.");
                return;
            }
            SaveSystem.Save();
            RebuildAll();
        }

        // ── Hero detail overlay ─────────────────────────────────────────────

        private void OpenDetail(string id)
        {
            AudioManager.Instance.Play(Sfx.Click);
            _detailHeroId = id;
            FillDetail();
            if (_detailPanel) _detailPanel.SetActive(true);
        }

        private void CloseDetail()
        {
            if (_detailPanel) _detailPanel.SetActive(false);
        }

        private void OnDetailLevelUp()
        {
            if (ProgressionService.LevelUp(_detailHeroId) < 0)
                SetError("Can't level up — max level or not enough gems.");
            FillDetail();
            RebuildAll();
        }

        private void OnDetailTeamToggle()
        {
            ToggleTeam(_detailHeroId);
            FillDetail();
        }

        private void OnDetailAutoEquip()
        {
            GearService.AutoEquip(_detailHeroId);
            FillDetail();
        }

        private void OnDetailUnequip()
        {
            GearService.UnequipAll(_detailHeroId);
            FillDetail();
        }

        // Always enhances the weakest equipped piece — no picker needed, and it
        // always makes progress toward a fuller build.
        private void OnDetailEnhance()
        {
            if (!GearService.EnhanceWeakestEquipped(_detailHeroId))
                SetError("Can't enhance — no equipped gear, max level, or not enough gems.");
            FillDetail();
            RebuildAll();
        }

        // Spends duplicate copies of this hero to raise its star count.
        private void OnDetailAscend()
        {
            if (!AscensionService.Ascend(_detailHeroId))
                SetError("Can't ascend — not enough duplicates, or already at max stars.");
            FillDetail();
            RebuildAll();
        }

        private void FillDetail()
        {
            var hero = HeroById(_detailHeroId);
            if (hero == null) return;

            var  p      = SaveSystem.Profile;
            int  level  = ProgressionService.GetLevel(_detailHeroId);
            bool inTeam = p.teamHeroIds.Contains(_detailHeroId);

            var gear = GearService.BonusesFor(_detailHeroId, hero);
            var asc  = AscensionService.BonusesFor(_detailHeroId, hero);
            int stars    = AscensionService.GetStars(_detailHeroId);
            int maxLevel = ProgressionService.MaxLevelFor(_detailHeroId);

            if (_detailPortrait) _detailPortrait.sprite = PortraitFor(hero);
            if (_detailName)     { _detailName.text = hero.heroName; _detailName.color = RarityColor(hero.rarity); }
            if (_detailSubtitle) _detailSubtitle.text = $"{Stars(hero.rarity)}    {hero.element}    {hero.heroClass}";
            if (_detailLevel)    _detailLevel.text = $"Level {level} / {maxLevel}    {AscensionService.StarsText(stars)}";

            if (_detailStats)
            {
                int   hp  = Mathf.RoundToInt(hero.baseHP  + hero.hpGrowth  * (level - 1)) + gear.hp + asc.hp;
                int   atk = Mathf.RoundToInt(hero.baseATK + hero.atkGrowth * (level - 1)) + gear.atk + asc.atk;
                int   def = Mathf.RoundToInt(hero.baseDEF + hero.defGrowth * (level - 1)) + gear.def + asc.def;
                int   spd = hero.baseSPD + gear.spd + asc.spd;
                float cr  = Mathf.Clamp01(hero.baseCritRate   + gear.critRate);
                float cd  = hero.baseCritDamage + gear.critDamage;
                float res = Mathf.Clamp01(hero.baseResistance + gear.resistance);
                _detailStats.text =
                    $"HP    {hp}\n" +
                    $"ATK   {atk}\n" +
                    $"DEF   {def}\n" +
                    $"SPD   {spd}\n" +
                    $"CRIT  {Mathf.RoundToInt(cr * 100)}%  x{cd:0.0}\n" +
                    $"RES   {Mathf.RoundToInt(res * 100)}%";
            }

            if (_detailSkills)
            {
                var sb = new System.Text.StringBuilder();
                foreach (var s in hero.Skills())
                    if (s != null) sb.AppendLine($"<b>{s.skillName}</b>  {s.description}");
                _detailSkills.text = sb.ToString();
            }

            if (_detailGear)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("<b>Gear</b>");
                foreach (GearSlot slot in System.Enum.GetValues(typeof(GearSlot)))
                {
                    var piece = GearService.EquippedOn(_detailHeroId, slot);
                    if (piece == null) { sb.AppendLine($"{slot}: (empty)"); continue; }
                    sb.AppendLine($"{slot}: {GearService.Describe(piece)}");
                    string subs = GearService.DescribeSubstats(piece);
                    if (!string.IsNullOrEmpty(subs)) sb.AppendLine($"    {subs}");
                }
                sb.Append($"Inventory: {GearService.Inventory.Count} pieces");
                _detailGear.text = sb.ToString();
            }

            if (_detailTeamButton) SetButtonLabel(_detailTeamButton, inTeam ? "Remove from Team" : "Add to Team");
            if (_detailLevelUpButton)
            {
                bool max  = level >= maxLevel;
                int  cost = ProgressionService.CostToLevel(level);
                _detailLevelUpButton.interactable = !max && p.gems >= cost;
                SetButtonLabel(_detailLevelUpButton, max ? "MAX LEVEL" : $"Level Up  ({cost}g)");
            }
            if (_detailEnhanceButton)
            {
                bool hasTarget = GearService.TryGetWeakestEquipped(_detailHeroId, out var weakest);
                if (hasTarget)
                {
                    int cost = GearService.EnhanceCost(weakest.enhanceLevel);
                    _detailEnhanceButton.interactable = p.gems >= cost;
                    SetButtonLabel(_detailEnhanceButton, $"Enhance Gear  ({cost}g)");
                }
                else
                {
                    _detailEnhanceButton.interactable = false;
                    SetButtonLabel(_detailEnhanceButton, "No Gear to Enhance");
                }
            }
            if (_detailAscendButton)
            {
                bool maxStars = stars >= AscensionService.MaxStars;
                int  dupes    = AscensionService.AvailableDupes(_detailHeroId);
                int  need     = AscensionService.DupesNeededForStar(stars);
                _detailAscendButton.interactable = !maxStars && dupes >= need;
                SetButtonLabel(_detailAscendButton,
                    maxStars ? "MAX STARS" : $"Ascend  ({dupes}/{need} dupes)");
            }
        }

        private static void SetButtonLabel(Button btn, string label)
        {
            if (btn == null) return;
            var txt = btn.GetComponentInChildren<Text>();
            if (txt) txt.text = label;
        }

        // ── Cell / label construction ───────────────────────────────────────

        private void MakeHeroCell(string id, HeroData hero, int count, bool inTeam,
            Vector2 aMin, Vector2 aMax)
        {
            int    rarity = hero != null ? hero.rarity : 3;
            string name   = hero != null ? hero.heroName : id;

            var go = new GameObject($"Cell_{id}");
            go.transform.SetParent(_gridContainer, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = rt.offsetMax = Vector2.zero;

            var img = go.AddComponent<Image>();
            Color rc = RarityColor(rarity);
            img.color = inTeam ? new Color(rc.r * 0.5f, rc.g * 0.5f, rc.b * 0.5f, 0.95f)
                               : new Color(0.12f, 0.12f, 0.18f, 0.95f);

            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() => OpenDetail(id));

            if (inTeam)
            {
                var outline = go.AddComponent<Outline>();
                outline.effectColor    = new Color(1f, 0.85f, 0.25f, 0.9f);
                outline.effectDistance = new Vector2(3f, 3f);
            }

            int level = ProgressionService.GetLevel(id);

            // Portrait (real art, or a generated element emblem) on the left.
            var portGO = new GameObject("Portrait");
            portGO.transform.SetParent(go.transform, false);
            var portRT = portGO.AddComponent<RectTransform>();
            portRT.anchorMin = new Vector2(0.015f, 0.12f);
            portRT.anchorMax = new Vector2(0.16f,  0.88f);
            portRT.offsetMin = portRT.offsetMax = Vector2.zero;
            var portImg = portGO.AddComponent<Image>();
            portImg.sprite         = PortraitFor(hero);
            portImg.preserveAspect = true;
            portImg.raycastTarget  = false;

            // Whole cell opens the hero detail (team + leveling live there).
            var txt = MakeCellText(go.transform, new Vector2(0.17f, 0f), Vector2.one);
            string check = inTeam ? "[✓] " : "";
            txt.text  = $"{check}{name}  {Stars(rarity)}   Lv {level}   x{count}   ›";
            txt.color = inTeam ? Color.white : new Color(0.85f, 0.9f, 1f);
        }

        private void MakeGridLabel(string message)
        {
            var go = new GameObject("EmptyLabel");
            go.transform.SetParent(_gridContainer, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.6f); rt.anchorMax = new Vector2(1f, 0.8f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var txt = MakeCellText(go.transform, Vector2.zero, Vector2.one);
            txt.text = message; txt.color = new Color(0.7f, 0.75f, 0.85f);
        }

        private static Text MakeCellText(Transform parent, Vector2 aMin, Vector2 aMax)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.offsetMin = new Vector2(12f, 0f); rt.offsetMax = new Vector2(-12f, 0f);
            var txt = go.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 28; txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleLeft;
            txt.raycastTarget = false;
            return txt;
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        private void SetError(string message)
        {
            if (_resultLabel) { _resultLabel.text = message; _resultLabel.color = new Color(1f, 0.5f, 0.5f); }
        }

        private HeroData HeroById(string heroId)
        {
            if (_pool != null && _pool.heroes != null)
                foreach (var h in _pool.heroes)
                    if (h != null && h.heroId == heroId) return h;
            return null;
        }

        private static string Stars(int rarity) => new string('★', Mathf.Clamp(rarity, 0, 5));

        private static Color RarityColor(int rarity) => rarity switch
        {
            5 => new Color(1f,   0.85f, 0.30f),
            4 => new Color(0.70f, 0.50f, 1f),
            _ => new Color(0.70f, 0.85f, 1f),
        };

        // ── Portraits ───────────────────────────────────────────────────────

        private static readonly Dictionary<string, Sprite> _emblemCache = new Dictionary<string, Sprite>();

        // Real portrait art if the hero has one; otherwise a generated element emblem.
        private static Sprite PortraitFor(HeroData hero)
        {
            if (hero == null) return null;
            if (hero.portrait != null) return hero.portrait;

            string key = !string.IsNullOrEmpty(hero.heroId) ? hero.heroId : hero.heroName;
            if (_emblemCache.TryGetValue(key, out var cached) && cached != null) return cached;

            var emblem = BuildEmblem(ElementColor(hero.element), RarityColor(hero.rarity));
            _emblemCache[key] = emblem;
            return emblem;
        }

        private static Sprite BuildEmblem(Color main, Color ring)
        {
            const int S = 96;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            float c = (S - 1) * 0.5f, r = S * 0.46f;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / r;
                    Color col;
                    if      (d > 1.00f) col = new Color(0f, 0f, 0f, 0f);
                    else if (d > 0.86f) col = ring;
                    else                col = Color.Lerp(main * 1.25f, main * 0.55f, d);
                    tex.SetPixel(x, y, col);
                }
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Color ElementColor(Element e) => e switch
        {
            Element.Fire  => new Color(0.85f, 0.30f, 0.15f),
            Element.Ice   => new Color(0.30f, 0.55f, 0.85f),
            Element.Earth => new Color(0.45f, 0.60f, 0.25f),
            Element.Light => new Color(0.90f, 0.80f, 0.35f),
            Element.Dark  => new Color(0.45f, 0.25f, 0.55f),
            _             => new Color(0.5f, 0.5f, 0.5f),
        };
    }
}
