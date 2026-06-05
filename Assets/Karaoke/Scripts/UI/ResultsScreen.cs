using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Karaoke.Core;

namespace Karaoke.UI
{
    public class ResultsScreen : MonoBehaviour
    {
        [Header("Stats")]
        [SerializeField] private TextMeshProUGUI gradeText;
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI accuracyText;
        [SerializeField] private TextMeshProUGUI perfectText;
        [SerializeField] private TextMeshProUGUI goodText;
        [SerializeField] private TextMeshProUGUI missText;
        [SerializeField] private TextMeshProUGUI maxStreakText;

        [Header("Buttons")]
        [SerializeField] private Button retryButton;
        [SerializeField] private Button menuButton;

        [Header("Animation")]
        [SerializeField] private GameObject panel;

        private SongPlayer _songPlayer;

        private void Start()
        {
            _songPlayer = SongPlayer.Instance;
            if (_songPlayer != null)
                _songPlayer.OnSongFinished += OnSongFinished;

            if (retryButton != null) retryButton.onClick.AddListener(OnRetry);
            if (menuButton != null) menuButton.onClick.AddListener(OnMenu);

            if (panel != null) panel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_songPlayer != null)
                _songPlayer.OnSongFinished -= OnSongFinished;
        }

        private void OnSongFinished()
        {
            var sm = ScoreManager.Instance;
            if (sm == null) return;

            if (gradeText != null) gradeText.text = sm.Grade;
            if (scoreText != null) scoreText.text = sm.Score.ToString("N0");
            if (accuracyText != null) accuracyText.text = $"{sm.Accuracy * 100f:F1}%";
            if (perfectText != null) perfectText.text = sm.PerfectCount.ToString();
            if (goodText != null) goodText.text = sm.GoodCount.ToString();
            if (missText != null) missText.text = sm.MissCount.ToString();
            if (maxStreakText != null) maxStreakText.text = sm.MaxStreak.ToString();

            if (panel != null) panel.SetActive(true);
        }

        private void OnRetry()
        {
            if (panel != null) panel.SetActive(false);
            _songPlayer?.Play();
        }

        private void OnMenu()
        {
            // Hook up to your scene manager / navigation system
            Debug.Log("[ResultsScreen] Return to menu requested.");
        }
    }
}
