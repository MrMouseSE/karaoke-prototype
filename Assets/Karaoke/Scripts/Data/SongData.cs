using System;
using UnityEngine;

namespace Karaoke.Data
{
    [Serializable]
    public struct NoteData
    {
        [Tooltip("Время прибытия в хит-зону (мс от старта)")]
        public float timeMs;
        [Tooltip("Дорожка 0-3")]
        public int lane;
        [Tooltip("Скорость блоба (ед/сек)")]
        public float speed;
        [Tooltip("Время ожидания блоба в хит-зоне (мс)")]
        public float tapWindowMs;

        [Header("Audio")]
        [Tooltip("Базовый клип (петля без огибающей, записан на baseMidiNote)")]
        public AudioClip baseClip;
        [Tooltip("MIDI номер базового клипа (36=C2, 38=D2 ...)")]
        public int baseMidiNote;
        [Tooltip("Целевой MIDI номер ноты (60=C4, 67=G4 ...)")]
        public int midiNote;
        [Tooltip("Сколько держать ноту (сек)")]
        public float noteDurationSec;
        [Tooltip("Длительность fade out (сек)")]
        public float fadeSec;

        /// <summary>Питч-множитель для AudioSource.pitch</summary>
        public float Pitch => Mathf.Pow(2f, (midiNote - baseMidiNote) / 12f);
    }

    [CreateAssetMenu(fileName = "NewSong", menuName = "Karaoke/Song Data")]
    public class SongData : ScriptableObject
    {
        [Header("Metadata")]
        public string songTitle = "Untitled";
        public string artist    = "";
        public float  bpm       = 120f;

        [Header("Notes")]
        public NoteData[] notes = Array.Empty<NoteData>();

        public float TotalDurationMs
        {
            get
            {
                float max = 0f;
                foreach (var n in notes)
                {
                    float end = n.timeMs + n.tapWindowMs;
                    if (end > max) max = end;
                }
                return max;
            }
        }
    }
}
