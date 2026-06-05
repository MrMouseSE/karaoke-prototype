using System;
using UnityEngine;
using Karaoke.Tap;

namespace Karaoke.Core
{
    public class ScoreManager : MonoBehaviour
    {
        public static ScoreManager Instance { get; private set; }

        [Header("Score Values")]
        [SerializeField] private int perfectScore = 300;
        [SerializeField] private int goodScore = 100;
        [SerializeField] private int streakBonusEvery = 10;
        [SerializeField] private int streakBonusAmount = 500;

        public int Score { get; private set; }
        public int Streak { get; private set; }
        public int MaxStreak { get; private set; }
        public int TotalNotes { get; private set; }
        public int HitNotes { get; private set; }
        public int PerfectCount { get; private set; }
        public int GoodCount { get; private set; }
        public int MissCount { get; private set; }

        public float Accuracy => TotalNotes == 0 ? 0f : (float)HitNotes / TotalNotes;

        public string Grade
        {
            get
            {
                float acc = Accuracy;
                if (acc >= 0.95f) return "S";
                if (acc >= 0.85f) return "A";
                if (acc >= 0.70f) return "B";
                if (acc >= 0.50f) return "C";
                return "D";
            }
        }

        public event Action<int, int> OnScoreChanged;    // score, streak
        public event Action<HitResult> OnNoteJudged;

        private HitJudge _hitJudge;
        private SongPlayer _songPlayer;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            _songPlayer = SongPlayer.Instance;
            _hitJudge = HitJudge.Instance;

            if (_songPlayer != null)
                _songPlayer.OnSongStarted += ResetStats;

            if (_hitJudge != null)
                _hitJudge.OnJudgement += OnJudgement;
            else
                Debug.LogError("[ScoreManager] HitJudge instance not found.");
        }

        private void OnDestroy()
        {
            if (_songPlayer != null) _songPlayer.OnSongStarted -= ResetStats;
            if (_hitJudge != null) _hitJudge.OnJudgement -= OnJudgement;
        }

        private void ResetStats()
        {
            Score = 0;
            Streak = 0;
            MaxStreak = 0;
            TotalNotes = 0;
            HitNotes = 0;
            PerfectCount = 0;
            GoodCount = 0;
            MissCount = 0;
            OnScoreChanged?.Invoke(Score, Streak);
        }

        private void OnJudgement(HitResult result, int lane)
        {
            TotalNotes++;
            OnNoteJudged?.Invoke(result);

            switch (result)
            {
                case HitResult.Perfect:
                    PerfectCount++;
                    HitNotes++;
                    AddScore(perfectScore);
                    IncreaseStreak();
                    break;
                case HitResult.Good:
                    GoodCount++;
                    HitNotes++;
                    AddScore(goodScore);
                    IncreaseStreak();
                    break;
                case HitResult.Miss:
                    MissCount++;
                    Streak = 0;
                    OnScoreChanged?.Invoke(Score, Streak);
                    break;
            }
        }

        private void AddScore(int amount)
        {
            Score += amount;
            OnScoreChanged?.Invoke(Score, Streak);
        }

        private void IncreaseStreak()
        {
            Streak++;
            if (Streak > MaxStreak) MaxStreak = Streak;

            if (Streak % streakBonusEvery == 0)
                AddScore(streakBonusAmount);
        }
    }
}
