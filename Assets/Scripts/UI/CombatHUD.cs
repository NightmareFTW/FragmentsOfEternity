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

        [Header("Skill Buttons")]
        [SerializeField] private Button _skill1Button;
        [SerializeField] private Button _skill2Button;
        [SerializeField] private Button _skill3Button;

        [Header("Tooltip")]
        [SerializeField] private TooltipUI _tooltip;

        private SkillData[] _playerSkills;
        private int[]       _cachedCooldowns;
        private bool        _isPlayerTurn;
        private bool        _combatOver;
        private bool        _inputLocked;
        private int[]       _prevCooldowns = new int[]  { 0, 0, 0 };
        private bool[]      _shaking       = new bool[] { false, false, false };

        // Subscribe in Awake — guaranteed before any Start() fires.
        private void Awake()
        {
            EventBus.Subscribe<CombatInitEvent>(OnCombatInit);
            EventBus.Subscribe<TurnStartedEvent>(OnTurnStarted);
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
            EventBus.Unsubscribe<CombatEndEvent>(OnCombatEnd);
            EventBus.Unsubscribe<SkillCooldownsChangedEvent>(OnSkillCooldownsChanged);
        }

        // ── Event handlers ─────────────────────────────────────────────────

        private void OnCombatInit(CombatInitEvent evt)
        {
            _isPlayerTurn = false;
            SetAllButtonsInteractable(false);   // enabled only on an ally's turn
        }

        // Buttons follow whichever ally is currently acting.
        private void OnSkillCooldownsChanged(SkillCooldownsChangedEvent evt)
        {
            if (_combatOver) return;
            if (evt.Owner != null && evt.Owner.Team != Team.Player) return;   // ignore enemies
            _playerSkills    = evt.Skills;
            _cachedCooldowns = evt.Cooldowns;
            RefreshButtonStates();
        }

        private void OnTurnStarted(TurnStartedEvent evt)
        {
            _inputLocked  = false;   // always reset at turn boundary
            if (_turnLabel) _turnLabel.text = $"{evt.Actor.Name}'s Turn";
            _isPlayerTurn = evt.Actor.Team == Team.Player;

            if (!_isPlayerTurn)
                SetAllButtonsInteractable(false);
            else
                RefreshButtonStates();  // _cachedCooldowns already has ticked values
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
            UpdateButtonState(_skill1Button, 0);
            UpdateButtonState(_skill2Button, 1);
            UpdateButtonState(_skill3Button, 2);
        }

        private void UpdateButtonState(Button btn, int slot)
        {
            if (btn == null) return;

            // No skill in this slot for the active ally — blank and disable.
            if (_playerSkills == null || slot >= _playerSkills.Length || _playerSkills[slot] == null)
            {
                btn.interactable = false;
                SetButtonLabel(btn, "–");
                _prevCooldowns[slot] = 0;
                return;
            }

            int cd = (_cachedCooldowns != null && slot < _cachedCooldowns.Length) ? _cachedCooldowns[slot] : 0;
            bool canUse = cd == 0 && _isPlayerTurn && !_combatOver && !_inputLocked;

            // Punch animation when a skill first goes on cooldown.
            if (cd > 0 && _prevCooldowns[slot] == 0 && !_shaking[slot])
                StartCoroutine(PunchButton(btn, slot));
            _prevCooldowns[slot] = cd;

            btn.interactable = canUse;
            SetButtonLabel(btn, cd > 0 ? $"{_playerSkills[slot].skillName} ({cd}T)" : _playerSkills[slot].skillName);
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
                if (_tooltip == null || _playerSkills == null || slot >= _playerSkills.Length || _playerSkills[slot] == null) return;
                int cd = (_cachedCooldowns != null && slot < _cachedCooldowns.Length) ? _cachedCooldowns[slot] : 0;
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

        // ── Helpers ────────────────────────────────────────────────────────

        private static void SetButtonLabel(Button btn, string label)
        {
            if (btn == null) return;
            var txt = btn.GetComponentInChildren<Text>();
            if (txt) txt.text = label;
        }

        private void OnSkillPressed(int slot)
        {
            // HUD-layer guard — BattleManager is authoritative, but this prevents
            // queuing extra SkillSelectedEvents during the EndTurnDelayed window.
            if (_inputLocked || !_isPlayerTurn || _combatOver)
                return;

            if (_playerSkills == null || slot >= _playerSkills.Length || _playerSkills[slot] == null)
                return;

            int cd = (_cachedCooldowns != null && slot < _cachedCooldowns.Length) ? _cachedCooldowns[slot] : 0;
            if (cd > 0)
            {
                Debug.Log("[Input] Skill ignored: cooldown");
                return;
            }

            // Lock the UI immediately so buttons go grey before BattleManager even runs.
            _inputLocked = true;
            _tooltip?.Hide();
            SetAllButtonsInteractable(false);

            EventBus.Raise(new SkillSelectedEvent { SkillSlot = slot });
        }
    }
}
