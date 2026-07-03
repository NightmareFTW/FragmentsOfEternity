using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Core;
using Data;

namespace Combat
{
    public class UnitVisual : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Transform      _canvasRoot;
        [SerializeField] private Vector2        _damageSpawnAnchor;
        [SerializeField] private RectTransform  _turnMeterFill;
        [SerializeField] private bool           _isPlayerUnit;
        [SerializeField] private int            _slotIndex;
        [SerializeField] private Outline        _targetHighlight;
        [SerializeField] private Transform      _statusContainer;
        [SerializeField] private RectTransform  _hpFill;
        [SerializeField] private Text           _hpLabel;
        [SerializeField] private Text           _nameLabel;

        private Image         _image;
        private RectTransform _rt;
        private RectTransform _canvasRT;
        private Color         _originalColor;
        private Vector2       _originalAnchoredPos;
        private Vector2       _canvasOriginalPos;
        private Vector3       _originalScale;
        private Unit          _trackedUnit;
        private bool          _combatOver;
        private float         _displayFill;
        private bool          _wasReady;
        private bool          _idleEnabled;
        private Coroutine     _attackCoroutine;
        private Coroutine     _hitCoroutine;
        private Coroutine     _shakeCoroutine;
        private SkillData     _pendingSkillVFX;
        private float         _lastShowHitTime = -1f;
        private bool          _isDead;

        private void Awake()
        {
            _image         = GetComponent<Image>();
            _rt            = GetComponent<RectTransform>();
            _originalScale = transform.localScale;
            if (_image) _originalColor = _image.color;
            if (_canvasRoot) _canvasRT = _canvasRoot.GetComponent<RectTransform>();

            EventBus.Subscribe<CombatInitEvent>(OnCombatInit);
            EventBus.Subscribe<CombatEndEvent>(OnCombatEnd);
            EventBus.Subscribe<TargetChangedEvent>(OnTargetChanged);
            EventBus.Subscribe<SkillUsedEvent>(OnSkillUsed);
            EventBus.Subscribe<UnitDamagedEvent>(OnUnitDamagedAnim);
            EventBus.Subscribe<UnitHealedEvent>(OnUnitHealedAnim);
            EventBus.Subscribe<StatusEffectAppliedEvent>(OnStatusEffectApplied);
            EventBus.Subscribe<UnitResistedEvent>(OnUnitResisted);
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
            if (_canvasRT) _canvasRT.anchoredPosition = _canvasOriginalPos;
            EventBus.Unsubscribe<CombatInitEvent>(OnCombatInit);
            EventBus.Unsubscribe<CombatEndEvent>(OnCombatEnd);
            EventBus.Unsubscribe<TargetChangedEvent>(OnTargetChanged);
            EventBus.Unsubscribe<SkillUsedEvent>(OnSkillUsed);
            EventBus.Unsubscribe<UnitDamagedEvent>(OnUnitDamagedAnim);
            EventBus.Unsubscribe<UnitHealedEvent>(OnUnitHealedAnim);
            EventBus.Unsubscribe<StatusEffectAppliedEvent>(OnStatusEffectApplied);
            EventBus.Unsubscribe<UnitResistedEvent>(OnUnitResisted);
        }

        // ── Event handlers ────────────────────────────────────────────────

        private void OnCombatInit(CombatInitEvent evt)
        {
            var team = _isPlayerUnit ? evt.Allies : evt.Enemies;
            _trackedUnit = (team != null && _slotIndex >= 0 && _slotIndex < team.Count)
                ? team[_slotIndex] : null;

            // Panel with no matching unit (encounter smaller than the scene) — hide it.
            if (_trackedUnit == null) { gameObject.SetActive(false); return; }

            _displayFill         = 0f;
            _wasReady            = false;
            _idleEnabled         = true;
            _originalAnchoredPos = _rt != null ? _rt.anchoredPosition : Vector2.zero;
            _canvasOriginalPos   = _canvasRT != null ? _canvasRT.anchoredPosition : Vector2.zero;
            if (_turnMeterFill != null) _turnMeterFill.anchorMax = new Vector2(0f, 1f);
            if (_targetHighlight != null) _targetHighlight.enabled = false;
            if (_nameLabel != null) _nameLabel.text = $"{_trackedUnit.Name.ToUpper()}  ·  {_trackedUnit.Element}";
            RefreshHP();
            RefreshStatusIcons();
        }

