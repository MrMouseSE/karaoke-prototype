using UnityEngine;

namespace Karaoke.Tap
{
    // Обрабатывает ввод: тачи на мобайле, мышь в редакторе, клавиши 1-4 на клавиатуре.
    public class TapInputHandler : MonoBehaviour
    {
        [SerializeField] private HitJudge hitJudge;
        [Tooltip("Количество дорожек (совпадает с laneRoots в NoteSpawner)")]
        [SerializeField] private int laneCount = 4;

        private void Update()
        {
            HandleTouchInput();
            HandleKeyboardInput();
#if UNITY_EDITOR
            HandleMouseInput();
#endif
        }

        // --- Touch ---
        private void HandleTouchInput()
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                var touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Began)
                    DispatchTap(ScreenPositionToLane(touch.position));
            }
        }

        // --- Mouse (только в редакторе) ---
        private void HandleMouseInput()
        {
            if (Input.GetMouseButtonDown(0))
                DispatchTap(ScreenPositionToLane(Input.mousePosition));
        }

        // --- Клавиатура: 1-4 и NumPad 1-4 ---
        private void HandleKeyboardInput()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) DispatchTap(0);
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) DispatchTap(1);
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) DispatchTap(2);
            if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) DispatchTap(3);
        }

        private void DispatchTap(int lane)
        {
            hitJudge?.RegisterTap(lane);
        }

        private int ScreenPositionToLane(Vector2 screenPosition)
        {
            float normalizedX = screenPosition.x / Screen.width;
            return Mathf.Clamp(Mathf.FloorToInt(normalizedX * laneCount), 0, laneCount - 1);
        }
    }
}
