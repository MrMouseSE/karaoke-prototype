using UnityEngine;

namespace Karaoke.Editor
{
    // Отвечает за систему координат таймлайна: zoom, scroll, конвертация время↔пиксели
    public class TimelineView
    {
        // Область таймлайна внутри окна
        public Rect area;

        // Ширина левой панели с лейблами питч-рядов
        public float labelWidth  = 48f;
        public float RowHeight   = 28f;
        public int   RowCount    = 6;

        // Горизонтальный скролл (мс)
        public float scrollMs    = 0f;
        // Пикселей на миллисекунду
        public float PixelsPerMs = 0.12f;

        // --- Конвертация ---
        public float TimeToPixel(float ms)   => labelWidth + (ms - scrollMs) * PixelsPerMs;
        public float PixelToTime(float px)   => (px - labelWidth) / PixelsPerMs + scrollMs;
        public float MsToPixels(float ms)    => ms * PixelsPerMs;
        public float PixelsToMs(float px)    => px / PixelsPerMs;

        public float RowToY(int row)         => area.y + row * RowHeight;
        public int   YToRow(float y)         => Mathf.Clamp((int)((y - area.y) / RowHeight), 0, RowCount - 1);

        public float TotalHeight             => RowCount * RowHeight;

        // Снэп к сетке (snapMs=0 отключает)
        public float Snap(float ms, float snapMs)
        {
            if (snapMs <= 0f) return ms;
            return Mathf.Round(ms / snapMs) * snapMs;
        }

        // Рисует вертикальные линии времени и горизонтальные разделители рядов
        public void DrawGrid(float totalDurationMs)
        {
            // Фон
            UnityEditor.EditorGUI.DrawRect(area, new Color(0.15f, 0.15f, 0.15f));

            // Горизонтальные полосы рядов
            for (int r = 0; r < RowCount; r++)
            {
                var rowRect = new Rect(area.x, RowToY(r), area.width, RowHeight);
                UnityEditor.EditorGUI.DrawRect(rowRect, r % 2 == 0
                    ? new Color(0.18f, 0.18f, 0.18f)
                    : new Color(0.21f, 0.21f, 0.21f));
            }

            // Вертикальные линии каждую секунду
            float stepMs = 1000f;
            float startMs = Mathf.Floor(scrollMs / stepMs) * stepMs;
            float endMs   = PixelToTime(area.xMax);

            for (float t = startMs; t <= endMs + stepMs; t += stepMs)
            {
                float px = TimeToPixel(t);
                if (px < area.x + labelWidth || px > area.xMax) continue;

                UnityEditor.EditorGUI.DrawRect(new Rect(px, area.y, 1f, TotalHeight),
                    new Color(0.4f, 0.4f, 0.4f));

                // Лейбл секунды
                GUI.Label(new Rect(px + 2f, area.y, 40f, 14f),
                    $"{t / 1000f:F0}s",
                    new GUIStyle(GUI.skin.label) { fontSize = 9, normal = { textColor = new Color(0.5f, 0.5f, 0.5f) } });
            }

            // Линия конца песни
            float endPx = TimeToPixel(totalDurationMs);
            if (endPx >= area.x + labelWidth && endPx <= area.xMax)
                UnityEditor.EditorGUI.DrawRect(new Rect(endPx, area.y, 2f, TotalHeight), new Color(1f, 0.4f, 0.4f, 0.8f));
        }

        // Рисует лейблы питч-рядов слева
        public void DrawRowLabels(string[] labels)
        {
            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleRight,
                fontSize  = 11,
                normal    = { textColor = new Color(0.8f, 0.8f, 0.8f) }
            };

            for (int r = 0; r < Mathf.Min(labels.Length, RowCount); r++)
            {
                var lRect = new Rect(area.x, RowToY(r), labelWidth - 4f, RowHeight);
                GUI.Label(lRect, labels[r], labelStyle);
            }
        }
    }
}
