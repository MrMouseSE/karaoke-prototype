using UnityEngine;
using Karaoke.Data;

namespace Karaoke.Editor
{
    // Представляет одну ноту на таймлайне — хранит индекс в SongData и вычисленный Rect
    public class NoteHandle
    {
        public int index;          // индекс в SongData.notes[]
        public Rect rect;          // позиция на таймлайне (пиксели)
        public Rect resizeHandle;  // правый край для изменения tapWindowMs

        private const float ResizeWidth = 8f;

        public void UpdateRect(NoteData note, TimelineView tl, int rowIndex)
        {
            float x     = tl.TimeToPixel(note.timeMs);
            float w     = tl.MsToPixels(note.tapWindowMs);
            float y     = tl.RowToY(rowIndex);
            float h     = tl.RowHeight - 2f;

            rect         = new Rect(x, y + 1f, Mathf.Max(w, ResizeWidth * 2), h);
            resizeHandle = new Rect(rect.xMax - ResizeWidth, rect.y, ResizeWidth, rect.height);
        }

        public bool HitTest(Vector2 pos)        => rect.Contains(pos);
        public bool ResizeHitTest(Vector2 pos)  => resizeHandle.Contains(pos);
    }
}
