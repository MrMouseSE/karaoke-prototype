using UnityEngine;

namespace Karaoke.Tap
{
    // Handles multitouch input on mobile and mouse clicks in editor.
    // Divides screen into N equal vertical lanes and maps each touch to a lane.
    public class TapInputHandler : MonoBehaviour
    {
        [SerializeField] private HitJudge hitJudge;
        [Tooltip("Number of lanes (must match NoteSpawner laneRoots count)")]
        [SerializeField] private int laneCount = 4;

        private void Update()
        {
#if UNITY_EDITOR
            HandleMouseInput();
#else
            HandleTouchInput();
#endif
        }

        private void HandleTouchInput()
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                var touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Began)
                    DispatchTap(touch.position);
            }
        }

        private void HandleMouseInput()
        {
            if (Input.GetMouseButtonDown(0))
                DispatchTap(Input.mousePosition);
        }

        private void DispatchTap(Vector2 screenPosition)
        {
            int lane = ScreenPositionToLane(screenPosition);
            hitJudge?.RegisterTap(lane);
        }

        private int ScreenPositionToLane(Vector2 screenPosition)
        {
            float normalizedX = screenPosition.x / Screen.width;
            int lane = Mathf.FloorToInt(normalizedX * laneCount);
            return Mathf.Clamp(lane, 0, laneCount - 1);
        }
    }
}
