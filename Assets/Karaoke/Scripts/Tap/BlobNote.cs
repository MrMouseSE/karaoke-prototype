using UnityEngine;

namespace Karaoke.Tap
{
    public enum HitResult { None, Perfect, Good, Miss }

    public class BlobNote : MonoBehaviour
    {
        [HideInInspector] public float timeMs;
        [HideInInspector] public int lane;

        // Timing windows in milliseconds
        public static float PerfectWindowMs = 60f;
        public static float GoodWindowMs = 120f;

        private float _travelDurationSec;
        private float _spawnTime;
        private bool _hit;
        private bool _missed;

        // Called by NoteSpawner after instantiation
        public void Init(float noteTimeMs, int noteLane, float travelDurationSec)
        {
            timeMs = noteTimeMs;
            lane = noteLane;
            _travelDurationSec = travelDurationSec;
            _spawnTime = Time.time;
            _hit = false;
            _missed = false;
        }

        private void Update()
        {
            float elapsed = Time.time - _spawnTime;
            float t = elapsed / _travelDurationSec;

            // Move from top (1) to bottom (0) in normalized Y on the lane
            float normalizedY = Mathf.Clamp01(1f - t);
            SetNormalizedPosition(normalizedY);

            // Auto-miss if blob passes the hit zone without a tap
            if (!_hit && !_missed && elapsed > _travelDurationSec + (GoodWindowMs / 1000f))
            {
                _missed = true;
                HitJudge.Instance?.RegisterMiss(this);
                Despawn();
            }
        }

        // Override in subclass or drive from outside to position the blob
        protected virtual void SetNormalizedPosition(float normalizedY)
        {
            var pos = transform.localPosition;
            pos.y = normalizedY;
            transform.localPosition = pos;
        }

        // Returns delta ms between tap time and note time (negative = early)
        public HitResult Evaluate(float songTimeMs)
        {
            if (_hit || _missed) return HitResult.None;

            float delta = Mathf.Abs(songTimeMs - timeMs);
            HitResult result;

            if (delta <= PerfectWindowMs)
                result = HitResult.Perfect;
            else if (delta <= GoodWindowMs)
                result = HitResult.Good;
            else
                result = HitResult.Miss;

            if (result != HitResult.Miss)
            {
                _hit = true;
                Despawn();
            }

            return result;
        }

        private void Despawn()
        {
            gameObject.SetActive(false);
        }
    }
}
