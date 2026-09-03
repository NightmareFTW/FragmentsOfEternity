using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Core;
using Combat;

namespace UI
{
    // A lateral "who's next" queue (Epic Seven style): small portrait icons in
    // predicted turn order, nearest-to-act at the top. Purely a readability
    // aid — BattleManager.PredictTurnOrder is the source of truth, this just
    // renders whatever it broadcasts.
    public class TurnOrderUI : MonoBehaviour
    {
        [SerializeField] private RectTransform _container;
        [SerializeField] private int           _maxIcons = 6;

        private readonly List<GameObject> _icons = new List<GameObject>();

        private void Awake()
        {
            EventBus.Subscribe<TurnOrderChangedEvent>(OnOrderChanged);
            EventBus.Subscribe<CombatEndEvent>(OnCombatEnd);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<TurnOrderChangedEvent>(OnOrderChanged);
            EventBus.Unsubscribe<CombatEndEvent>(OnCombatEnd);
        }

        private void OnCombatEnd(CombatEndEvent evt) => Clear();

        private void OnOrderChanged(TurnOrderChangedEvent evt)
        {
            Clear();
            if (_container == null || evt.Order == null) return;

            int count = Mathf.Min(_maxIcons, evt.Order.Count);
            for (int i = 0; i < count; i++)
                _icons.Add(MakeIcon(evt.Order[i], i, count));
        }

        private void Clear()
        {
            foreach (var go in _icons) if (go != null) Destroy(go);
            _icons.Clear();
        }

        // Icons stack top-to-bottom, next-to-act first, shrinking slightly
        // further down the queue so the eye reads "soonest" at a glance.
        private GameObject MakeIcon(Unit unit, int index, int count)
        {
            var go = new GameObject($"Turn_{index}_{unit.Name}");
            go.transform.SetParent(_container, false);

            float slice = 1f / _maxIcons;
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f - (index + 1) * slice);
            rt.anchorMax = new Vector2(1f, 1f - index * slice);
            rt.offsetMin = new Vector2(4f, 3f);
            rt.offsetMax = new Vector2(-4f, -3f);

            float scale = Mathf.Lerp(1f, 0.72f, index / Mathf.Max(1f, count - 1));
            go.transform.localScale = new Vector3(scale, scale, 1f);

            var ring = go.AddComponent<Image>();
            ring.color = unit.Team == Team.Player
                ? new Color(0.30f, 0.60f, 1.00f, 0.95f)
                : new Color(1.00f, 0.35f, 0.30f, 0.95f);
            ring.raycastTarget = false;

            var innerGO = new GameObject("Portrait");
            innerGO.transform.SetParent(go.transform, false);
            var innerRT = innerGO.AddComponent<RectTransform>();
            innerRT.anchorMin = Vector2.zero;
            innerRT.anchorMax = Vector2.one;
            innerRT.offsetMin = new Vector2(3f, 3f);
            innerRT.offsetMax = new Vector2(-3f, -3f);

            var innerImg = innerGO.AddComponent<Image>();
            innerImg.preserveAspect = true;
            innerImg.raycastTarget  = false;
            if (unit.Portrait != null) innerImg.sprite = unit.Portrait;
            else innerImg.color = new Color(0.18f, 0.18f, 0.22f);

            return go;
        }
    }
}
