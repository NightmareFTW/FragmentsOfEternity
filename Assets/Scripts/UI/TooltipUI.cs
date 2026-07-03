using UnityEngine;
using UnityEngine.UI;
using Data;

namespace UI
{
    public class TooltipUI : MonoBehaviour
    {
        [SerializeField] private Text _content;

        private RectTransform _rt;

        private void Awake() => _rt = GetComponent<RectTransform>();

        public void Show(SkillData skill, RectTransform buttonRT, int currentCooldown = 0)
        {
            if (skill == null) { Hide(); return; }

            if (_content == null)
            {
                Debug.LogWarning("[TooltipUI] _content is not wired — rebuild the scene via RPG → Setup Combat Scene.");
                return;
            }

            gameObject.SetActive(true);
            _content.text = BuildText(skill, currentCooldown);
            // Force layout rebuild so _rt.rect.height is current before positioning.
            Canvas.ForceUpdateCanvases();
            PositionAbove(buttonRT);
        }

        public void Hide() => gameObject.SetActive(false);

        private static string BuildText(SkillData skill, int currentCooldown)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"<b>{skill.skillName}</b>");

            if (!string.IsNullOrEmpty(skill.description))
                sb.AppendLine(skill.description);

            sb.AppendLine($"Target: {TargetLabel(skill.targetType)}");

            if (skill.skillType != SkillType.Buff)
            {
                string valueLabel = skill.skillType == SkillType.Heal ? "Heal" : "Damage";
                sb.AppendLine($"{valueLabel}: {skill.minValue}–{skill.maxValue}");
            }

            if (currentCooldown > 0)
            {
                string s = currentCooldown == 1 ? "turn" : "turns";
                sb.Append($"On cooldown: {currentCooldown} {s} remaining");
            }
            else if (skill.cooldownTurns > 0)
            {
                string s = skill.cooldownTurns == 1 ? "turn" : "turns";
                sb.Append($"Cooldown: {skill.cooldownTurns} {s}");
            }
            else
            {
                sb.Append("No cooldown");
            }

            return sb.ToString();
        }

        private static string TargetLabel(TargetType t) => t switch
        {
            TargetType.SingleEnemy => "One enemy",
            TargetType.AllEnemies  => "All enemies",
            TargetType.SingleAlly  => "One ally",
            TargetType.AllAllies   => "All allies",
            TargetType.Self        => "Self",
            _                      => t.ToString(),
        };

        private void PositionAbove(RectTransform buttonRT)
        {
            if (_rt == null) _rt = GetComponent<RectTransform>();
            if (_rt == null || buttonRT == null) { if (_rt != null) _rt.anchoredPosition = Vector2.zero; return; }

            var canvasRT = transform.parent as RectTransform;
            if (canvasRT == null) return;

            // For SS Overlay: world corners == screen pixel coordinates.
            // Corners: 0=BL, 1=TL, 2=TR, 3=BR
            Vector3[] btnCorners = new Vector3[4];
            buttonRT.GetWorldCorners(btnCorners);

            Vector2 btnTopMid    = new Vector2((btnCorners[1].x + btnCorners[2].x) * 0.5f, btnCorners[1].y);
            Vector2 btnBottomMid = new Vector2((btnCorners[0].x + btnCorners[3].x) * 0.5f, btnCorners[0].y);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, btnTopMid,    null, out Vector2 localTop);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, btnBottomMid, null, out Vector2 localBottom);

            const float Gap      = 8f;
            float       tipH     = _rt.rect.height;
            float       tipW     = _rt.rect.width;
            float       halfCanW = canvasRT.rect.width  * 0.5f;
            float       halfCanH = canvasRT.rect.height * 0.5f;

            // Keep tooltip horizontally centred on the button but clamped inside the canvas.
            float clampedX = Mathf.Clamp(localTop.x, -halfCanW + tipW * 0.5f, halfCanW - tipW * 0.5f);

            // Prefer above; fall back to below when the top of the tooltip would exceed the canvas.
            bool fitsAbove = (localTop.y + Gap + tipH) <= halfCanH;
            if (fitsAbove)
            {
                _rt.pivot            = new Vector2(0.5f, 0f);   // grow upward from bottom edge
                _rt.anchoredPosition = new Vector2(clampedX, localTop.y + Gap);
            }
            else
            {
                _rt.pivot            = new Vector2(0.5f, 1f);   // grow downward from top edge
                _rt.anchoredPosition = new Vector2(clampedX, localBottom.y - Gap);
            }
        }
    }
}