        private void OnCombatEnd(CombatEndEvent evt)
        {
            _combatOver  = true;
            _idleEnabled = false;
            transform.localScale = _originalScale;
            if (_rt) _rt.anchoredPosition = _originalAnchoredPos;
            if (_canvasRT) _canvasRT.anchoredPosition = _canvasOriginalPos;
            if (_targetHighlight != null) _targetHighlight.enabled = false;
        }

        private void OnTargetChanged(TargetChangedEvent evt)
        {
            if (_targetHighlight == null) return;
            _targetHighlight.enabled = (evt.Target != null && evt.Target == _trackedUnit);
        }

        // If the damaged unit is not me, I am the attacker — lunge.
        // If it IS me, I received the hit — trigger ShowHit.
        private void OnUnitDamagedAnim(UnitDamagedEvent evt)
        {
            if (_trackedUnit == null) return;

            // Damage-over-time has no attacker — only the victim reacts, no lunge.
            if (evt.IsDoT)
            {
                if (evt.Target == _trackedUnit) { ShowDoT(evt.Damage); RefreshHP(); }
                return;
            }

            if (evt.Target != _trackedUnit)
                PlayAttackAnimation();
            else
            {
                ShowHit(evt.Damage, evt.IsCrit, evt.Advantage);
                RefreshHP();
            }
        }

        // The healed unit shows a heal number and refreshes its bar (no lunge —
        // the caster's lunge/glow is driven from OnSkillUsed).
        private void OnUnitHealedAnim(UnitHealedEvent evt)
        {
            if (evt.Target != _trackedUnit) return;
            PlayHealSFX();
            ShowHeal(evt.Amount);
            RefreshHP();
        }

        private void OnStatusEffectApplied(StatusEffectAppliedEvent evt)
        {
            if (evt.Target == _trackedUnit) RefreshStatusIcons();
        }

        private void OnUnitResisted(UnitResistedEvent evt)
        {
            if (evt.Target == _trackedUnit) ShowResist();
        }

        private void OnSkillUsed(SkillUsedEvent evt)
        {
            if (_trackedUnit == null || evt.Skill == null) return;

            if (evt.Skill.skillType == SkillType.Heal)
            {
                // Heal VFX + lunge play on the caster, not the target.
                if (_trackedUnit == evt.Caster)
                {
                    if (_canvasRoot) StartCoroutine(HealGlowVFX());
                    PlayAttackAnimation();
                }
                return;
            }

            // Damage skill: queue VFX for the target's next ShowHit call.
            if (_trackedUnit == evt.Target)
                _pendingSkillVFX = evt.Skill;
        }

        // Tapping a unit panel proposes it as the current target; the
        // BattleManager decides whether the pick is legal for this turn.
        public void OnPointerClick(PointerEventData eventData)
        {
            if (_trackedUnit == null || _isDead) return;
            EventBus.Raise(new UnitClickedEvent { Unit = _trackedUnit });
        }

        // ── Update ────────────────────────────────────────────────────────

        private void Update()
        {
            if (_turnMeterFill == null || _trackedUnit == null) return;
            bool isReady = _trackedUnit.TurnMeter >= 100f;
            _wasReady = isReady;

            float target = _combatOver
                ? _displayFill
                : Mathf.Clamp01(_trackedUnit.TurnMeter / 100f);

            _displayFill = Mathf.Lerp(_displayFill, target, Time.deltaTime * 12f);
            _turnMeterFill.anchorMax = new Vector2(_displayFill, 1f);

            IdleUpdate();
        }

        // ── Public surface ────────────────────────────────────────────────

