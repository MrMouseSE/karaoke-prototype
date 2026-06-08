using System.Collections.Generic;
using UnityEngine;

namespace Karaoke.Core
{
    // Единственная точка воспроизведения звуков в игре.
    // Не знает ни о блобах, ни о тапах — только играет клипы по команде.
    public class AccompanimentPlayer : MonoBehaviour
    {
        [SerializeField] private int poolSize = 16;

        private Queue<AudioSource> _pool;
        private List<AudioSource> _active;

        private void Awake()
        {
            _pool = new Queue<AudioSource>();
            _active = new List<AudioSource>();

            for (int i = 0; i < poolSize; i++)
            {
                var go = new GameObject($"AudioSource_{i}");
                go.transform.SetParent(transform);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                _pool.Enqueue(src);
            }
        }

        private void Update()
        {
            // Возвращаем завершившиеся источники в пул
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (!_active[i].isPlaying)
                {
                    _pool.Enqueue(_active[i]);
                    _active.RemoveAt(i);
                }
            }
        }

        public void PlayClip(AudioClip clip)
        {
            if (clip == null) return;
            if (_pool.Count == 0)
            {
                Debug.LogWarning("[AccompanimentPlayer] Pool exhausted.");
                return;
            }

            var src = _pool.Dequeue();
            _active.Add(src);
            src.clip = clip;
            src.pitch = 1f;
            src.Play();
        }
    }
}
