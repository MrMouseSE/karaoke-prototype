using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Karaoke.Data;

namespace Karaoke.Editor
{
    public class SongEditorWindow : EditorWindow
    {
        // ── Ссылки ───────────────────────────────────────────────────
        private SongData           _song;
        private SongEditorSettings _settings;
        private TimelineView       _tl;
        private List<NoteHandle>   _handles = new();

        // ── Воспроизведение ──────────────────────────────────────────
        private bool   _isPlaying     = false;
        private double _playStartTime = 0;
        private float  _playheadMs    = 0f;
        private int    _nextNoteIdx   = 0;

        private static System.Reflection.MethodInfo _playClipMethod;
        private static System.Reflection.MethodInfo _stopAllMethod;
        
        // Кэш питч-шифтированных клипов для превью в редакторе
        private static Dictionary<(AudioClip, int), AudioClip> _pitchedCache = new();
private static AudioClip _lastPlayedClip;

        private static void InitAudioUtil()
        {
            if (_playClipMethod != null) return;
            var t = System.Type.GetType("UnityEditor.AudioUtil,UnityEditor");
            if (t == null) return;
            var f = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public;
            _playClipMethod = t.GetMethod("PlayPreviewClip", f, null,
                new System.Type[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
            _stopAllMethod  = t.GetMethod("StopAllPreviewClips", f);
        }

        private static void PlayClip(AudioClip clip)
        {
            if (clip == null) return;
            InitAudioUtil();
            if (clip == _lastPlayedClip) _stopAllMethod?.Invoke(null, null);
            _lastPlayedClip = clip;
            _playClipMethod?.Invoke(null, new object[] { clip, 0, false });
        }

        private static void StopAllClips()
        {
            InitAudioUtil();
            _stopAllMethod?.Invoke(null, null);
        }

private static AudioClip GetPitchedClip(AudioClip baseClip, float pitch) { if (baseClip == null) return null; var key = (baseClip, Mathf.RoundToInt(pitch * 1000)); if (_pitchedCache.TryGetValue(key, out var cached) && cached != null) return cached; var data = new float[baseClip.samples * baseClip.channels]; baseClip.GetData(data, 0); int newLen = Mathf.Max(1, Mathf.RoundToInt(data.Length / pitch)); var newData = new float[newLen]; for (int i = 0; i < newLen; i++) { float srcF = i * pitch; int s0 = (int)srcF; int s1 = Mathf.Min(s0 + 1, data.Length - 1); newData[i] = Mathf.Lerp(data[s0], data[s1], srcF - s0); } var clip = AudioClip.Create($"pitched_{pitch:F3}", newLen, baseClip.channels, baseClip.frequency, false); clip.SetData(newData, 0); _pitchedCache[key] = clip; return clip; }


        // ── Состояние drag ───────────────────────────────────────────
        private int     _selected    = -1;
        private int     _dragging    = -1;
        private bool    _resizing    = false;
        private Vector2 _dragStart;
        private float   _dragOrigMs;
        private float   _dragOrigWin;
        private int     _dragOrigRow;

        // ── Константы ────────────────────────────────────────────────
        private const float  ToolbarH    = 44f;
        private const float  InspectorH  = 120f;
        private const string SettingsPath = "Assets/Karaoke/Editor/SongEditorSettings.asset";

        [MenuItem("Karaoke/Song Editor")]
        public static void Open() => GetWindow<SongEditorWindow>("Song Editor");

        private void OnEnable()
        {
            _tl = new TimelineView();
            LoadOrCreateSettings();
            RebuildHandles();
            wantsMouseMove = true;
        }

        private void OnDisable() => StopPlayback();

        // ── Settings ─────────────────────────────────────────────────
        private void LoadOrCreateSettings()
        {
            _settings = AssetDatabase.LoadAssetAtPath<SongEditorSettings>(SettingsPath);
            if (_settings != null) return;
            System.IO.Directory.CreateDirectory("Assets/Karaoke/Editor");
            _settings = CreateInstance<SongEditorSettings>();
            AssetDatabase.CreateAsset(_settings, SettingsPath);
            AssetDatabase.SaveAssets();
        }

        // ── GUI ──────────────────────────────────────────────────────
        private void OnGUI()
        {
            DrawToolbar();
            if (_song == null) { EditorGUILayout.HelpBox("Выберите SongData.", MessageType.Info); return; }

            SyncTimeline();
            float timelineH = position.height - ToolbarH - InspectorH;
            _tl.area = new Rect(0, ToolbarH, position.width, timelineH);

            DrawTimeline();
            DrawInspector();
            HandleEvents();
        }

        // ── Toolbar ──────────────────────────────────────────────────
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(ToolbarH));

            EditorGUI.BeginChangeCheck();
            var newSong = (SongData)EditorGUILayout.ObjectField("Song", _song, typeof(SongData), false, GUILayout.Width(260));
            if (EditorGUI.EndChangeCheck()) { _song = newSong; _selected = -1; _playheadMs = 0f; RebuildHandles(); }

            GUILayout.Space(6);
            GUILayout.Label("Zoom", GUILayout.Width(34));
            _tl.PixelsPerMs = GUILayout.HorizontalSlider(_tl.PixelsPerMs, 0.02f, 0.8f, GUILayout.Width(70));

            GUILayout.Space(6);
            if (_settings != null)
            {
                GUILayout.Label("Row H", GUILayout.Width(38));
                EditorGUI.BeginChangeCheck();
                float newH = GUILayout.HorizontalSlider(_settings.rowHeight, 10f, 40f, GUILayout.Width(60));
                if (EditorGUI.EndChangeCheck()) { _settings.rowHeight = newH; EditorUtility.SetDirty(_settings); }

                GUILayout.Space(6);
                GUILayout.Label("Snap", GUILayout.Width(30));
                _settings.snapMs = EditorGUILayout.FloatField(_settings.snapMs, GUILayout.Width(40));
            }

            GUILayout.Space(10);
            GUI.color = _isPlaying ? Color.red : Color.green;
            if (GUILayout.Button(_isPlaying ? "■ Stop" : "▶ Play", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                if (_isPlaying) StopPlayback(); else StartPlayback();
            }
            GUI.color = Color.white;
            GUILayout.Label($"{_playheadMs / 1000f:F2}s", GUILayout.Width(44));

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("+ Note", EditorStyles.toolbarButton, GUILayout.Width(52)))
                AddNoteAtPlayhead();

            GUI.color = new Color(1f, 0.5f, 0.5f);
            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(46)))
                ClearTrack();
            GUI.color = Color.white;

