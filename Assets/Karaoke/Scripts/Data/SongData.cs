using System;
using UnityEngine;

namespace Karaoke.Data
{
    [Serializable]
    public struct NoteData
    {
        [Tooltip("Момент прибытия блоба в хит-зону (мс от старта)")]
        public float timeMs;
        [Tooltip("Дорожка 0-3")]
        public int lane;
        [Tooltip("Скорость перемещения блоба (ед/сек)")]
        public float speed;
        [Tooltip("Время ожидания блоба в хит-зоне (мс)")]
        public float tapWindowMs;
        [Tooltip("Звук, проигрываемый при тапе")]
        public AudioClip clip;
    }

    [CreateAssetMenu(fileName = "NewSong", menuName = "Karaoke/Song Data")]
    public class SongData : ScriptableObject
    {
        [Header("Metadata")]
        public string songTitle = "Untitled";
        public string artist = "";
        public float bpm = 120f;

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
