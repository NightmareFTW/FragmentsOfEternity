using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Core;
using Combat;
using Data;

namespace UI
{
    public class CombatHUD : MonoBehaviour
    {
        [Header("Turn Display")]
        [SerializeField] private Text _turnLabel;

        [Header("HP Display")]
        [SerializeField] private Text _playerHPLabel;
        [SerializeField] private Text _enemyHPLabel;

        [Header("Unit Visuals")]
        [SerializeField] private UnitVisual _playerVisual;
        [SerializeField] private UnitVisual _enemyVisual;

        [Header("Skill Buttons")]
        [SerializeField] private Button _skill1Button;
        [SerializeField] private Button _skill2Button;
        [SerializeField] private Button _skill3Button;

        [Header("Tooltip")]
        [SerializeField] private TooltipUI _tooltip;

        private Unit        _player;
        private Unit        _enemy;
        private Color       _playerHPColor;
        private Color       _enemyHPColor;
        private SkillData[] _playerSkills;
        private int[]       _cachedCooldowns;
        private bool        _isPlayerTurn;
        private bool        _combatOver;
        private int[]       _prevCooldowns = new int[]  { 0, 0, 0 };
        private bool[]      _shaking       = new bool[] { false, false, false };

        // Subscribe in Awake — guaranteed before any Start() fires.
        private void Awake()
        {
            EventBus.Subscribe<CombatInitEvent>(OnCombatInit);
            EventBus.Subscribe<TurnStartedEvent>(OnTurnStarted);
            EventBus.Subscribe<UnitDamagedEvent>(OnUnitDamaged);
            EventBus.Subscribe<UnitHealedEvent>(OnUnitHealed);
            EventBus.Subscribe<CombatEndEvent>(OnCombatEnd);
            EventBus.Subscribe<SkillCooldownsChangedEvent>(OnSkillCooldownsChanged);
            _skill1Button?.onClick.AddListener(() => OnSkillPressed(0));
            _skill2Button?.onClick.AddListener(() => OnSkillPressed(1));
            _skill3Button?.onClick.AddListener(() => OnSkillPressed(2));
            AddTooltipTriggers(_skill1Button, 0);
            AddTooltipTriggers(_skill2Button, 1);
            AddTooltipTriggers(_skill3Button, 2);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<CombatInitEvent>(OnCombatInit);
            EventBus.Unsubscribe<TurnStartedEvent>(OnTurnStarted);
            EventBus.Unsubscribe<UnitDamagedEvent>(OnUnitDamaged);
            EventBus.Unsubscribe<UnitHealedEvent>(OnUnitHealed);
            EventBus.Unsubscribe<CombatEndEvent>(OnCombatEnd);
            EventBus.Unsubscribe<SkillCooldownsChangedEvent>(OnSkillCooldownsChanged);
        }

        // ── Event handlers ─────────────────────────────────────────────────

        private void OnCombatInit(CombatInitEvent evt)
        {
            _player       = evt.Player;
            _enemy        = evt.Enemy;
            _playerSkills = evt.PlayerSkills;
            _isPlayerTurn = false;
            _playerHPColor = _playerHPLabel ? _playerHPLabel.color : Color.white;
            _enemyHPColor  = _enemyHPLabel  ? _enemyHPLabel.color  : Color.white;
            RefreshHP(_playerHPLabel, _player);
            RefreshHP(_enemyHPLabel,  _enemy);
            SetAllButtonsInteractable(false);   // enabled only when Hero's turn starts
        }

        private void OnSkillCooldownsChanged(SkillCooldownsChangedEvent evt)
        {
            if (_combatOver) return;
            if (evt.Skills != null) _playerSkills = evt.Skills;
            _cachedCooldowns = evt.Cooldowns;
            RefreshButtonStates();
        }

        private void OnTurnStarted(TurnStartedEvent evt)
        {
            if (_turnLabel) _turnLabel.text = $"{evt.Actor.Name}'s Turn";
            _isPlayerTurn = (_player != null && evt.Actor == _player);

            if (!_isPlayerTurn)
                SetAllButtonsInteractable(false);
            else
                RefreshButtonStates();  // _cachedCooldowns already has ticked values
        }

        private void OnUnitDamaged(UnitDamagedEvent evt)
        {
            if (evt.Target == _player)
            {
                if (_playerHPLabel) StartCoroutine(DamageFlash(_playerHPLabel, _player, _playerHPColor));
                _playerVisual?.ShowHit(evt.Damage);
            }
            else if (evt.Target == _enemy)
            {
                if (_enemyHPLabel) StartCoroutine(DamageFlash(_enemyHPLabel, _enemy, _enemyHPColor));
                _enemyVisual?.ShowHit(evt.Damage);
            }
        }

