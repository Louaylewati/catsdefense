using System;
using UnityEngine;
using UnityEngine.UI;

namespace CatDefense
{
    /// <summary>Plays a frame sequence on a SpriteRenderer (world) or Image (UI).</summary>
    public class SpriteAnim : MonoBehaviour
    {
        public string Current { get; private set; }
        public float Speed = 1f;
        public bool Unscaled;

        SpriteRenderer sr;
        Image img;
        Sprite[] seq;
        float fps, t;
        bool loop, done;
        Action onDone;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            img = GetComponent<Image>();
        }

        public void Play(string key, bool loop, float speed = 1f, Action onDone = null, bool restart = true)
        {
            if (!restart && key == Current && !done) return;
            if (sr == null && img == null) Awake();
            Current = key;
            seq = Art.Anim(key);
            fps = Art.Fps(key);
            this.loop = loop;
            Speed = speed;
            this.onDone = onDone;
            t = 0;
            done = false;
            Apply(0);
        }

        public float Length => seq == null || fps <= 0 ? 0 : seq.Length / fps;

        void Update()
        {
            if (seq == null || seq.Length == 0 || done) return;
            t += (Unscaled ? Time.unscaledDeltaTime : Time.deltaTime) * Speed;
            int f = Mathf.FloorToInt(t * fps);
            if (loop) f %= seq.Length;
            else if (f >= seq.Length)
            {
                f = seq.Length - 1;
                done = true;
                Apply(f);
                var cb = onDone;
                onDone = null;
                cb?.Invoke();
                return;
            }
            Apply(f);
        }

        void Apply(int f)
        {
            if (seq == null || seq.Length == 0) return;
            if (sr != null) sr.sprite = seq[f];
            if (img != null) img.sprite = seq[f];
        }
    }
}
