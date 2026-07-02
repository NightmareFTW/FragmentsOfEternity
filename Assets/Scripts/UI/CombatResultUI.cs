using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Core;
using Combat;

namespace UI
{
    // Shows the end-of-battle overlay (outcome + reward) and returns to Home.
    // Lives on an always-active object so it hears CombatEndEvent even though
    // the visual panel it toggles starts hidden.
    public class CombatResultUI : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Text       _outcomeLabel;
        [SerializeField] private Text       _rewardLabel;
        [SerializeField] private Button     _continueButton;

        private void Awake()
        {
            EventBus.Subscribe<CombatEndEvent>(OnCombatEnd);
            _continueButton?.onClick.AddListener(Continue);
            if (_panel) _panel.SetActive(false);
        }

        private void OnDestroy() => EventBus.Unsubscribe<CombatEndEvent>(OnCombatEnd);

        private void OnCombatEnd(CombatEndEvent evt)
        {
            if (_panel) _panel.SetActive(true);

            if (_outcomeLabel)
            {
                _outcomeLabel.text  = evt.Victory ? "VICTORY" : "DEFEAT";
                _outcomeLabel.color = evt.Victory ? new Color(1f, 0.85f, 0.3f)
                                                  : new Color(1f, 0.4f, 0.4f);
            }

            if (_rewardLabel)
            {
                _rewardLabel.text = evt.Victory
                    ? $"+{RewardService.GrantVictory()} Gems"
                    : "No reward — try again!";
            }
        }

        private void Continue()
        {
            if (Application.CanStreamedLevelBeLoaded("Home"))
                SceneManager.LoadScene("Home");
        }
    }
}
