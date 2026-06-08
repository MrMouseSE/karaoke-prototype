using System;
using UnityEngine;

namespace Karaoke.Editor
{
    [Serializable]
    public class NoteClipEntry
    {
        [Tooltip("Название ноты (C, D, E, F, G, A, B)")]
        public string   noteName;
        [Tooltip("MIDI номер этого клипа (36=C2, 38=D2 ...)")]
        public int      midiNote;
        [Tooltip("Петлевой WAV большой октавы")]
        public AudioClip clip;
    }

    [CreateAssetMenu(fileName = "SongEditorSettings", menuName = "Karaoke/Song Editor Settings")]
    public class SongEditorSettings : ScriptableObject
    {
        [Header("Base clips (большая октава C2–B2)")]
        public NoteClipEntry[] baseClips = Array.Empty<NoteClipEntry>();

        [Header("Octave range")]
        public int minOctave = 2;
        public int maxOctave = 5;

        [Header("Defaults")]
        public float defaultSpeed           = 5f;
        public float defaultWindowMs        = 350f;
        public float defaultNoteDurationSec = 0.4f;
        public float defaultFadeSec         = 0.15f;
        public float snapMs                 = 100f;

        [Header("Visual")]
        [Range(10f, 40f)]
        public float rowHeight = 16f;
    }
}