        private void OnUnitHealed(UnitHealedEvent evt)
        {
            if (evt.Target == _player && _playerHPLabel)
                StartCoroutine(HealFlash(_playerHPLabel, _player, _playerHPColor));
        }

        private void OnCombatEnd(CombatEndEvent evt)
        {
            _combatOver = true;
            _tooltip?.Hide();
            if (_turnLabel) _turnLabel.text = evt.Victory ? "Victory!" : "Defeat...";
            SetAllButtonsInteractable(false);
        }

        // ── Button state ───────────────────────────────────────────────────

        private void RefreshButtonStates()
        {
            if (_cachedCooldowns == null) return;
            var cds = _cachedCooldowns;
            UpdateButtonState(_skill1Button, 0, cds.Length > 0 ? cds[0] : 0);
            UpdateButtonState(_skill2Button, 1, cds.Length > 1 ? cds[1] : 0);
            UpdateButtonState(_skill3Button, 2, cds.Length > 2 ? cds[2] : 0);
        }

        private void UpdateButtonState(Button btn, int slot, int cd)
        {
            if (btn == null || _playerSkills == null || slot >= _playerSkills.Length) return;
            bool canUse = cd == 0 && _isPlayerTurn && !_combatOver;

            // Punch animation when skill first goes on cooldown
            if (cd > 0 && _prevCooldowns[slot] == 0 && !_shaking[slot])
                StartCoroutine(PunchButton(btn, slot));
            _prevCooldowns[slot] = cd;

            btn.interactable = canUse;
            var lbl = cd > 0
                ? $"{_playerSkills[slot].skillName} ({cd}T)"
                : _playerSkills[slot].skillName;
            SetButtonLabel(btn, lbl);
            Debug.Log($"[CooldownUI] {_playerSkills[slot].skillName} cooldown={cd} interactable={canUse}");
        }

        private void SetAllButtonsInteractable(bool value)
        {
            if (_skill1Button) _skill1Button.interactable = value;
            if (_skill2Button) _skill2Button.interactable = value;
            if (_skill3Button) _skill3Button.interactable = value;
        }

        // ── Tooltip ────────────────────────────────────────────────────────

        private void AddTooltipTriggers(Button btn, int slot)
        {
            if (btn == null) return;
            var trigger = btn.gameObject.AddComponent<EventTrigger>();

            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ =>
            {
                if (_tooltip == null || _playerSkills == null || slot >= _playerSkills.Length) return;
                int cd = (_cachedCooldowns != null && slot < _cachedCooldowns.Length)
                    ? _cachedCooldowns[slot] : 0;
                _tooltip.Show(_playerSkills[slot], btn.GetComponent<RectTransform>(), cd);
            });
            trigger.triggers.Add(enter);

            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => _tooltip?.Hide());
            trigger.triggers.Add(exit);
        }

        // ── Coroutines ─────────────────────────────────────────────────────

        private System.Collections.IEnumerator PunchButton(Button btn, int slot)
        {
            _shaking[slot] = true;
            var t          = btn.transform;
            float elapsed  = 0f;
            const float Dur = 0.28f;

            while (elapsed < Dur)
            {
                elapsed += Time.deltaTime;
                float scale = 1f + Mathf.Sin(elapsed / Dur * Mathf.PI) * 0.08f;
                t.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }

            t.localScale   = Vector3.one;
            _shaking[slot] = false;
        }

        private System.Collections.IEnumerator DamageFlash(Text label, Unit unit, Color original)
        {
            label.color = Color.white;
            yield return null;
            label.color = new Color(1f, 0.25f, 0.25f);
            yield return new WaitForSeconds(0.2f);
            label.text  = $"{unit.Name}  {unit.HP} / {unit.MaxHP}";
            yield return new WaitForSeconds(0.1f);
            label.color = original;
        }

        private System.Collections.IEnumerator HealFlash(Text label, Unit unit, Color original)
        {
            label.color = Color.white;
            yield return null;
            label.color = new Color(0.3f, 1f, 0.4f);
            yield return new WaitForSeconds(0.2f);
            label.text  = $"{unit.Name}  {unit.HP} / {unit.MaxHP}";
            yield return new WaitForSeconds(0.1f);
            label.color = original;
        }

        // ── Static helpers ─────────────────────────────────────────────────

        private static void RefreshHP(Text label, Unit unit)
        {
            if (label && unit != null)
                label.text = $"{unit.Name}  {unit.HP} / {unit.MaxHP}";
        }

        private static void SetButtonLabel(Button btn, string label)
        {
            if (btn == null) return;
            var txt = btn.GetComponentInChildren<Text>();
            if (txt) txt.text = label;
        }

        private void OnSkillPressed(int slot)
        {
            Debug.Log($"[CombatHUD] Skill {slot + 1} pressed");
            EventBus.Raise(new SkillSelectedEvent { SkillSlot = slot });
        }
    }
}
