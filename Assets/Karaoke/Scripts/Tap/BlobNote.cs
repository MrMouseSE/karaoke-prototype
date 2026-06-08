using System;
using UnityEngine;
using Karaoke.Data;

namespace Karaoke.Tap
{
    public enum BlobPhase { Flying, Waiting, Done }

    public class BlobNote : MonoBehaviour
    {
        // Событие: (clip) — кидается при тапе, NoteSpawner слушает
        public static event Action<AudioClip> OnNoteHit;

        // Данные текущей ноты
        public NoteData NoteData { get; private set; }
        public BlobPhase Phase { get; private set; }

        private Transform _hitZone;
        private float _arrivalTime;      // Time.time когда блоб должен прибыть
        private float _waitUntil;        // Time.time до которого блоб ждёт тапа
        private bool _initialized;

        // Анимация исчезновения
        
        private MeshRenderer _renderer;
        private static readonly int ColorProp = Shader.PropertyToID("_Color");
        private MaterialPropertyBlock _mpb;
private bool _dying;
        private float _dieTimer;
        private const float DieDuration = 0.15f;
        private Vector3 _originalScale;

        public void Init(NoteData data, Transform hitZone, float arrivalTime)
        {
            NoteData = data;
            _hitZone = hitZone;
            _arrivalTime = arrivalTime;
            _waitUntil = arrivalTime + data.tapWindowMs / 1000f;
            Phase = BlobPhase.Flying;
            _dying = false;
            _originalScale = transform.localScale;
            _renderer = GetComponent<MeshRenderer>();
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            if (_renderer != null) { _renderer.GetPropertyBlock(_mpb); _mpb.SetColor(ColorProp, Color.white); _renderer.SetPropertyBlock(_mpb); }
            transform.localScale = _originalScale;
            _initialized = true;
        }

private void Update() { if (!_initialized || _dying) return; float now = Time.time; switch (Phase) { case BlobPhase.Flying: MoveTowardHitZone(now); break; case BlobPhase.Waiting: AnimateWaiting(now); if (now >= _waitUntil) Miss(); break; } }

        private void MoveTowardHitZone(float now)
        {
            float remaining = _arrivalTime - now;

            if (remaining <= 0f)
            {
                // Прибыли — защёлкиваемся в хит-зоне
                transform.position = _hitZone.position;
                Phase = BlobPhase.Waiting;
                return;
            }

            // Двигаемся с постоянной скоростью к хит-зоне
            transform.position = Vector3.MoveTowards(
                transform.position,
                _hitZone.position,
                NoteData.speed * Time.deltaTime
            );
        }

private void AnimateWaiting(float now) { float elapsed = now - (_waitUntil - NoteData.tapWindowMs / 1000f); float t = Mathf.Clamp01(elapsed / (NoteData.tapWindowMs / 1000f)); float scale = Mathf.Lerp(1f, 1.6f, t); transform.localScale = _originalScale * scale; if (_renderer != null) { Color c = Color.Lerp(Color.white, Color.red, t); _mpb.SetColor(ColorProp, c); _renderer.SetPropertyBlock(_mpb); } }


        // Вызывается HitJudge при тапе
        public void RegisterTap()
        {
            if (Phase != BlobPhase.Waiting) return;
            Phase = BlobPhase.Done;
            OnNoteHit?.Invoke(NoteData.clip);
            StartDie(hit: true);
        }

        private void Miss()
        {
            if (Phase == BlobPhase.Done) return;
            Phase = BlobPhase.Done;
            HitJudge.Instance?.RegisterMiss(this);
            StartDie(hit: false);
        }

        private void StartDie(bool hit)
        {
            _dying = true;
            _dieTimer = 0f;
            if (!hit) transform.localScale = _originalScale;
            StartCoroutine(DieRoutine(hit));
        }

        private System.Collections.IEnumerator DieRoutine(bool hit)
        {
            float elapsed = 0f;
            Vector3 startScale = hit ? _originalScale * 1.4f : _originalScale;

            if (hit) transform.localScale = startScale;

            while (elapsed < DieDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / DieDuration;
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
                yield return null;
            }

            gameObject.SetActive(false);
            _initialized = false;
        }
    }
}