            if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(40)))
                Save();

            EditorGUILayout.EndHorizontal();
        }

        // ── Timeline ─────────────────────────────────────────────────
        private void SyncTimeline()
        {
            _tl.RowHeight = _settings?.rowHeight ?? 16f;
            _tl.RowCount  = TotalRowCount();
        }

        private int TotalRowCount()
        {
            if (_settings == null || _settings.baseClips.Length == 0) return 7;
            return (_settings.maxOctave - _settings.minOctave + 1) * _settings.baseClips.Length;
        }

        private void DrawTimeline()
        {
            float dur = _song != null ? Mathf.Max(_song.TotalDurationMs, 5000f) : 5000f;
            _tl.DrawGrid(dur);
            DrawRowLabels();
            DrawNotes();
            DrawPlayhead();

            float contentW = _tl.MsToPixels(dur + 2000f) + _tl.labelWidth;
            float scrollPx = GUI.HorizontalScrollbar(
                new Rect(0, _tl.area.yMax - 14f, position.width, 14f),
                _tl.scrollMs * _tl.PixelsPerMs, position.width, 0f, contentW);
            if (!_isPlaying) _tl.scrollMs = Mathf.Max(0f, scrollPx / _tl.PixelsPerMs);
        }

        private void DrawRowLabels()
        {
            if (_settings == null || _settings.baseClips.Length == 0) return;
            int noteCount = _settings.baseClips.Length;
            int row = 0;
            for (int oct = _settings.maxOctave; oct >= _settings.minOctave; oct--)
            {
                for (int n = 0; n < noteCount; n++, row++)
                {
                    string label = _settings.baseClips[n].noteName + oct;
                    float  y     = _tl.RowToY(row);

                    // Чередуем фон октав
                    Color bg = oct % 2 == 0
                        ? new Color(0.17f, 0.17f, 0.22f)
                        : new Color(0.19f, 0.19f, 0.19f);
                    EditorGUI.DrawRect(new Rect(_tl.area.x, y, _tl.labelWidth, _tl.RowHeight), bg);

                    GUI.Label(new Rect(_tl.area.x, y, _tl.labelWidth - 2f, _tl.RowHeight),
                        label,
                        new GUIStyle(GUI.skin.label)
                        {
                            fontSize  = 9,
                            alignment = TextAnchor.MiddleRight,
                            normal    = { textColor = new Color(0.8f, 0.8f, 0.8f) }
                        });
                }
            }
        }

        private void DrawNotes()
        {
            if (_song == null) return;
            for (int i = 0; i < _handles.Count; i++)
            {
                var h    = _handles[i];
                var note = _song.notes[h.index];
                int row  = MidiToRow(note.midiNote);
                h.UpdateRect(note, _tl, row);

                bool  sel    = i == _selected;
                Color body   = sel ? new Color(1f, 0.85f, 0.2f) : new Color(0.3f, 0.7f, 1f);

                EditorGUI.DrawRect(h.rect,         body);
                EditorGUI.DrawRect(h.resizeHandle, new Color(1f, 1f, 1f, 0.5f));

                GUI.Label(
                    new Rect(h.rect.x + 2, h.rect.y, h.rect.width - 10, h.rect.height),
                    MidiToLabel(note.midiNote),
                    new GUIStyle(GUI.skin.label) { fontSize = 9, alignment = TextAnchor.MiddleLeft, normal = { textColor = Color.black } });
            }
        }

        private void DrawPlayhead()
        {
            float px = _tl.TimeToPixel(_playheadMs);
            if (px < _tl.area.x + _tl.labelWidth || px > _tl.area.xMax) return;
            EditorGUI.DrawRect(new Rect(px - 1f, _tl.area.y, 2f, _tl.TotalHeight), new Color(1f, 1f, 0f, 0.9f));
            GUI.Label(new Rect(px + 2f, _tl.area.y, 50f, 14f), $"{_playheadMs / 1000f:F2}s",
                new GUIStyle(GUI.skin.label) { fontSize = 9, normal = { textColor = Color.yellow } });
        }

        // ── Inspector ────────────────────────────────────────────────
        private void DrawInspector()
        {
            float y = position.height - InspectorH;
            EditorGUI.DrawRect(new Rect(0, y, position.width, InspectorH), new Color(0.2f, 0.2f, 0.2f));
            GUILayout.BeginArea(new Rect(8, y + 6, position.width - 16, InspectorH - 8));

            if (_selected < 0 || _selected >= _handles.Count)
            {
                GUILayout.Label("Выберите ноту", EditorStyles.boldLabel);
                GUILayout.EndArea(); return;
            }

            int idx  = _handles[_selected].index;
            var note = _song.notes[idx];
            Undo.RecordObject(_song, "Edit Note");

            // Строка 1
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(MidiToLabel(note.midiNote), EditorStyles.boldLabel, GUILayout.Width(40));
            GUILayout.Label("Time (ms)", GUILayout.Width(58));
            note.timeMs = EditorGUILayout.FloatField(note.timeMs, GUILayout.Width(64));
            GUILayout.Space(6);
            GUILayout.Label("Окно тапа (ms)", GUILayout.Width(90));
            note.tapWindowMs = EditorGUILayout.FloatField(note.tapWindowMs, GUILayout.Width(52));
            GUILayout.Space(6);
            GUILayout.Label("Скорость", GUILayout.Width(56));
            note.speed = EditorGUILayout.FloatField(note.speed, GUILayout.Width(36));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Delete", GUILayout.Width(54)))
            {
                DeleteNote(idx);
                GUILayout.EndHorizontal(); GUILayout.EndArea(); return;
            }
            EditorGUILayout.EndHorizontal();

            // Строка 2
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("MIDI", GUILayout.Width(30));
            note.midiNote = EditorGUILayout.IntField(note.midiNote, GUILayout.Width(36));
            GUILayout.Space(6);
            GUILayout.Label("Duration (s)", GUILayout.Width(76));
            note.noteDurationSec = EditorGUILayout.FloatField(note.noteDurationSec, GUILayout.Width(44));
            GUILayout.Space(6);
            GUILayout.Label("Fade (s)", GUILayout.Width(52));
            note.fadeSec = EditorGUILayout.FloatField(note.fadeSec, GUILayout.Width(36));
            GUILayout.Space(6);
            GUILayout.Label("Lane", GUILayout.Width(28));
            note.lane = EditorGUILayout.IntField(note.lane, GUILayout.Width(24));
            GUILayout.EndHorizontal();

            _song.notes[idx] = note;
            EditorUtility.SetDirty(_song);
            GUILayout.EndArea();
        }

        // ── Events ───────────────────────────────────────────────────
        private void HandleEvents()
        {
            var e = Event.current;
            if (e == null || !_tl.area.Contains(e.mousePosition)) return;

            if (e.type == EventType.ScrollWheel)
            {
                if (e.control) _tl.PixelsPerMs = Mathf.Clamp(_tl.PixelsPerMs - e.delta.y * 0.005f, 0.02f, 0.8f);
                else           _tl.scrollMs    = Mathf.Max(0f, _tl.scrollMs + e.delta.y * 30f);
                e.Use(); Repaint(); return;
            }

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                int  hit = -1; bool hitResize = false;
                for (int i = _handles.Count - 1; i >= 0; i--)
                {
                    if (_handles[i].ResizeHitTest(e.mousePosition)) { hit = i; hitResize = true; break; }
                    if (_handles[i].HitTest(e.mousePosition))       { hit = i; break; }
                }
                if (hit >= 0)
                {
                    _selected = hit; _dragging = hit; _resizing = hitResize;
                    _dragStart = e.mousePosition;
                    var n = _song.notes[_handles[hit].index];
                    _dragOrigMs = n.timeMs; _dragOrigWin = n.tapWindowMs;
                    _dragOrigRow = MidiToRow(n.midiNote);
                    e.Use();
                }
                else if (e.clickCount == 2) { AddNoteAt(e.mousePosition); e.Use(); }
                else { _selected = -1; }
                Repaint();
            }

            if (e.type == EventType.MouseDrag && _dragging >= 0)
            {
                Vector2 delta = e.mousePosition - _dragStart;
                int idx = _handles[_dragging].index;
                var note = _song.notes[idx];
                Undo.RecordObject(_song, "Move Note");

                if (_resizing)
                {
                    note.tapWindowMs = Mathf.Max(50f, _dragOrigWin + _tl.PixelsToMs(delta.x));
                }
                else
                {
                    note.timeMs = Mathf.Max(0f, _tl.Snap(_dragOrigMs + _tl.PixelsToMs(delta.x), _settings?.snapMs ?? 0f));
                    int newRow  = Mathf.Clamp(_dragOrigRow + Mathf.RoundToInt(delta.y / _tl.RowHeight), 0, _tl.RowCount - 1);
                    note.midiNote = RowToMidi(newRow);
                    ApplyBaseClip(ref note);
                }

                _song.notes[idx] = note;
                EditorUtility.SetDirty(_song);
                e.Use(); Repaint();
            }

            if (e.type == EventType.MouseUp && _dragging >= 0) { _dragging = -1; _resizing = false; e.Use(); }

            if (e.type == EventType.KeyDown && _selected >= 0
                && (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace))
            { DeleteNote(_handles[_selected].index); e.Use(); }
        }

        // ── Playback ─────────────────────────────────────────────────
        private void StartPlayback()
        {
            if (_song == null) return;
            _playheadMs    = 0f;
            _playStartTime = EditorApplication.timeSinceStartup;
            _nextNoteIdx   = 0;
            _isPlaying     = true;
            EditorApplication.update += OnEditorUpdate;
        }

        private void StopPlayback()
        {
            _isPlaying = false;
            EditorApplication.update -= OnEditorUpdate;
            StopAllClips();
            Repaint();
        }

