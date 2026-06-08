using System;
using UnityEngine;

namespace Karaoke.Editor
{
    [Serializable]
    public class PitchRow
    {
        public string label;        // "E4"
        public AudioClip clip;
        public int lane;
    }

    [CreateAssetMenu(fileName = "SongEditorSettings", menuName = "Karaoke/Song Editor Settings")]
    public class SongEditorSettings : ScriptableObject
    {
        [Header("Defaults")]
        public float defaultSpeed    = 5f;
        public float defaultWindowMs = 350f;
        public float snapMs          = 100f;

        [Header("Pitch Rows (top = highest)")]
        public PitchRow[] pitchRows = Array.Empty<PitchRow>();
    }
}
