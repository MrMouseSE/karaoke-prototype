using System;
using UnityEngine;
using Karaoke.Data;

namespace Karaoke.Core
{
    public class SongPlayer : MonoBehaviour
    {
        [SerializeField] private SongData songData;

        public static SongPlayer Instance { get; private set; }

        public SongData SongData => songData;
        public float SongTimeMs { get; private set; }
        public bool IsPlaying { get; private set; }

        public event Action OnSongStarted;
        public event Action OnSongFinished;
        public event Action<float> OnTick; // deltaTime in ms

        private float _totalDurationMs;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void SetSong(SongData data)
        {
            if (IsPlaying) Stop();
            songData = data;
        }

        public void Play()
        {
            if (songData == null)
            {
                Debug.LogWarning("[SongPlayer] No SongData assigned.");
                return;
            }
            SongTimeMs = 0f;
            _totalDurationMs = songData.TotalDurationMs;
            IsPlaying = true;
            OnSongStarted?.Invoke();
        }

        public void Stop()
        {
            IsPlaying = false;
            SongTimeMs = 0f;
        }

        public void Pause() => IsPlaying = false;
        public void Resume() => IsPlaying = true;

        private void Update()
        {
            if (!IsPlaying) return;

            float deltaMs = Time.deltaTime * 1000f;
            SongTimeMs += deltaMs;
            OnTick?.Invoke(deltaMs);

            if (SongTimeMs >= _totalDurationMs)
            {
                SongTimeMs = _totalDurationMs;
                IsPlaying = false;
                OnSongFinished?.Invoke();
            }
        }
    }
}
