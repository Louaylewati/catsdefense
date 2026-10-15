using UnityEngine;

namespace CatDefense
{
    public static class Fx
    {
        // packed explosion frame is ~5.1 world units across at scale 1
        const float ExplosionSize = 5.1f;

        public static SpriteAnim Play(string key, Vector3 pos, float scale, float angle = 0f, int order = 4000, float speed = 1f, Transform parent = null)
        {
            var go = new GameObject(key);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0, 0, angle);
            go.transform.localScale = Vector3.one * scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            var a = go.AddComponent<SpriteAnim>();
            a.Play(key, false, speed, () => Object.Destroy(go));
            return a;
        }

        /// <summary>Explosion burst with the given world diameter.</summary>
        public static void Explosion(Vector3 pos, float diameter, float speed = 1.4f)
        {
            Play("Explosion_fx", pos, diameter / ExplosionSize, Random.Range(0f, 360f), 4000, speed);
        }

        public static void Muzzle(Vector3 pos, float angle, float scale)
        {
            Play("Muzzle_fx", pos, scale, angle, 3500, 2.2f);
        }

        public static void Coin(Vector3 pos)
        {
            var go = new GameObject("CoinPop");
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * 0.9f;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Art.UI("CoinIcon");
            sr.sortingOrder = 4500;
            go.AddComponent<FloatUp>();
        }
    }

    /// <summary>Rises and fades, then destroys itself.</summary>
    public class FloatUp : MonoBehaviour
    {
        public float Duration = 0.7f, Rise = 1.0f;
        float t;
        SpriteRenderer sr;
        Vector3 start;

        void Start()
        {
            sr = GetComponent<SpriteRenderer>();
            start = transform.position;
        }

        void Update()
        {
            t += Time.deltaTime;
            float k = t / Duration;
            transform.position = start + Vector3.up * Rise * Mathf.Sqrt(Mathf.Clamp01(k));
            if (sr != null) sr.color = new Color(1, 1, 1, 1 - Mathf.Clamp01((k - 0.5f) * 2));
            if (k >= 1) Destroy(gameObject);
        }
    }

    /// <summary>Fades a sprite out then destroys it (dead enemies).</summary>
    public class FadeOut : MonoBehaviour
    {
        public float Delay = 0.4f, Duration = 0.5f;
        float t;
        SpriteRenderer sr;

        void Start() => sr = GetComponent<SpriteRenderer>();

        void Update()
        {
            t += Time.deltaTime;
            if (t < Delay) return;
            float k = (t - Delay) / Duration;
            if (sr != null) sr.color = new Color(1, 1, 1, 1 - k);
            if (k >= 1) Destroy(gameObject);
        }
    }
}
