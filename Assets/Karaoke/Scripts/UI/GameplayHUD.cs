using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Karaoke.Core;
using Karaoke.Tap;

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
            {
                _songPlayer.OnSongStarted += OnSongStarted;
            }

            if (judgementText != null)
                judgementText.gameObject.SetActive(false);

            UpdateScoreUI(0, 0);
        }

        private void OnDestroy()
        {
            if (_scoreManager != null)
            {
                _scoreManager.OnScoreChanged -= UpdateScoreUI;
                _scoreManager.OnNoteJudged -= ShowJudgement;
            }
            if (_songPlayer != null)
                _songPlayer.OnSongStarted -= OnSongStarted;
        }

        private void OnSongStarted()
        {
            _songDurationMs = _songPlayer.SongData.TotalDurationMs;
            if (progressBar != null)
            {
                progressBar.minValue = 0f;
                progressBar.maxValue = 1f;
                progressBar.value = 0f;
            }
        }

        private void Update()
        {
            UpdateProgress();
            TickJudgementDisplay();
        }

        private void UpdateProgress()
        {
            if (_songPlayer == null || !_songPlayer.IsPlaying || _songDurationMs <= 0f) return;
            if (progressBar != null)
                progressBar.value = _songPlayer.SongTimeMs / _songDurationMs;
        }

        private void UpdateScoreUI(int score, int streak)
        {
            if (scoreText != null)
                scoreText.text = score.ToString("N0");

            if (streakText != null)
                streakText.text = streak > 1 ? $"x{streak}" : "";
        }

        private void ShowJudgement(HitResult result)
        {
            if (judgementText == null) return;

            judgementText.gameObject.SetActive(true);
            _judgementTimer = judgementDisplaySec;

            switch (result)
            {
                case HitResult.Perfect:
                    judgementText.text = "PERFECT";
                    judgementText.color = Color.yellow;
                    break;
                case HitResult.Good:
                    judgementText.text = "GOOD";
                    judgementText.color = Color.green;
                    break;
                case HitResult.Miss:
                    judgementText.text = "MISS";
                    judgementText.color = Color.red;
                    break;
            }
        }

        private void TickJudgementDisplay()
        {
            if (judgementText == null || !judgementText.gameObject.activeSelf) return;
            _judgementTimer -= Time.deltaTime;
            if (_judgementTimer <= 0f)
                judgementText.gameObject.SetActive(false);
        }
    }
}
