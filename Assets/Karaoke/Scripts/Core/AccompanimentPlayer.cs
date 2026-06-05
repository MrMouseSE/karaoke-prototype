using System.Collections.Generic;
using UnityEngine;
using Karaoke.Data;

namespace Karaoke.Core
{
    // Plays accompaniment notes by pitch-shifting instrument AudioClips.
    // MIDI note 69 = A4 = 440 Hz (reference pitch for clips recorded at A4).
    public class AccompanimentPlayer : MonoBehaviour
    {
        [SerializeField] private int poolSize = 16;
        [Tooltip("How many ms before note time to schedule playback")]
        [SerializeField] private float lookAheadMs = 100f;

        private SongPlayer _songPlayer;
        private SongData _songData;
        private int _nextNoteIndex;
        private Queue<AudioSource> _pool;
        private List<AudioSource> _active;

        private const int ReferenceMidiNote = 69; // A4

        private void Awake()
        {
            _pool = new Queue<AudioSource>();
            _active = new List<AudioSource>();

            for (int i = 0; i < poolSize; i++)
            {
                var go = new GameObject($"AccompSource_{i}");
                go.transform.SetParent(transform);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                _pool.Enqueue(src);
            }
        }

        private void Start()
        {
            _songPlayer = SongPlayer.Instance;
            if (_songPlayer == null)
            {
                Debug.LogError("[AccompanimentPlayer] SongPlayer instance not found.");
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
            _songData = _songPlayer.SongData;
            _nextNoteIndex = 0;
            StopAllNotes();
        }

        private void OnSongFinished() => StopAllNotes();

        private void OnTick(float deltaMs)
        {
            if (_songData == null) return;

            float currentMs = _songPlayer.SongTimeMs;
            float scheduleUntil = currentMs + lookAheadMs;

            var notes = _songData.accompaniment;
            while (_nextNoteIndex < notes.Length && notes[_nextNoteIndex].timeMs <= scheduleUntil)
            {
                ScheduleNote(notes[_nextNoteIndex], currentMs);
                _nextNoteIndex++;
            }

            // Return finished sources to pool
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (!_active[i].isPlaying)
                {
                    _pool.Enqueue(_active[i]);
                    _active.RemoveAt(i);
                }
            }
        }

        private void ScheduleNote(AccompanimentNote note, float currentMs)
        {
            if (note.instrumentClip == null) return;
            if (_pool.Count == 0) return;

            var src = _pool.Dequeue();
            _active.Add(src);

            src.clip = note.instrumentClip;
            src.pitch = MidiNoteToPitchMultiplier(note.midiNote);

            float delayMs = note.timeMs - currentMs;
            float delaySec = Mathf.Max(0f, delayMs / 1000f);
            src.PlayDelayed(delaySec);
        }

        private static float MidiNoteToPitchMultiplier(int midiNote)
        {
            int semitones = midiNote - ReferenceMidiNote;
            return Mathf.Pow(2f, semitones / 12f);
        }

        private void StopAllNotes()
        {
            foreach (var src in _active)
            {
                src.Stop();
                _pool.Enqueue(src);
            }
            _active.Clear();
        }
    }
}
