using System;
using System.Collections.Generic;
using UnityEngine;
using Karaoke.Core;

namespace Karaoke.Tap
{
    public class HitJudge : MonoBehaviour
    {
        public static HitJudge Instance { get; private set; }

        [SerializeField] private NoteSpawner noteSpawner;

        public event Action<HitResult, int> OnJudgement; // result, lane

        private SongPlayer _songPlayer;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            _songPlayer = SongPlayer.Instance;
            if (_songPlayer == null)
                Debug.LogError("[HitJudge] SongPlayer instance not found.");
        }

        // Called by TapInputHandler when a tap is registered for a lane
        public void RegisterTap(int lane)
        {
            if (_songPlayer == null || !_songPlayer.IsPlaying) return;

            float songTimeMs = _songPlayer.SongTimeMs;
            BlobNote closest = FindClosestBlob(lane, songTimeMs);

            if (closest == null)
            {
                // Tap with no blob nearby = empty tap, ignore silently
                return;
            }

            HitResult result = closest.Evaluate(songTimeMs);
            if (result != HitResult.None)
                OnJudgement?.Invoke(result, lane);
        }

        // Called by BlobNote when it passes the hit zone unscathed
        public void RegisterMiss(BlobNote blob)
        {
            OnJudgement?.Invoke(HitResult.Miss, blob.lane);
        }

        private BlobNote FindClosestBlob(int lane, float songTimeMs)
        {
            List<BlobNote> active = noteSpawner.GetActiveBlobs();
            BlobNote best = null;
            float bestDelta = float.MaxValue;

            foreach (var blob in active)
            {
                if (blob.lane != lane) continue;
                float delta = Mathf.Abs(songTimeMs - blob.timeMs);
                if (delta < bestDelta)
                {
                    bestDelta = delta;
                    best = blob;
                }
            }

            // Only return if within the broadest timing window
            return bestDelta <= BlobNote.GoodWindowMs ? best : null;
        }
    }
}
