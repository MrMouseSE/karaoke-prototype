using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Karaoke.Core
{
    /// <summary>
    /// Единственная точка воспроизведения звуков.
    /// Играет ноту в лупе нужное время, затем делает fade out.
    /// </summary>
    public class AccompanimentPlayer : MonoBehaviour
    {
        [SerializeField] private int poolSize = 16;

        private Queue<AudioSource> _pool;
        private List<AudioSource>  _active;

        private void Awake()
        {
            _pool   = new Queue<AudioSource>();
            _active = new List<AudioSource>();

            for (int i = 0; i < poolSize; i++)
            {
                var go = new GameObject($"AudioSource_{i}");
                go.transform.SetParent(transform);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop        = false;
                _pool.Enqueue(src);
            }
        }

        /// <summary>
        /// Воспроизвести ноту: клип в лупе с заданным питчем, держать durationSec, затем fade out за fadeSec.
        /// </summary>
        public void PlayNote(AudioClip clip, float pitch, float durationSec, float fadeSec)
        {
            if (clip == null) return;
            if (_pool.Count == 0) { Debug.LogWarning("[AccompanimentPlayer] Pool exhausted."); return; }

            var src = _pool.Dequeue();
            _active.Add(src);

            src.clip   = clip;
            src.pitch  = pitch;
            src.volume = 1f;
            src.loop   = true;
            src.Play();

            StartCoroutine(NoteRoutine(src, durationSec, fadeSec));
        }

        private IEnumerator NoteRoutine(AudioSource src, float durationSec, float fadeSec)
        {
            // Держим ноту
            yield return new WaitForSeconds(durationSec);

            // Fade out
            float elapsed = 0f;
            while (elapsed < fadeSec)
            {
                elapsed   += Time.deltaTime;
                src.volume = Mathf.Lerp(1f, 0f, elapsed / fadeSec);
                yield return null;
            }

            src.Stop();
            src.loop   = false;
            src.volume = 1f;
            _active.Remove(src);
            _pool.Enqueue(src);
        }
    }
}
