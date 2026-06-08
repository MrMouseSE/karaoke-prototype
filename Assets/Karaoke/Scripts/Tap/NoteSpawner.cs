using System.Collections.Generic;
using UnityEngine;
using Karaoke.Core;
using Karaoke.Data;

namespace Karaoke.Tap
{
    public class NoteSpawner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BlobNote             blobPrefab;
        [SerializeField] private Transform[]          laneSpawnPoints;
        [SerializeField] private Transform[]          laneHitZones;
        [SerializeField] private AccompanimentPlayer  accompanimentPlayer;

        [Header("Pool")]
        [SerializeField] private int poolSizePerLane = 8;

        private SongPlayer       _songPlayer;
        private NoteData[]       _notes;
        private int              _nextIndex;
        private Queue<BlobNote>[] _pools;
        private List<BlobNote>   _active;

        private void Awake()
        {
            int lanes = laneSpawnPoints.Length;
            _pools  = new Queue<BlobNote>[lanes];
            _active = new List<BlobNote>();

            for (int l = 0; l < lanes; l++)
            {
                _pools[l] = new Queue<BlobNote>();
                for (int i = 0; i < poolSizePerLane; i++)
                {
                    var blob = Instantiate(blobPrefab, laneSpawnPoints[l].position, Quaternion.identity, transform);
                    blob.gameObject.SetActive(false);
                    _pools[l].Enqueue(blob);
                }
            }
        }

        private void OnEnable()  => BlobNote.OnNoteHit += OnNoteHit;
        private void OnDisable() => BlobNote.OnNoteHit -= OnNoteHit;

        private void Start()
        {
            _songPlayer = SongPlayer.Instance;
            if (_songPlayer == null) { Debug.LogError("[NoteSpawner] SongPlayer not found."); return; }
            _songPlayer.OnSongStarted += OnSongStarted;
            _songPlayer.OnSongFinished += OnSongFinished;
            _songPlayer.OnTick += OnTick;
        }

        private void OnDestroy()
        {
            if (_songPlayer == null) return;
            _songPlayer.OnSongStarted  -= OnSongStarted;
            _songPlayer.OnSongFinished -= OnSongFinished;
            _songPlayer.OnTick         -= OnTick;
        }

        private void OnSongStarted()
        {
            _notes = _songPlayer.SongData.notes;
            _nextIndex = 0;
            ReturnAllToPool();
        }

        private void OnSongFinished() => ReturnAllToPool();

        private void OnTick(float deltaMs)
        {
            if (_notes == null) return;

            float songTimeSec = _songPlayer.SongTimeMs / 1000f;

            while (_nextIndex < _notes.Length)
            {
                var note = _notes[_nextIndex];
                int lane = Mathf.Clamp(note.lane, 0, laneSpawnPoints.Length - 1);
                float dist      = Vector3.Distance(laneSpawnPoints[lane].position, laneHitZones[lane].position);
                float spawnAtSec = note.timeMs / 1000f - dist / note.speed;

                if (songTimeSec >= spawnAtSec)
                {
                    SpawnBlob(note, lane, songTimeSec);
                    _nextIndex++;
                }
                else break;
            }
        }

        private void SpawnBlob(NoteData note, int lane, float songTimeSec)
        {
            if (_pools[lane].Count == 0) { Debug.LogWarning($"[NoteSpawner] Pool empty lane {lane}."); return; }

            float dist       = Vector3.Distance(laneSpawnPoints[lane].position, laneHitZones[lane].position);
            float arrivalTime = Time.time + (note.timeMs / 1000f - songTimeSec);

            var blob = _pools[lane].Dequeue();
            blob.transform.position = laneSpawnPoints[lane].position;
            blob.gameObject.SetActive(true);
            blob.Init(note, laneHitZones[lane], arrivalTime);
            _active.Add(blob);
        }

        private void OnNoteHit(AudioClip clip, float pitch, float durationSec, float fadeSec)
            => accompanimentPlayer?.PlayNote(clip, pitch, durationSec, fadeSec);

        public List<BlobNote> GetActiveBlobs() => _active;

        private void ReturnAllToPool()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var blob = _active[i];
                int lane = Mathf.Clamp(blob.NoteData.lane, 0, _pools.Length - 1);
                blob.gameObject.SetActive(false);
                _pools[lane].Enqueue(blob);
            }
            _active.Clear();
        }
    }
}
