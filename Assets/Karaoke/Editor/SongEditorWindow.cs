using System;
using System.Collections.Generic;
using System.Reflection;
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
        private float  _playStartMs   = 0f;
        private float  _playheadMs    = 0f;
        private int    _nextNoteIdx   = 0;

        // AudioUtil reflection
        private static AudioClip _lastPlayedClip;

        private static void PlaySound(AudioClip clip, int startSample = 0, bool loop = false)
        {
            Assembly unityEditorAssembly = typeof(AudioImporter).Assembly;
      
            Type audioUtilClass = unityEditorAssembly.GetType("UnityEditor.AudioUtil");
            MethodInfo method = audioUtilClass.GetMethod(
                "PlayPreviewClip",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new Type[] { typeof(AudioClip), typeof(int), typeof(bool) },
                null
            );

            method.Invoke(null, new object[] { clip, startSample, loop });
        }
        
        private static void StopAllClips()
        {
            Assembly unityEditorAssembly = typeof(AudioImporter).Assembly;

            Type audioUtilClass = unityEditorAssembly.GetType("UnityEditor.AudioUtil");
            MethodInfo method = audioUtilClass.GetMethod(
                "StopAllPreviewClips",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new Type[] { },
                null
            );

            method.Invoke(null, new object[] { });
        }

        private static void PlayClip(AudioClip clip)
        {
            if (clip == null) return;
            StopAllClips();
            PlaySound(clip);
        }

        // ── Состояние drag ───────────────────────────────────────────
        private int     _selected    = -1;
        private int     _dragging    = -1;
        private bool    _resizing    = false;
        private Vector2 _dragStart;
        private float   _dragOrigMs;
        private float   _dragOrigWin;
        private int     _dragOrigRow;

        // ── Константы ────────────────────────────────────────────────
        private const float  ToolbarH     = 44f;
        private const float  InspectorH   = 110f;
        private const string SettingsPath = "Assets/Karaoke/Editor/SongEditorSettings.asset";

        // ─────────────────────────────────────────────────────────────
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
            if (_song == null)
            {
                EditorGUILayout.HelpBox("Выберите SongData в поле выше.", MessageType.Info);
                return;
            }
            SyncRowCount();
            float timelineH = position.height - ToolbarH - InspectorH;
            _tl.area      = new Rect(0, ToolbarH, position.width, timelineH);
            _tl.RowHeight = Mathf.Max(20f, timelineH / _tl.RowCount);
            DrawTimeline();
            DrawInspector();
            HandleEvents();
        }

        // ── Toolbar ──────────────────────────────────────────────────
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(ToolbarH));

            EditorGUI.BeginChangeCheck();
            var newSong = (SongData)EditorGUILayout.ObjectField("Song", _song, typeof(SongData), false, GUILayout.Width(280));
            if (EditorGUI.EndChangeCheck()) { _song = newSong; _selected = -1; _playheadMs = 0f; RebuildHandles(); }

            GUILayout.Space(8);
            GUILayout.Label("Zoom", GUILayout.Width(36));
            _tl.PixelsPerMs = GUILayout.HorizontalSlider(_tl.PixelsPerMs, 0.02f, 0.8f, GUILayout.Width(80));

            GUILayout.Space(8);
            if (_settings != null)
            {
                GUILayout.Label("Snap", GUILayout.Width(30));
                _settings.snapMs = EditorGUILayout.FloatField(_settings.snapMs, GUILayout.Width(45));
            }

            GUILayout.Space(12);

            GUI.color = _isPlaying ? Color.red : Color.green;
            if (GUILayout.Button(_isPlaying ? "■ Stop" : "▶ Play", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                if (_isPlaying) StopPlayback(); else StartPlayback();
            }
            GUI.color = Color.white;
            GUILayout.Label($"{_playheadMs / 1000f:F2}s", GUILayout.Width(44));

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("+ Note", EditorStyles.toolbarButton, GUILayout.Width(56)))
                AddNoteAtScroll();

            GUI.color = new Color(1f, 0.5f, 0.5f);
            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(48)))
                ClearTrack();
            GUI.color = Color.white;

            if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(44)))
                Save();

            EditorGUILayout.EndHorizontal();
        }

        // ── Timeline ─────────────────────────────────────────────────
        private void DrawTimeline()
        {
            float dur = _song != null ? Mathf.Max(_song.TotalDurationMs, 5000f) : 5000f;
            _tl.DrawGrid(dur);

            if (_settings != null && _settings.pitchRows.Length > 0)
            {
                var labels = new string[_settings.pitchRows.Length];
                for (int i = 0; i < labels.Length; i++) labels[i] = _settings.pitchRows[i].label;
                _tl.DrawRowLabels(labels);
            }

            DrawNotes();
            DrawPlayhead();

            float contentW = _tl.MsToPixels(dur + 2000f) + _tl.labelWidth;
            float scrollPx = GUI.HorizontalScrollbar(
                new Rect(0, _tl.area.yMax - 14f, position.width, 14f),
                _tl.scrollMs * _tl.PixelsPerMs, position.width, 0f, contentW);
            if (!_isPlaying) _tl.scrollMs = scrollPx / _tl.PixelsPerMs;
        }

        private void DrawPlayhead()
        {
            float px = _tl.TimeToPixel(_playheadMs);
            if (px < _tl.area.x + _tl.labelWidth || px > _tl.area.xMax) return;
            EditorGUI.DrawRect(new Rect(px - 1f, _tl.area.y, 2f, _tl.TotalHeight), new Color(1f, 1f, 0f, 0.9f));
            GUI.Label(new Rect(px + 2f, _tl.area.y, 50f, 14f), $"{_playheadMs / 1000f:F2}s",
                new GUIStyle(GUI.skin.label) { fontSize = 9, normal = { textColor = Color.yellow } });
        }

        private void DrawNotes()
        {
            if (_song == null) return;
            for (int i = 0; i < _handles.Count; i++)
            {
                var h    = _handles[i];
                var note = _song.notes[h.index];
                int row  = NoteToRow(h.index);
                h.UpdateRect(note, _tl, row);

                bool  sel    = i == _selected;
                Color body   = sel ? new Color(1f, 0.85f, 0.2f) : new Color(0.3f, 0.7f, 1f);
                Color resize = new Color(1f, 1f, 1f, 0.5f);

                EditorGUI.DrawRect(h.rect,         body);
                EditorGUI.DrawRect(h.resizeHandle, resize);

                var labelStyle = new GUIStyle(GUI.skin.label)
                    { fontSize = 9, alignment = TextAnchor.MiddleLeft, normal = { textColor = Color.black } };
                GUI.Label(new Rect(h.rect.x + 2, h.rect.y, h.rect.width - 10, h.rect.height),
                    NoteLabel(h.index), labelStyle);
            }
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

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Нота", EditorStyles.boldLabel, GUILayout.Width(40));
            GUILayout.Label("Time (ms)", GUILayout.Width(60));
            note.timeMs = EditorGUILayout.FloatField(note.timeMs, GUILayout.Width(64));
            GUILayout.Space(8);
            GUILayout.Label("Окно тапа (ms)", GUILayout.Width(96));
            note.tapWindowMs = EditorGUILayout.FloatField(note.tapWindowMs, GUILayout.Width(56));
            GUILayout.Space(8);
            GUILayout.Label("Скорость", GUILayout.Width(58));
            note.speed = EditorGUILayout.FloatField(note.speed, GUILayout.Width(40));
            GUILayout.Space(8);
            GUILayout.Label("Клип", GUILayout.Width(30));
            note.clip = (AudioClip)EditorGUILayout.ObjectField(note.clip, typeof(AudioClip), false, GUILayout.Width(120));
            GUILayout.Space(8);
            GUILayout.Label("Lane", GUILayout.Width(30));
            note.lane = EditorGUILayout.IntField(note.lane, GUILayout.Width(28));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Delete", GUILayout.Width(56)))
            {
                DeleteNote(idx);
                GUILayout.EndHorizontal(); GUILayout.EndArea(); return;
            }
            EditorGUILayout.EndHorizontal();

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
                int  hit       = -1;
                bool hitResize = false;
                for (int i = _handles.Count - 1; i >= 0; i--)
                {
                    if (_handles[i].ResizeHitTest(e.mousePosition)) { hit = i; hitResize = true; break; }
                    if (_handles[i].HitTest(e.mousePosition))       { hit = i; break; }
                }

                if (hit >= 0)
                {
                    _selected = hit; _dragging = hit; _resizing = hitResize;
                    _dragStart   = e.mousePosition;
                    var n        = _song.notes[_handles[hit].index];
                    _dragOrigMs  = n.timeMs; _dragOrigWin = n.tapWindowMs;
                    _dragOrigRow = NoteToRow(_handles[hit].index);
                    e.Use();
                }
                else if (e.clickCount == 2) { AddNoteAt(e.mousePosition); e.Use(); }
                else                        { _selected = -1; }
                Repaint();
            }

            if (e.type == EventType.MouseDrag && _dragging >= 0)
            {
                Vector2 delta = e.mousePosition - _dragStart;
                int     idx   = _handles[_dragging].index;
                var     note  = _song.notes[idx];
                Undo.RecordObject(_song, "Move Note");

                if (_resizing)
                {
                    note.tapWindowMs = Mathf.Max(50f, _dragOrigWin + _tl.PixelsToMs(delta.x));
                }
                else
                {
                    float newMs = _dragOrigMs + _tl.PixelsToMs(delta.x);
                    note.timeMs = Mathf.Max(0f, _tl.Snap(newMs, _settings?.snapMs ?? 0f));
                    int newRow  = Mathf.Clamp(_dragOrigRow + Mathf.RoundToInt(delta.y / _tl.RowHeight), 0, _tl.RowCount - 1);
                    ApplyRowToNote(ref note, newRow);
                }

                _song.notes[idx] = note;
                EditorUtility.SetDirty(_song);
                e.Use(); Repaint();
            }

            if (e.type == EventType.MouseUp && _dragging >= 0) { _dragging = -1; _resizing = false; e.Use(); }

            if (e.type == EventType.KeyDown && _selected >= 0
                && (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace))
            {
                DeleteNote(_handles[_selected].index); e.Use();
            }
        }

        // ── Playback ─────────────────────────────────────────────────
        private void StartPlayback()
        {
            if (_song == null) return;
            _playheadMs    = 0f;
            _playStartMs   = 0f;
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

        private void OnEditorUpdate()
        {
            if (!_isPlaying || _song == null) { StopPlayback(); return; }

            float elapsed = (float)(EditorApplication.timeSinceStartup - _playStartTime) * 1000f;
            _playheadMs = _playStartMs + elapsed;

            if (_playheadMs >= _song.TotalDurationMs)
            {
                _playheadMs = _song.TotalDurationMs;
                StopPlayback(); return;
            }

            var notes = _song.notes;
            while (_nextNoteIdx < notes.Length && notes[_nextNoteIdx].timeMs <= _playheadMs)
                PlayClip(notes[_nextNoteIdx++].clip);

            _tl.scrollMs = Mathf.Max(0f,
                _playheadMs - (_tl.area.width - _tl.labelWidth) / _tl.PixelsPerMs * 0.5f);
            Repaint();
        }

        private int FindFirstNoteAfter(float ms)
        {
            if (_song == null) return 0;
            for (int i = 0; i < _song.notes.Length; i++)
                if (_song.notes[i].timeMs >= ms) return i;
            return _song.notes.Length;
        }

        // ── Helpers ──────────────────────────────────────────────────
        private void RebuildHandles()
        {
            _handles.Clear();
            if (_song == null) return;
            for (int i = 0; i < _song.notes.Length; i++)
                _handles.Add(new NoteHandle { index = i });
        }

        private void SyncRowCount()
        {
            _tl.RowCount = (_settings != null && _settings.pitchRows.Length > 0)
                ? _settings.pitchRows.Length : 6;
        }

        private int NoteToRow(int noteIdx)
        {
            if (_settings == null || _settings.pitchRows.Length == 0) return 0;
            var clip = _song.notes[noteIdx].clip;
            for (int r = 0; r < _settings.pitchRows.Length; r++)
                if (_settings.pitchRows[r].clip == clip) return r;
            return 0;
        }

        private void ApplyRowToNote(ref NoteData note, int row)
        {
            if (_settings == null || row >= _settings.pitchRows.Length) return;
            note.clip = _settings.pitchRows[row].clip;
            note.lane = _settings.pitchRows[row].lane;
        }

        private string NoteLabel(int idx)
        {
            if (_settings == null) return "";
            var clip = _song.notes[idx].clip;
            for (int r = 0; r < _settings.pitchRows.Length; r++)
                if (_settings.pitchRows[r].clip == clip) return _settings.pitchRows[r].label;
            return "?";
        }

        private void AddNoteAtScroll()
        {
            float ms = _tl.PixelToTime(_tl.area.x + _tl.labelWidth + 40f);
            AddNote(Mathf.Max(0f, ms), 0);
        }

        private void AddNoteAt(Vector2 mousePos)
        {
            float ms  = _tl.Snap(_tl.PixelToTime(mousePos.x), _settings?.snapMs ?? 0f);
            int   row = _tl.YToRow(mousePos.y);
            AddNote(Mathf.Max(0f, ms), row);
        }

        private void AddNote(float ms, int row)
        {
            Undo.RecordObject(_song, "Add Note");
            var note = new NoteData
            {
                timeMs      = ms,
                speed       = _settings?.defaultSpeed    ?? 5f,
                tapWindowMs = _settings?.defaultWindowMs ?? 350f,
                lane        = 0,
            };
            ApplyRowToNote(ref note, row);
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
            if (!EditorUtility.DisplayDialog(
                "Очистить трек?",
                $"Удалить все {_song.notes.Length} нот из '{_song.songTitle}'?",
                "Удалить", "Отмена")) return;
            Undo.RecordObject(_song, "Clear Track");
            _song.notes = System.Array.Empty<NoteData>();
            _selected   = -1;
            _playheadMs = 0f;
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