        public void ShowHit(int damage, bool isCrit = false, int advantage = 0)
        {
            string unitName = _trackedUnit?.Name ?? "?";

            // Guard against EventBus double-subscription or duplicate UnitVisuals on the
            // same unit — two ShowHit calls within 100ms from one event produce one number.
            if (Time.unscaledTime - _lastShowHitTime < 0.1f)
            {
                Debug.LogWarning($"[ShowHit] duplicate suppressed on {unitName} — check EventBus subscriptions or scene UnitVisual setup");
                return;
            }
            _lastShowHitTime = Time.unscaledTime;

            PlayHitSFX();
            StartCoroutine(HitStop());
            if (_canvasRoot)
            {
                if (_shakeCoroutine != null) StopCoroutine(_shakeCoroutine);
                _shakeCoroutine = StartCoroutine(ScreenShake());
            }
            if (_canvasRoot) StartCoroutine(SkillImpactVFX(_pendingSkillVFX));
            _pendingSkillVFX = null;
            PlayHitReaction();
            if (_canvasRoot) StartCoroutine(ScreenFlashAnim());
            if (_canvasRoot) StartCoroutine(SpawnDamageNumber(
                damage,
                isCrit ? new Color(1f, 0.35f, 0.20f) : new Color(1f, 0.9f, 0.15f),
                isCrit ? 66 : 48,
                isCrit,
                advantage));
        }

        // Lightweight feedback for damage-over-time: a coloured number and a
        // brief tint, without the hit-stop / screen-shake of a real hit.
        private void ShowDoT(int damage)
        {
            if (_canvasRoot) StartCoroutine(SpawnDamageNumber(
                damage, new Color(0.55f, 0.90f, 0.35f), 40, false));
            if (_image) StartCoroutine(DoTTint());
        }

        private IEnumerator DoTTint()
        {
            if (_image == null) yield break;
            _image.color = new Color(0.55f, 0.90f, 0.35f);
            yield return new WaitForSeconds(0.12f);
            _image.color = _originalColor;
        }

        // Rising green "+N" for heals.
        private void ShowHeal(int amount)
        {
            if (amount <= 0 || _canvasRoot == null) return;
            StartCoroutine(SpawnHealNumber(amount));
        }

        // Grey "RESIST" when a debuff is shrugged off.
        private void ShowResist()
        {
            if (_canvasRoot != null)
                StartCoroutine(SpawnFloatingText("RESIST", new Color(0.78f, 0.80f, 0.88f), 34));
        }

        private IEnumerator SpawnFloatingText(string text, Color color, int fontSize)
        {
            var go = new GameObject("FloatText");
            go.transform.SetParent(_canvasRoot, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = _damageSpawnAnchor;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(220f, 70f);

            var txt = go.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.text = text; txt.fontSize = fontSize; txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter; txt.color = color;

            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor    = new Color(0f, 0f, 0f, 0.6f);
            shadow.effectDistance = new Vector2(2f, -2f);

            float elapsed = 0f;
            const float Dur = 0.75f;
            while (elapsed < Dur)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / Dur;
                rt.anchoredPosition = new Vector2(0f, Mathf.Lerp(0f, 110f, t));
                txt.color = new Color(color.r, color.g, color.b, 1f - t);
                yield return null;
            }
            Destroy(go);
        }

