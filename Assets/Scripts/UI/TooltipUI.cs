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

            // Activate first so Awake fires and _rt is cached before PositionAbove runs.
            gameObject.SetActive(true);
            _content.text = BuildText(skill, currentCooldown);
            PositionAbove(buttonRT);
        }

        public void Hide() => gameObject.SetActive(false);

        private static string BuildText(SkillData skill, int currentCooldown)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"<b>{skill.skillName}</b>");

            if (!string.IsNullOrEmpty(skill.description))
                sb.AppendLine(skill.description);

            string valueLabel = skill.skillType == SkillType.Heal ? "Heal" : "Damage";
            sb.AppendLine($"{valueLabel}: {skill.minValue}–{skill.maxValue}");

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

        private void PositionAbove(RectTransform buttonRT)
        {
            if (_rt == null) _rt = GetComponent<RectTransform>();
            if (_rt == null) return;

            var canvasRT = transform.parent as RectTransform;
            if (canvasRT == null) return;

            if (buttonRT == null)
            {
                _rt.anchoredPosition = Vector2.zero;
                return;
            }

            Vector3[] corners = new Vector3[4];
            buttonRT.GetWorldCorners(corners);
            // corners[1]=top-left, corners[2]=top-right; for SS Overlay, world == screen pixels
            Vector2 screenTopMid = ((Vector2)corners[1] + (Vector2)corners[2]) * 0.5f;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRT, screenTopMid, null, out Vector2 localPos);

            _rt.anchoredPosition = localPos + Vector2.up * 10f;
        }
    }
}
