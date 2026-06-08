using System;
using System.Collections;
using UnityEngine;
using Karaoke.Data;

namespace Karaoke.Tap
{
    public enum BlobPhase { Flying, Waiting, Done }

    public class BlobNote : MonoBehaviour
    {
        /// <summary>clip, pitch, durationSec, fadeSec</summary>
        public static event Action<AudioClip, float, float, float> OnNoteHit;

        public NoteData  NoteData { get; private set; }
        public BlobPhase Phase    { get; private set; }

        private Transform _hitZone;
        private float     _arrivalTime;
        private float     _waitUntil;
        private bool      _initialized;

        private MeshRenderer  _renderer;
        private MaterialPropertyBlock _mpb;
        private static readonly int ColorProp = Shader.PropertyToID("_Color");

        private bool    _dying;
        private Vector3 _originalScale;

        public void Init(NoteData data, Transform hitZone, float arrivalTime)
        {
            NoteData      = data;
            _hitZone      = hitZone;
            _arrivalTime  = arrivalTime;
            _waitUntil    = arrivalTime + data.tapWindowMs / 1000f;
            Phase         = BlobPhase.Flying;
            _dying        = false;
            _originalScale = transform.localScale;
            transform.localScale = _originalScale;

            _renderer = GetComponent<MeshRenderer>();
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            SetColor(Color.white);

            _initialized = true;
        }

        private void Update()
        {
            if (!_initialized || _dying) return;

            switch (Phase)
            {
                case BlobPhase.Flying:
                    MoveTowardHitZone();
                    break;
                case BlobPhase.Waiting:
                    AnimateWaiting();
                    if (Time.time >= _waitUntil) Miss();
                    break;
            }
        }

        private void MoveTowardHitZone()
        {
            float remaining = _arrivalTime - Time.time;
            if (remaining <= 0f)
            {
                transform.position = _hitZone.position;
                Phase = BlobPhase.Waiting;
                return;
            }
            transform.position = Vector3.MoveTowards(
                transform.position, _hitZone.position, NoteData.speed * Time.deltaTime);
        }

        private void AnimateWaiting()
        {
            float windowSec = NoteData.tapWindowMs / 1000f;
            float elapsed   = Time.time - (_waitUntil - windowSec);
            float t         = Mathf.Clamp01(elapsed / windowSec);
            transform.localScale = _originalScale * Mathf.Lerp(1f, 1.6f, t);
            SetColor(Color.Lerp(Color.white, Color.red, t));
        }

        public void RegisterTap()
        {
            if (Phase != BlobPhase.Waiting) return;
            Phase = BlobPhase.Done;
            OnNoteHit?.Invoke(NoteData.baseClip, NoteData.Pitch, NoteData.noteDurationSec, NoteData.fadeSec);
            StartCoroutine(DieRoutine(hit: true));
        }

        private void Miss()
        {
            if (Phase == BlobPhase.Done) return;
            Phase = BlobPhase.Done;
            HitJudge.Instance?.RegisterMiss(this);
            StartCoroutine(DieRoutine(hit: false));
        }

        private IEnumerator DieRoutine(bool hit)
        {
            _dying = true;
            float elapsed  = 0f;
            float duration = 0.15f;
            Vector3 startScale = hit ? _originalScale * 1.4f : _originalScale;
            transform.localScale = startScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, elapsed / duration);
                yield return null;
            }

            gameObject.SetActive(false);
            _initialized = false;
        }

        private void SetColor(Color c)
        {
            if (_renderer == null) return;
            _mpb.SetColor(ColorProp, c);
            _renderer.SetPropertyBlock(_mpb);
        }
    }
}
