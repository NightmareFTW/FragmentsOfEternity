using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Core;
using Data;

namespace UI
{
    // Drives the Home screen: summon, collection readout, and entering battle.
    public class HomeController : MonoBehaviour
    {
        [SerializeField] private GachaPool _pool;
        [SerializeField] private Text   _gemsLabel;
        [SerializeField] private Text   _collectionLabel;
        [SerializeField] private Text   _resultLabel;
        [SerializeField] private Button _summonButton;
        [SerializeField] private Button _battleButton;
        [SerializeField] private Button _resetButton;

        private void Start()
        {
            _summonButton?.onClick.AddListener(OnSummon);
            _battleButton?.onClick.AddListener(() => SceneManager.LoadScene("Combat"));
            _resetButton?.onClick.AddListener(OnReset);

            if (_resultLabel) _resultLabel.text = "";
            Refresh();
        }

        private void OnSummon()
        {
            var result = GachaService.Summon(_pool);
            if (_resultLabel)
            {
                if (result.Success)
                {
                    string stars = new string('★', Mathf.Clamp(result.Hero.rarity, 0, 5));
                    string tag   = result.IsNew ? "   NEW!" : "";
                    _resultLabel.text  = $"{result.Hero.heroName}  {stars}{tag}";
                    _resultLabel.color = RarityColor(result.Hero.rarity);
                }
                else
                {
                    _resultLabel.text  = result.Message;
                    _resultLabel.color = new Color(1f, 0.5f, 0.5f);
                }
            }
            Refresh();
        }

        private void OnReset()
        {
            SaveSystem.Reset();
            if (_resultLabel) { _resultLabel.text = "Progress reset."; _resultLabel.color = Color.white; }
            Refresh();
        }

        private void Refresh()
        {
            var p    = SaveSystem.Profile;
            int cost = _pool != null ? _pool.summonCost : 300;

            if (_gemsLabel)       _gemsLabel.text       = $"Gems: {p.gems}";
            if (_collectionLabel) _collectionLabel.text = BuildCollection(p);
            if (_summonButton)    _summonButton.interactable = p.gems >= cost;
        }

        private string BuildCollection(PlayerProfile p)
        {
            if (p.ownedHeroIds.Count == 0) return "Collection\n(empty — summon a hero!)";

            var counts = new Dictionary<string, int>();
            foreach (var id in p.ownedHeroIds)
                counts[id] = counts.TryGetValue(id, out var c) ? c + 1 : 1;

            var sb = new StringBuilder();
            sb.AppendLine($"Collection  ({p.ownedHeroIds.Count})");
            foreach (var kv in counts)
                sb.AppendLine($"{NameFor(kv.Key)}  x{kv.Value}");
            return sb.ToString();
        }

        private string NameFor(string heroId)
        {
            if (_pool != null && _pool.heroes != null)
                foreach (var h in _pool.heroes)
                    if (h != null && h.heroId == heroId) return h.heroName;
            return heroId;
        }

        private static Color RarityColor(int rarity) => rarity switch
        {
            5 => new Color(1f,   0.85f, 0.30f),
            4 => new Color(0.70f, 0.50f, 1f),
            _ => new Color(0.70f, 0.85f, 1f),
        };
    }
}