        private IEnumerator SpawnHealNumber(int amount)
        {
            var go = new GameObject("HealNum");
            go.transform.SetParent(_canvasRoot, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = _damageSpawnAnchor;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(180f, 80f);

            var txt = go.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.text = $"+{amount}"; txt.fontSize = 44; txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter; txt.color = new Color(0.35f, 1f, 0.45f);

            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor    = new Color(0f, 0f, 0f, 0.6f);
            shadow.effectDistance = new Vector2(2f, -2f);

            float elapsed        = 0f;
            const float Duration = 0.8f;
            while (elapsed < Duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / Duration;
                rt.anchoredPosition = new Vector2(0f, Mathf.Lerp(0f, 120f, t));
                txt.color = new Color(0.35f, 1f, 0.45f, 1f - t);
                yield return null;
            }

            Destroy(go);
        }

        // ── HP bar ─────────────────────────────────────────────────────────

        private void RefreshHP()
        {
            if (_trackedUnit == null) return;
            float ratio = _trackedUnit.MaxHP > 0 ? (float)_trackedUnit.HP / _trackedUnit.MaxHP : 0f;
            if (_hpFill != null) _hpFill.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
            if (_hpLabel != null) _hpLabel.text = $"{_trackedUnit.HP} / {_trackedUnit.MaxHP}";
            if (!_trackedUnit.IsAlive) MarkDead();
        }

        // Dim the whole panel and take it out of the action once the unit falls.
        private void MarkDead()
        {
            if (_isDead) return;
            _isDead       = true;
            _idleEnabled  = false;
            transform.localScale = _originalScale;
            if (_rt) _rt.anchoredPosition = _originalAnchoredPos;
            if (_targetHighlight != null) _targetHighlight.enabled = false;

            var cg = GetComponent<CanvasGroup>();
            if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0.35f;
        }

        // ── Idle breathing ────────────────────────────────────────────────

        private void IdleUpdate()
        {
            if (!_idleEnabled || _trackedUnit == null) return;
            float phase = _isPlayerUnit ? 0f : Mathf.PI;
            float s     = 1.02f + Mathf.Sin(Time.time * 1.2f + phase) * 0.02f;
            transform.localScale = new Vector3(s, s, 1f);
        }

        // ── Attack animation ──────────────────────────────────────────────

        private void PlayAttackAnimation()
        {
            PlayAttackSFX();
            if (_attackCoroutine != null) StopCoroutine(_attackCoroutine);
            _attackCoroutine = StartCoroutine(AttackAnim());
        }

        private IEnumerator AttackAnim()
        {
            _idleEnabled         = false;
            transform.localScale = _originalScale;

            float   lunge  = _isPlayerUnit ? 60f : -60f;
            Vector2 origin = _originalAnchoredPos;
            float elapsed = 0f;
            while (elapsed < 0.2f)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / 0.2f);
                if (_rt) _rt.anchoredPosition = new Vector2(
                    origin.x + Mathf.Sin(t * Mathf.PI * 0.5f) * lunge, origin.y);
                transform.localScale = new Vector3(
                    Mathf.Lerp(_originalScale.x, _originalScale.x * 1.08f, t),
                    Mathf.Lerp(_originalScale.y, _originalScale.y * 0.92f, t),
                    1f);
                yield return null;
            }

