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

        // Hit = игрок тапнул в окне ожидания, Miss = блоб ушёл без тапа
        public event Action<bool, int> OnJudgement; // isHit, lane

        private SongPlayer _songPlayer;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            _songPlayer = SongPlayer.Instance;
        }

        // Вызывается TapInputHandler
        public void RegisterTap(int lane)
        {
            if (_songPlayer == null || !_songPlayer.IsPlaying) return;

            BlobNote blob = FindWaitingBlob(lane);
            if (blob == null) return;

            blob.RegisterTap();
            OnJudgement?.Invoke(true, lane);
        }

        // Вызывается BlobNote при промахе
        public void RegisterMiss(BlobNote blob)
        {
            OnJudgement?.Invoke(false, blob.NoteData.lane);
        }

        private BlobNote FindWaitingBlob(int lane)
        {
            foreach (var blob in noteSpawner.GetActiveBlobs())
            {
                if (blob.NoteData.lane == lane && blob.Phase == BlobPhase.Waiting)
                    return blob;
            }
            return null;
        }
    }
}
