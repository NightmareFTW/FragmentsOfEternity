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

        private void Start()
        {
            _summonButton?.onClick.AddListener(OnSummon);
            _summon10Button?.onClick.AddListener(OnSummon10);
            _resetButton?.onClick.AddListener(OnReset);

            if (_resultLabel) _resultLabel.text = "";
            RebuildAll();
        }

        // ── Summoning ───────────────────────────────────────────────────────

        private void OnSummon()
        {
            var result = GachaService.Summon(_pool);
            if (_resultLabel)
            {
                if (result.Success)
                {
                    string tag = result.IsNew ? "   NEW!" : "";
                    _resultLabel.text  = $"{result.Hero.heroName}  {Stars(result.Hero.rarity)}{tag}";
                    _resultLabel.color = RarityColor(result.Hero.rarity);
                }
                else SetError(result.Message);
            }
            RebuildAll();
        }

        private void OnSummon10()
        {
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

            if (_gemsLabel) _gemsLabel.text = $"Gems: {p.gems}    Pity: {p.pityCounter}/{GachaService.PityThreshold}";
            if (_teamLabel) _teamLabel.text = $"Team: {p.teamHeroIds.Count}/{TeamSize}   (tap a hero to add/remove)";
            if (_summonButton)   _summonButton.interactable   = p.gems >= cost;
            if (_summon10Button) _summon10Button.interactable = p.gems >= cost;

            BuildGrid(p);
            BuildStages(p);
        }

        // ── Campaign stages ─────────────────────────────────────────────────

        private void BuildStages(PlayerProfile p)
        {
            if (_stageContainer == null || _campaign == null || _campaign.stages == null) return;
            for (int i = _stageContainer.childCount - 1; i >= 0; i--)
                Destroy(_stageContainer.GetChild(i).gameObject);

            int n = _campaign.stages.Length;
            if (n == 0) return;

            const float gap = 0.02f;
            float w = (1f - gap * (n - 1)) / n;
            for (int i = 0; i < n; i++)
            {
                float xMin = i * (w + gap);
                MakeStageButton(i, _campaign.stages[i], p.campaignProgress,
                    new Vector2(xMin, 0f), new Vector2(xMin + w, 1f));
            }
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
            btn.onClick.AddListener(() => LoadStage(captured));

            var txt = MakeCellText(go.transform);
            txt.alignment = TextAnchor.MiddleCenter;
            string line2 = locked ? "Locked" : (cleared ? $"✓ +{stage.gemReward}" : $"+{stage.gemReward}");
            txt.text  = $"Stage {index + 1}\n{line2}";
            txt.color = locked ? new Color(0.6f, 0.6f, 0.65f) : Color.white;
        }

        private void LoadStage(int index)
        {
            CampaignState.SelectedStage = index;
            SceneManager.LoadScene("Combat");
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
            btn.onClick.AddListener(() => ToggleTeam(id));

            if (inTeam)
            {
                var outline = go.AddComponent<Outline>();
                outline.effectColor    = new Color(1f, 0.85f, 0.25f, 0.9f);
                outline.effectDistance = new Vector2(3f, 3f);
            }

            var txt = MakeCellText(go.transform);
            string check = inTeam ? "[✓] " : "";
            txt.text  = $"{check}{name}  {Stars(rarity)}   x{count}";
            txt.color = inTeam ? Color.white : new Color(0.85f, 0.9f, 1f);
        }

        private void MakeGridLabel(string message)
        {
            var go = new GameObject("EmptyLabel");
            go.transform.SetParent(_gridContainer, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.6f); rt.anchorMax = new Vector2(1f, 0.8f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var txt = MakeCellText(go.transform);
            txt.text = message; txt.color = new Color(0.7f, 0.75f, 0.85f);
        }

        private static Text MakeCellText(Transform parent)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(16f, 0f); rt.offsetMax = new Vector2(-16f, 0f);
            var txt = go.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 30; txt.fontStyle = FontStyle.Bold;
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
    }
}