            // Overshoot — briefly push 10% past the lunge peak.
            float overshoot = lunge * 1.1f;
            elapsed = 0f;
            while (elapsed < 0.05f)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / 0.05f);
                if (_rt) _rt.anchoredPosition = new Vector2(
                    origin.x + Mathf.Lerp(lunge, overshoot, t), origin.y);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < 0.2f)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / 0.2f);
                if (_rt) _rt.anchoredPosition = new Vector2(
                    origin.x + Mathf.Lerp(overshoot, 0f, t), origin.y);
                transform.localScale = new Vector3(
                    Mathf.Lerp(_originalScale.x * 1.08f, _originalScale.x, t),
                    Mathf.Lerp(_originalScale.y * 0.92f, _originalScale.y, t),
                    1f);
                yield return null;
            }

            if (_rt) _rt.anchoredPosition = origin;
            transform.localScale = _originalScale;
            _idleEnabled         = true;
            _attackCoroutine     = null;
        }

        // ── Hit reaction ──────────────────────────────────────────────────

        private void PlayHitReaction()
        {
            // Getting hit interrupts the lunge; snap back first.
            if (_attackCoroutine != null)
            {
                StopCoroutine(_attackCoroutine);
                _attackCoroutine = null;
                _idleEnabled     = true;
                transform.localScale = _originalScale;
            }
            if (_rt) _rt.anchoredPosition = _originalAnchoredPos;
            if (_hitCoroutine != null) StopCoroutine(_hitCoroutine);
            _hitCoroutine = StartCoroutine(HitReactionAnim());
        }

        private IEnumerator HitReactionAnim()
        {
            // Delay so attacker's lunge is visible before the reaction lands.
            yield return new WaitForSeconds(0.05f);

            Vector2 origin  = _originalAnchoredPos;
            float knockback = _isPlayerUnit ? -12f : 12f;

            if (_rt) _rt.anchoredPosition = new Vector2(origin.x + knockback, origin.y);
            float elapsed = 0f;
            while (elapsed < 0.12f)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / 0.12f);
                if (_rt) _rt.anchoredPosition = new Vector2(
                    Mathf.Lerp(origin.x + knockback, origin.x, t), origin.y);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < 0.15f)
            {
                elapsed += Time.deltaTime;
                float jitter = Mathf.Sin(elapsed * 85f) * 7f;
                if (_rt) _rt.anchoredPosition = new Vector2(origin.x + jitter, origin.y);
                yield return null;
            }
            if (_rt) _rt.anchoredPosition = origin;

            if (_image)
            {
                _image.color = Color.white;
                yield return new WaitForSeconds(0.07f);
                _image.color = new Color(1f, 0.3f, 0.3f);
                yield return new WaitForSeconds(0.12f);
                _image.color = _originalColor;
            }

            _hitCoroutine = null;
        }

        // ── Hit-stop ──────────────────────────────────────────────────────

        private IEnumerator HitStop()
        {
            Time.timeScale = 0.05f;
            yield return new WaitForSecondsRealtime(0.04f);
            Time.timeScale = 1f;
        }

        // ── Screen flash ─────────────────────────────────────────────────

        private IEnumerator ScreenFlashAnim()
        {
            var go = new GameObject("ScreenFlash");
            go.transform.SetParent(_canvasRoot, false);
            go.transform.SetAsLastSibling();   // render on top of everything

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var img = go.AddComponent<Image>();
            float elapsed   = 0f;
            const float Dur = 0.1f;
            while (elapsed < Dur)
            {
                elapsed += Time.unscaledDeltaTime;   // runs at real speed during hit-stop
                float t = Mathf.Clamp01(elapsed / Dur);
                img.color = new Color(1f, 1f, 1f, 0.20f * (1f - t));
                yield return null;
            }

            Destroy(go);
        }

        // ── Screen shake ──────────────────────────────────────────────────

        private IEnumerator ScreenShake()
        {
            if (_canvasRT == null) yield break;
            const float Dur       = 0.15f;
            const float Intensity = 5f;
            float elapsed = 0f;
            while (elapsed < Dur)
            {
                elapsed += Time.unscaledDeltaTime;
                float fade    = 1f - Mathf.Clamp01(elapsed / Dur);
                float offsetX = Random.Range(-1f, 1f) * Intensity * fade;
                float offsetY = Random.Range(-1f, 1f) * Intensity * fade;
                _canvasRT.anchoredPosition = _canvasOriginalPos + new Vector2(offsetX, offsetY);
                yield return null;
            }

            _canvasRT.anchoredPosition = _canvasOriginalPos;
            _shakeCoroutine = null;
        }

        // ── SFX hooks (stubs — wire real AudioSource here later) ──────────

        private void PlayAttackSFX() { }
        private void PlayHitSFX()    { }
        private void PlayHealSFX()   { }

        // ── Skill VFX dispatcher ─────────────────────────────────────────
        // Null skill = enemy attack with no skill → fall back to generic impact.

        private IEnumerator SkillImpactVFX(SkillData skill)
        {
            if (skill == null)
            {
                yield return StartCoroutine(ImpactEffectAnim());
                yield break;
            }
            if (skill.skillName == "Slash")
            {
                yield return StartCoroutine(SlashVFX());
                yield break;
            }
            if (skill.skillName == "Heavy Blow")
            {
                yield return StartCoroutine(HeavyBlowVFX());
                yield break;
            }
            yield return StartCoroutine(ImpactEffectAnim());
        }

        // Slash — three layered diagonal streaks across the target.
        private IEnumerator SlashVFX()
        {
            if (_canvasRoot == null) yield break;

            var sizes   = new Vector2[] { new Vector2(220f, 14f), new Vector2(170f, 8f), new Vector2(120f, 5f) };
            var offsets = new Vector2[] { Vector2.zero, new Vector2(0f, 18f), new Vector2(0f, -16f) };
            var alphas  = new float[]   { 1f, 0.75f, 0.5f };
            var gos     = new GameObject[3];
            var imgs    = new Image[3];

            for (int i = 0; i < 3; i++)
            {
                gos[i] = new GameObject($"SlashVFX_{i}");
                gos[i].transform.SetParent(_canvasRoot, false);
                gos[i].transform.SetAsLastSibling();
                gos[i].transform.localRotation = Quaternion.Euler(0f, 0f, 45f);

                var rt = gos[i].AddComponent<RectTransform>();
                rt.anchorMin        = rt.anchorMax = _damageSpawnAnchor;
                rt.anchoredPosition = offsets[i];
                rt.sizeDelta        = sizes[i];

                imgs[i] = gos[i].AddComponent<Image>();
                imgs[i].color = new Color(1f, 1f, 1f, 0f);
            }

            const float Dur = 0.22f;
            float elapsed = 0f;
            while (elapsed < Dur)
            {
                elapsed += Time.deltaTime;
                float t      = Mathf.Clamp01(elapsed / Dur);
                float scaleX = t < 0.4f ? t / 0.4f : 1f;
                float fade   = t < 0.5f ? 1f : Mathf.Pow(1f - (t - 0.5f) / 0.5f, 2f);
                for (int i = 0; i < 3; i++)
                {
                    gos[i].transform.localScale = new Vector3(scaleX, 1f, 1f);
                    imgs[i].color = new Color(1f, 1f, 1f, alphas[i] * fade);
                }
                yield return null;
            }

            for (int i = 0; i < 3; i++) Destroy(gos[i]);
        }

        // Heavy Blow — large orange diamond burst plus a secondary darker pulse.
        private IEnumerator HeavyBlowVFX()
        {
            if (_canvasRoot == null) yield break;

            var go = new GameObject("HeavyBlowVFX");
            go.transform.SetParent(_canvasRoot, false);
            go.transform.SetAsLastSibling();
            go.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin        = rt.anchorMax = _damageSpawnAnchor;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = new Vector2(80f, 80f);

            var img = go.AddComponent<Image>();
            img.color = new Color(1f, 0.45f, 0.1f, 0f);

            var go2 = new GameObject("HeavyBlowVFX_Pulse");
            go2.transform.SetParent(_canvasRoot, false);
            go2.transform.SetAsLastSibling();
            go2.transform.localRotation = Quaternion.Euler(0f, 0f, 22f);

            var rt2 = go2.AddComponent<RectTransform>();
            rt2.anchorMin        = rt2.anchorMax = _damageSpawnAnchor;
            rt2.anchoredPosition = Vector2.zero;
            rt2.sizeDelta        = new Vector2(60f, 60f);

            var img2 = go2.AddComponent<Image>();
            img2.color = new Color(0.7f, 0.2f, 0.0f, 0f);

            const float Dur = 0.38f;
            float elapsed = 0f;
            while (elapsed < Dur)
            {
                elapsed += Time.deltaTime;
                float t      = elapsed / Dur;
                float scale  = Mathf.Sin(t * Mathf.PI) * 4.5f;
                float scale2 = Mathf.Sin(Mathf.Clamp01(t * 1.2f - 0.1f) * Mathf.PI) * 3.2f;
                go.transform.localScale  = new Vector3(scale,  scale,  1f);
                go2.transform.localScale = new Vector3(scale2, scale2, 1f);
                img.color  = new Color(1f,  0.45f, 0.1f, 0.92f * Mathf.Pow(1f - t, 1.5f));
                img2.color = new Color(0.7f, 0.2f, 0.0f, 0.80f * Mathf.Pow(Mathf.Max(0f, 1f - t * 1.3f), 1.5f));
                yield return null;
            }

            Destroy(go);
            Destroy(go2);
        }

        // Recover — two green radiating rings that expand from the caster.
        private IEnumerator HealGlowVFX()
        {
            StartCoroutine(HealRing(0f));
            yield return StartCoroutine(HealRing(0.12f));
        }

        private IEnumerator HealRing(float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (_canvasRoot == null) yield break;

            var go = new GameObject("HealRing");
            go.transform.SetParent(_canvasRoot, false);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin        = rt.anchorMax = _damageSpawnAnchor;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = new Vector2(70f, 70f);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.3f, 1f, 0.4f, 0f);

            const float Dur = 0.45f;
            float elapsed = 0f;
            while (elapsed < Dur)
            {
                elapsed += Time.deltaTime;
                float t     = elapsed / Dur;
                float scale = Mathf.Lerp(0.5f, 3.5f, t);
                go.transform.localScale = new Vector3(scale, scale, 1f);
                img.color = new Color(0.3f, 1f, 0.4f, 0.8f * Mathf.Pow(1f - t, 1.8f));
                yield return null;
            }

            Destroy(go);
        }

        // ── Impact effect ─────────────────────────────────────────────────

        private IEnumerator ImpactEffectAnim()
        {
            var go = new GameObject("Impact");
            go.transform.SetParent(_canvasRoot, false);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin        = rt.anchorMax = _damageSpawnAnchor;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = new Vector2(60f, 60f);

            var img = go.AddComponent<Image>();
            img.color = new Color(1f, 0.96f, 0.65f, 0f);
            float elapsed   = 0f;
            const float Dur = 0.30f;
            while (elapsed < Dur)
            {
                elapsed += Time.deltaTime;
                float t     = elapsed / Dur;
                float scale = Mathf.Sin(t * Mathf.PI) * 2.6f;
                go.transform.localScale = new Vector3(scale, scale, 1f);
                float alpha = Mathf.Pow(1f - t, 2.5f);
                img.color = new Color(1f, 0.96f, 0.65f, 0.90f * alpha);
                yield return null;
            }

            Destroy(go);
        }

        // ── Status icons ──────────────────────────────────────────────────

        private void RefreshStatusIcons()
        {
            if (_statusContainer == null || _trackedUnit == null) return;

            for (int i = _statusContainer.childCount - 1; i >= 0; i--)
                Destroy(_statusContainer.GetChild(i).gameObject);

            var effects = _trackedUnit.GetEffects();
            if (effects.Count == 0) return;

            const float IconSize = 50f;
            const float Spacing  = 12f;

            float totalWidth = effects.Count * IconSize + (effects.Count - 1) * Spacing;
            float startX     = -totalWidth * 0.5f;

            for (int i = 0; i < effects.Count; i++)
            {
                var   effect    = effects[i];
                Color iconColor = GetIconColor(effect.Type);

                var iconGO = new GameObject($"StatusIcon_{i}");
                iconGO.transform.SetParent(_statusContainer, false);

                var rt = iconGO.AddComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot            = new Vector2(0f,   0.5f);
                rt.sizeDelta        = new Vector2(IconSize, IconSize);
                rt.anchoredPosition = new Vector2(startX + i * (IconSize + Spacing), 14f);

                var img = iconGO.AddComponent<Image>();
                img.color         = new Color(iconColor.r, iconColor.g, iconColor.b, 0f);
                img.raycastTarget = false;   // don't block target-selection taps

                var lblGO = new GameObject("Label");
                lblGO.transform.SetParent(iconGO.transform, false);
                var lblRT = lblGO.AddComponent<RectTransform>();
                lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one;
                lblRT.offsetMin = lblRT.offsetMax = Vector2.zero;

                var txt = lblGO.AddComponent<Text>();
                txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                txt.text = $"{GetIconLetter(effect.Type)}\n{effect.Duration}";
                txt.fontSize = 16; txt.fontStyle = FontStyle.Bold;
                txt.alignment = TextAnchor.MiddleCenter; txt.color = new Color(1f, 1f, 1f, 0f);
                txt.raycastTarget = false;

                var shadow = lblGO.AddComponent<Shadow>();
                shadow.effectColor    = new Color(0f, 0f, 0f, 0.85f);
                shadow.effectDistance = new Vector2(1f, -1f);

                StartCoroutine(FadeInIcon(img, txt, iconColor));
            }
        }

        private IEnumerator FadeInIcon(Image iconImg, Text labelTxt, Color targetColor)
        {
            float elapsed   = 0f;
            const float Dur = 0.15f;
            while (elapsed < Dur)
            {
                // A refresh may destroy these icons mid-fade — bail if they're gone.
                if (iconImg == null || labelTxt == null) yield break;
                elapsed += Time.deltaTime;
                float a = Mathf.Clamp01(elapsed / Dur);
                iconImg.color  = new Color(targetColor.r, targetColor.g, targetColor.b, a);
                labelTxt.color = new Color(1f, 1f, 1f, a);
                yield return null;
            }
            if (iconImg == null || labelTxt == null) yield break;
            iconImg.color  = targetColor;
            labelTxt.color = Color.white;
        }

        private static Color GetIconColor(StatusEffectType t) => t switch
        {
            StatusEffectType.AttackUp      => new Color(0.75f, 0.22f, 0.10f),
            StatusEffectType.AttackDebuff  => new Color(0.45f, 0.20f, 0.28f),
            StatusEffectType.DefenseUp     => new Color(0.10f, 0.35f, 0.75f),
            StatusEffectType.DefenseDebuff => new Color(0.20f, 0.28f, 0.42f),
            StatusEffectType.SpeedBuff     => new Color(0.15f, 0.55f, 0.45f),
            StatusEffectType.SpeedDebuff   => new Color(0.30f, 0.35f, 0.30f),
            StatusEffectType.Poison        => new Color(0.38f, 0.10f, 0.60f),
            StatusEffectType.Burn          => new Color(0.80f, 0.30f, 0.08f),
            StatusEffectType.Bleed         => new Color(0.60f, 0.08f, 0.12f),
            StatusEffectType.Stun          => new Color(0.85f, 0.70f, 0.15f),
            StatusEffectType.Sleep         => new Color(0.30f, 0.30f, 0.55f),
            StatusEffectType.Silence       => new Color(0.45f, 0.30f, 0.55f),
            StatusEffectType.Shield        => new Color(0.25f, 0.65f, 0.85f),
            StatusEffectType.Barrier       => new Color(0.30f, 0.55f, 0.80f),
            _                              => new Color(0.20f, 0.20f, 0.20f),
        };

        private static string GetIconLetter(StatusEffectType t) => t switch
        {
            StatusEffectType.AttackUp      => "A+",
            StatusEffectType.AttackDebuff  => "A-",
            StatusEffectType.DefenseUp     => "D+",
            StatusEffectType.DefenseDebuff => "D-",
            StatusEffectType.SpeedBuff     => "S+",
            StatusEffectType.SpeedDebuff   => "S-",
            StatusEffectType.Poison        => "PSN",
            StatusEffectType.Burn          => "BRN",
            StatusEffectType.Bleed         => "BLD",
            StatusEffectType.Stun          => "STN",
            StatusEffectType.Sleep         => "SLP",
            StatusEffectType.Silence       => "SIL",
            StatusEffectType.Shield        => "SHD",
            StatusEffectType.Barrier       => "BAR",
            _                              => t.ToString().Substring(0, 1),
        };

        // ── Floating damage number ────────────────────────────────────────

        private IEnumerator SpawnDamageNumber(int damage, Color color, int fontSize, bool isCrit, int advantage = 0)
        {
            var go = new GameObject("DmgNum");
            go.transform.SetParent(_canvasRoot, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin        = rt.anchorMax = _damageSpawnAnchor;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = new Vector2(200f, 80f);

            // A fully-absorbed hit (0 HP lost) reads as a shield BLOCK. Elemental
            // advantage / weakness colours the number and adds a ▲ / ▼ marker.
            bool   blocked = damage <= 0;
            string mark    = advantage > 0 ? "  ▲" : advantage < 0 ? "  ▼" : "";
            Color  col     = blocked            ? new Color(0.50f, 0.85f, 1.00f)
                           : advantage > 0      ? new Color(1.00f, 0.45f, 0.15f)   // super-effective
                           : advantage < 0      ? new Color(0.70f, 0.72f, 0.78f)   // resisted element
                           :                      color;

            var txt = go.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.text = blocked ? "BLOCK" : (isCrit ? $"-{damage}!{mark}" : $"-{damage}{mark}");
            txt.fontSize = fontSize; txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter; txt.color = col;

            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor    = new Color(0f, 0f, 0f, 0.6f);
            shadow.effectDistance = new Vector2(2f, -2f);

            float rise     = isCrit ? 150f : 120f;
            float duration = isCrit ? 0.90f : 0.75f;
            float pop       = isCrit ? 1.4f : 1f;
            float elapsed  = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                rt.anchoredPosition = new Vector2(0f, Mathf.Lerp(0f, rise, t));
                float s = Mathf.Lerp(pop, 1f, Mathf.Clamp01(t * 3f));
                go.transform.localScale = new Vector3(s, s, 1f);
                txt.color = new Color(col.r, col.g, col.b, 1f - t);
                yield return null;
            }

            Destroy(go);
        }
    }
}
