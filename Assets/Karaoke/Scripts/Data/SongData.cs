using System;
using UnityEngine;

namespace Karaoke.Data
{
    [Serializable]
    public struct TapEvent
    {
        [Tooltip("Time in milliseconds from song start")]
        public float timeMs;
        [Tooltip("Lane index 0-3 (left to right)")]
        public int lane;
    }

    [Serializable]
    public struct NoteEvent
    {
        [Tooltip("Time in milliseconds from song start")]
        public float timeMs;
        [Tooltip("Duration in milliseconds")]
        public float durationMs;
        [Tooltip("Frequency in Hz (e.g. A4 = 440)")]
        public float frequencyHz;
    }

    [Serializable]
    public struct AccompanimentNote
    {
        [Tooltip("Time in milliseconds from song start")]
        public float timeMs;
        [Tooltip("Duration in milliseconds")]
        public float durationMs;
        [Tooltip("MIDI note number (60 = Middle C)")]
        public int midiNote;
        [Tooltip("Instrument clip to play (pitch-shifted)")]
        public AudioClip instrumentClip;
    }

    [CreateAssetMenu(fileName = "NewSong", menuName = "Karaoke/Song Data")]
    public class SongData : ScriptableObject
    {
        [Header("Metadata")]
        public string songTitle = "Untitled";
        public string artist = "";
        [Tooltip("Beats per minute")]
        public float bpm = 120f;

        [Header("Events")]
        public TapEvent[] tapEvents = Array.Empty<TapEvent>();
        public NoteEvent[] noteEvents = Array.Empty<NoteEvent>();
        public AccompanimentNote[] accompaniment = Array.Empty<AccompanimentNote>();

        public float TotalDurationMs
        {
            get
            {
                float max = 0f;
                foreach (var t in tapEvents)
                    if (t.timeMs > max) max = t.timeMs;
                foreach (var n in noteEvents)
                    if (n.timeMs + n.durationMs > max) max = n.timeMs + n.durationMs;
                foreach (var a in accompaniment)
                    if (a.timeMs + a.durationMs > max) max = a.timeMs + a.durationMs;
                return max;
            }
        }
    }
}
