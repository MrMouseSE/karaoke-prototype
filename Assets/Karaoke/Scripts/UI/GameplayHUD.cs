using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Karaoke.Core;

namespace Karaoke.UI
{
    public class GameplayHUD : MonoBehaviour
    {
        [Header("Score")]
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI streakText;

        [Header("Progress")]
        [SerializeField] private Slider progressBar;

        [Header("Judgement Feedback")]
        [SerializeField] private TextMeshProUGUI judgementText;
        [SerializeField] private float judgementDisplaySec = 0.6f;

        private ScoreManager _scoreManager;
        private SongPlayer _songPlayer;
        private float _judgementTimer;
        private float _songDurationMs;

        private void Start()
        {
            _scoreManager = ScoreManager.Instance;
            _songPlayer = SongPlayer.Instance;

            if (_scoreManager != null)
            {
                _scoreManager.OnScoreChanged += UpdateScoreUI;
                _scoreManager.OnNoteJudged += ShowJudgement;
            }
            if (_songPlayer != null)
                _songPlayer.OnSongStarted += OnSongStarted;

            if (judgementText != null) judgementText.gameObject.SetActive(false);
            UpdateScoreUI(0, 0);
        }

        private void OnDestroy()
        {
            if (_scoreManager != null)
            {
                _scoreManager.OnScoreChanged -= UpdateScoreUI;
                _scoreManager.OnNoteJudged -= ShowJudgement;
            }
            if (_songPlayer != null) _songPlayer.OnSongStarted -= OnSongStarted;
        }

        private void OnSongStarted()
        {
            _songDurationMs = _songPlayer.SongData.TotalDurationMs;
            if (progressBar != null) { progressBar.minValue = 0f; progressBar.maxValue = 1f; progressBar.value = 0f; }
        }

        private void Update()
        {
            if (_songPlayer != null && _songPlayer.IsPlaying && _songDurationMs > 0f)
                if (progressBar != null) progressBar.value = _songPlayer.SongTimeMs / _songDurationMs;

            if (judgementText != null && judgementText.gameObject.activeSelf)
            {
                _judgementTimer -= Time.deltaTime;
                if (_judgementTimer <= 0f) judgementText.gameObject.SetActive(false);
            }
        }

        private void UpdateScoreUI(int score, int streak)
        {
            if (scoreText != null) scoreText.text = score.ToString("N0");
            if (streakText != null) streakText.text = streak > 1 ? $"x{streak}" : "";
        }

        private void ShowJudgement(bool isHit)
        {
            if (judgementText == null) return;
            judgementText.gameObject.SetActive(true);
            _judgementTimer = judgementDisplaySec;
            judgementText.text = isHit ? "HIT" : "MISS";
            judgementText.color = isHit ? Color.yellow : Color.red;
        }
    }
}
