using System.Collections.Generic;
using UnityEngine;
using Karaoke.Core;
using Karaoke.Data;

namespace Karaoke.Tap
{
    public class NoteSpawner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BlobNote blobPrefab;
        [Tooltip("Parent transforms for each lane (index = lane number)")]
        [SerializeField] private Transform[] laneRoots;

        [Header("Timing")]
        [Tooltip("How far ahead (ms) blobs are spawned before their hit time")]
        [SerializeField] private float lookAheadMs = 1500f;
        [Tooltip("Time (sec) the blob travels from top to hit zone")]
        [SerializeField] private float travelDurationSec = 1.5f;

        [Header("Pool")]
        [SerializeField] private int poolSizePerLane = 8;

        private SongPlayer _songPlayer;
        private TapEvent[] _tapEvents;
        private int _nextEventIndex;

        private Queue<BlobNote>[] _pools;
        private List<BlobNote> _active;

        private void Awake()
        {
            int laneCount = laneRoots.Length;
            _pools = new Queue<BlobNote>[laneCount];
            _active = new List<BlobNote>();

            for (int l = 0; l < laneCount; l++)
            {
                _pools[l] = new Queue<BlobNote>();
                for (int i = 0; i < poolSizePerLane; i++)
                {
                    var blob = Instantiate(blobPrefab, laneRoots[l]);
                    blob.gameObject.SetActive(false);
                    _pools[l].Enqueue(blob);
                }
            }
        }

        private void Start()
        {
            _songPlayer = SongPlayer.Instance;
            if (_songPlayer == null)
            {
                Debug.LogError("[NoteSpawner] SongPlayer instance not found.");
                return;
            }
            _songPlayer.OnSongStarted += OnSongStarted;
            _songPlayer.OnSongFinished += OnSongFinished;
            _songPlayer.OnTick += OnTick;
        }

        private void OnDestroy()
        {
            if (_songPlayer == null) return;
            _songPlayer.OnSongStarted -= OnSongStarted;
            _songPlayer.OnSongFinished -= OnSongFinished;
            _songPlayer.OnTick -= OnTick;
        }

        private void OnSongStarted()
        {
            _tapEvents = _songPlayer.SongData.tapEvents;
            _nextEventIndex = 0;
            ReturnAllToPool();
        }

        private void OnSongFinished() => ReturnAllToPool();

        private void OnTick(float deltaMs)
        {
            if (_tapEvents == null) return;

            float currentMs = _songPlayer.SongTimeMs;
            float spawnUntil = currentMs + lookAheadMs;

            while (_nextEventIndex < _tapEvents.Length &&
                   _tapEvents[_nextEventIndex].timeMs <= spawnUntil)
            {
                SpawnBlob(_tapEvents[_nextEventIndex]);
                _nextEventIndex++;
            }
        }

        private void SpawnBlob(TapEvent ev)
        {
            int lane = Mathf.Clamp(ev.lane, 0, laneRoots.Length - 1);
            if (_pools[lane].Count == 0)
            {
                Debug.LogWarning($"[NoteSpawner] Pool empty for lane {lane}.");
                return;
            }

            var blob = _pools[lane].Dequeue();
            blob.gameObject.SetActive(true);
            blob.Init(ev.timeMs, lane, travelDurationSec);
            _active.Add(blob);
        }

        // Called by HitJudge or BlobNote itself when blob is done
        public void ReturnToPool(BlobNote blob)
        {
            int lane = Mathf.Clamp(blob.lane, 0, _pools.Length - 1);
            _active.Remove(blob);
            blob.gameObject.SetActive(false);
            _pools[lane].Enqueue(blob);
        }

        private void ReturnAllToPool()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var blob = _active[i];
                int lane = Mathf.Clamp(blob.lane, 0, _pools.Length - 1);
                blob.gameObject.SetActive(false);
                _pools[lane].Enqueue(blob);
            }
            _active.Clear();
        }

        public List<BlobNote> GetActiveBlobs() => _active;
    }
}
