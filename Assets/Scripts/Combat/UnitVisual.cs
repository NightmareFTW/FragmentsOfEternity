using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Core;
using Data;

namespace Combat
{
    public class UnitVisual : MonoBehaviour
    {
        [SerializeField] private Transform      _canvasRoot;
        [SerializeField] private Vector2        _damageSpawnAnchor;
        [SerializeField] private RectTransform  _turnMeterFill;
        [SerializeField] private bool           _isPlayerUnit;
        [SerializeField] private Outline        _targetHighlight;
        [SerializeField] private Transform      _statusContainer;

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
            EventBus.Subscribe<UnitDamagedEvent>(OnUnitDamagedAnim);
            EventBus.Subscribe<UnitHealedEvent>(OnUnitHealedAnim);
            EventBus.Subscribe<StatusEffectAppliedEvent>(OnStatusEffectApplied);
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
            if (_canvasRT) _canvasRT.anchoredPosition = _canvasOriginalPos;
            EventBus.Unsubscribe<CombatInitEvent>(OnCombatInit);
            EventBus.Unsubscribe<CombatEndEvent>(OnCombatEnd);
            EventBus.Unsubscribe<TargetChangedEvent>(OnTargetChanged);
            EventBus.Unsubscribe<UnitDamagedEvent>(OnUnitDamagedAnim);
            EventBus.Unsubscribe<UnitHealedEvent>(OnUnitHealedAnim);
            EventBus.Unsubscribe<StatusEffectAppliedEvent>(OnStatusEffectApplied);
        }

        // ── Event handlers ────────────────────────────────────────────────

        private void OnCombatInit(CombatInitEvent evt)
        {
            _trackedUnit         = _isPlayerUnit ? evt.Player : evt.Enemy;
            _displayFill         = 0f;
            _wasReady            = false;
            _idleEnabled         = true;
            _originalAnchoredPos = _rt != null ? _rt.anchoredPosition : Vector2.zero;
            _canvasOriginalPos   = _canvasRT != null ? _canvasRT.anchoredPosition : Vector2.zero;
            if (_turnMeterFill != null) _turnMeterFill.anchorMax = new Vector2(0f, 1f);
            if (_targetHighlight != null) _targetHighlight.enabled = false;
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
            if (evt.Target != _trackedUnit)
                PlayAttackAnimation();
            else
                ShowHit(evt.Damage);
        }

        // Self-heal is still an action; the caster should lunge.
        private void OnUnitHealedAnim(UnitHealedEvent evt)
        {
            if (evt.Target != _trackedUnit) return;
            PlayHealSFX();
            PlayAttackAnimation();
        }

        private void OnStatusEffectApplied(StatusEffectAppliedEvent evt)
        {
            if (evt.Target == _trackedUnit) RefreshStatusIcons();
        }

        // ── Update ────────────────────────────────────────────────────────

        private void Update()
        {
            if (_turnMeterFill == null || _trackedUnit == null) return;
            bool isReady = _trackedUnit.TurnMeter >= 100f;
            if (isReady && !_wasReady)
                Debug.Log($"[TurnMeter] {_trackedUnit.Name} meter full");
            _wasReady = isReady;

            float target = _combatOver
                ? _displayFill
                : Mathf.Clamp01(_trackedUnit.TurnMeter / 100f);

            _displayFill = Mathf.Lerp(_displayFill, target, Time.deltaTime * 12f);
            _turnMeterFill.anchorMax = new Vector2(_displayFill, 1f);

            IdleUpdate();
        }

        // ── Public surface ────────────────────────────────────────────────

        public void ShowHit(int damage)
        {
            PlayHitSFX();
            StartCoroutine(HitStop());
            if (_canvasRoot)
            {
                if (_shakeCoroutine != null) StopCoroutine(_shakeCoroutine);
                _shakeCoroutine = StartCoroutine(ScreenShake());
            }
            if (_canvasRoot) StartCoroutine(ImpactEffectAnim());
            PlayHitReaction();
            if (_canvasRoot) StartCoroutine(ScreenFlashAnim());
            if (_canvasRoot) StartCoroutine(SpawnDamageNumber(damage));
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
            Debug.Log("[FX] HitStop triggered");
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
            Debug.Log("[FX] ScreenShake triggered");
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

        private void PlayAttackSFX() => Debug.Log("[SFX] Attack");
        private void PlayHitSFX()    => Debug.Log("[SFX] Hit");
        private void PlayHealSFX()   => Debug.Log("[SFX] Heal");

        // ── Impact effect ─────────────────────────────────────────────────

        private IEnumerator ImpactEffectAnim()
        {
            var go = new GameObject("Impact");
            go.transform.SetParent(_canvasRoot, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin        = rt.anchorMax = _damageSpawnAnchor;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = new Vector2(60f, 60f);

            var img = go.AddComponent<Image>();
            img.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
            img.color  = new Color(1f, 0.96f, 0.65f, 0f);
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
                img.color = new Color(iconColor.r, iconColor.g, iconColor.b, 0f);

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
                elapsed += Time.deltaTime;
                float a = Mathf.Clamp01(elapsed / Dur);
                iconImg.color  = new Color(targetColor.r, targetColor.g, targetColor.b, a);
                labelTxt.color = new Color(1f, 1f, 1f, a);
                yield return null;
            }
            iconImg.color  = targetColor;
            labelTxt.color = Color.white;
        }

        private static Color GetIconColor(StatusEffectType t) => t switch
        {
            StatusEffectType.AttackUp  => new Color(0.75f, 0.22f, 0.10f),
            StatusEffectType.DefenseUp => new Color(0.10f, 0.35f, 0.75f),
            StatusEffectType.Poison    => new Color(0.38f, 0.10f, 0.60f),
            _                          => new Color(0.20f, 0.20f, 0.20f),
        };

        private static string GetIconLetter(StatusEffectType t) => t switch
        {
            StatusEffectType.AttackUp  => "A",
            StatusEffectType.DefenseUp => "D",
            StatusEffectType.Poison    => "P",
            _                          => t.ToString().Substring(0, 1),
        };

        // ── Floating damage number ────────────────────────────────────────

        private IEnumerator SpawnDamageNumber(int damage)
        {
            var go = new GameObject("DmgNum");
            go.transform.SetParent(_canvasRoot, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin        = rt.anchorMax = _damageSpawnAnchor;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = new Vector2(160f, 70f);

            var txt = go.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.text = $"-{damage}"; txt.fontSize = 48; txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter; txt.color = new Color(1f, 0.9f, 0.15f);

            float elapsed        = 0f;
            const float Duration = 0.75f;
            while (elapsed < Duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / Duration;
                rt.anchoredPosition = new Vector2(0f, Mathf.Lerp(0f, 120f, t));
                txt.color           = new Color(1f, 0.9f, 0.15f, 1f - t);
                yield return null;
            }

            Destroy(go);
        }
    }
}