private void OnEditorUpdate() { if (!_isPlaying || _song == null) { StopPlayback(); return; } _playheadMs = (float)(EditorApplication.timeSinceStartup - _playStartTime) * 1000f; if (_playheadMs >= _song.TotalDurationMs) { _playheadMs = _song.TotalDurationMs; StopPlayback(); return; } var notes = _song.notes; while (_nextNoteIdx < notes.Length && notes[_nextNoteIdx].timeMs <= _playheadMs) { var n = notes[_nextNoteIdx++]; PlayClip(GetPitchedClip(n.baseClip, n.Pitch)); } _tl.scrollMs = Mathf.Max(0f, _playheadMs - (_tl.area.width - _tl.labelWidth) / _tl.PixelsPerMs * 0.5f); Repaint(); }

        // ── Helpers ──────────────────────────────────────────────────
        private void RebuildHandles()
        {
            _handles.Clear();
            if (_song == null) return;
            for (int i = 0; i < _song.notes.Length; i++)
                _handles.Add(new NoteHandle { index = i });
        }

        // Конвертация MIDI ↔ ряд таймлайна
        private int MidiToRow(int midi)
        {
            if (_settings == null || _settings.baseClips.Length == 0) return 0;
            int noteCount = _settings.baseClips.Length;
            int octave    = midi / 12 - 1;
            int noteIdx   = MidiToNoteIndex(midi);
            int row       = (_settings.maxOctave - octave) * noteCount + noteIdx;
            return Mathf.Clamp(row, 0, _tl.RowCount - 1);
        }

        private int RowToMidi(int row)
        {
            if (_settings == null || _settings.baseClips.Length == 0) return 60;
            int noteCount = _settings.baseClips.Length;
            int octave    = _settings.maxOctave - row / noteCount;
            int noteIdx   = row % noteCount;
            noteIdx       = Mathf.Clamp(noteIdx, 0, noteCount - 1);
            return _settings.baseClips[noteIdx].midiNote + (octave - _settings.minOctave) * 12;
        }

        private int MidiToNoteIndex(int midi)
        {
            if (_settings == null) return 0;
            int baseMidi = ((midi / 12 - 1) == _settings.minOctave)
                ? midi
                : midi - ((midi / 12 - 1) - _settings.minOctave) * 12;
            for (int i = 0; i < _settings.baseClips.Length; i++)
                if (_settings.baseClips[i].midiNote == baseMidi) return i;
            // Fallback: find closest
            int best = 0; int bestDist = int.MaxValue;
            int noteInOctave = midi % 12;
            for (int i = 0; i < _settings.baseClips.Length; i++)
            {
                int d = Mathf.Abs(_settings.baseClips[i].midiNote % 12 - noteInOctave);
                if (d < bestDist) { bestDist = d; best = i; }
            }
            return best;
        }

        private void ApplyBaseClip(ref NoteData note)
        {
            if (_settings == null) return;
            int noteIdx = MidiToNoteIndex(note.midiNote);
            var entry   = _settings.baseClips[noteIdx];
            note.baseClip     = entry.clip;
            note.baseMidiNote = entry.midiNote;
        }

        private string MidiToLabel(int midi)
        {
            if (_settings == null || _settings.baseClips.Length == 0) return midi.ToString();
            int noteIdx = MidiToNoteIndex(midi);
            int octave  = midi / 12 - 1;
            return _settings.baseClips[noteIdx].noteName + octave;
        }

        private void AddNoteAtPlayhead()
        {
            float ms  = _tl.Snap(_playheadMs, _settings?.snapMs ?? 0f);
            int   row = _tl.RowCount / 2;
            AddNote(Mathf.Max(0f, ms), row);
        }

        private void AddNoteAt(Vector2 mousePos)
        {
            float ms  = _tl.Snap(_tl.PixelToTime(mousePos.x), _settings?.snapMs ?? 0f);
            int   row = _tl.YToRow(mousePos.y);
            AddNote(Mathf.Max(0f, ms), row);
        }

        private void AddNote(float ms, int row)
        {
            if (_settings == null) return;
            Undo.RecordObject(_song, "Add Note");

            var note = new NoteData
            {
                timeMs           = ms,
                speed            = _settings.defaultSpeed,
                tapWindowMs      = _settings.defaultWindowMs,
                noteDurationSec  = _settings.defaultNoteDurationSec,
                fadeSec          = _settings.defaultFadeSec,
                midiNote         = RowToMidi(row),
                lane             = Mathf.Clamp(row * 4 / _tl.RowCount, 0, 3),
            };
            ApplyBaseClip(ref note);

            var list = new List<NoteData>(_song.notes) { note };
            list.Sort((a, b) => a.timeMs.CompareTo(b.timeMs));
            _song.notes = list.ToArray();
            EditorUtility.SetDirty(_song);
            RebuildHandles(); Repaint();
        }

        private void DeleteNote(int idx)
        {
            Undo.RecordObject(_song, "Delete Note");
            var list = new List<NoteData>(_song.notes);
            list.RemoveAt(idx);
            _song.notes = list.ToArray();
            _selected = -1;
            EditorUtility.SetDirty(_song);
            RebuildHandles(); Repaint();
        }

        private void ClearTrack()
        {
            if (_song == null) return;
            if (!EditorUtility.DisplayDialog("Очистить трек?",
                $"Удалить все {_song.notes.Length} нот из '{_song.songTitle}'?",
                "Удалить", "Отмена")) return;
            Undo.RecordObject(_song, "Clear Track");
            _song.notes = System.Array.Empty<NoteData>();
            _selected = -1; _playheadMs = 0f;
            EditorUtility.SetDirty(_song);
            RebuildHandles(); Repaint();
        }

        private void Save()
        {
            if (_song == null) return;
            EditorUtility.SetDirty(_song);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SongEditor] Saved '{_song.songTitle}'");
        }
    }
}
