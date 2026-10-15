using System.Collections.Generic;
using UnityEngine;

namespace CatDefense
{
    /// <summary>Straight-line shot: hits the first thing it touches (or every enemy when piercing; a hostage always stops it).</summary>
    public class Projectile : MonoBehaviour
    {
        const float Speed = 1400f * GameData.Px;

        Battle b;
        Vector2 dir;
        float damage, splashDamage, splashRadius;
        bool pierce;
        readonly HashSet<Enemy> hitAlready = new HashSet<Enemy>();

        public static void Fire(Battle b, Vector2 from, Vector2 dir, int colour, float damage, bool pierce, float splashDamage, float splashRadius)
        {
            var go = new GameObject("Shot");
            go.transform.position = from;
            go.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            go.transform.localScale = Vector3.one * (pierce ? 1.4f : 0.9f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Art.Fx("Bullet" + Mathf.Clamp(colour, 0, 2));
            sr.sortingOrder = 3000;
            var p = go.AddComponent<Projectile>();
            p.b = b;
            p.dir = dir.normalized;
            p.damage = damage;
            p.pierce = pierce;
            p.splashDamage = splashDamage;
            p.splashRadius = splashRadius;
        }

        void Update()
        {
            Vector2 pos = transform.position;
            Vector2 next = pos + dir * Speed * Time.deltaTime;
            transform.position = next;

            // the boss's hostage is in the line of fire like anything else
            var h = b.Hostage;
            if (h != null && h.Alive && DistToSegment(h.Center, pos, next) <= h.Radius + 0.05f)
            {
                h.Hit(damage);
                if (splashRadius > 0) Fx.Explosion(next, splashRadius * 2f, 2f);
                Destroy(gameObject);
                return;
            }

            // segment-vs-circle so fast shots never tunnel through enemies
            var list = b.Enemies;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var e = list[i];
                if (e == null || !e.Alive || hitAlready.Contains(e)) continue;
                if (e.Center.x - e.Radius > b.VisibleRight) continue; // can't hit what the player can't see yet
                if (DistToSegment(e.Center, pos, next) > e.Radius + 0.12f) continue;
                hitAlready.Add(e);
                e.Hit(damage);
                if (splashRadius > 0)
                {
                    b.Splash(next, splashRadius, splashDamage, e);
                    Fx.Explosion(next, splashRadius * 2f, 2f);
                }
                if (!pierce) { Destroy(gameObject); return; }
            }

            if (next.x > b.VisibleRight + 1f || Mathf.Abs(next.x) > 14 || Mathf.Abs(next.y) > 7) Destroy(gameObject);
        }

        static float DistToSegment(Vector2 p, Vector2 a, Vector2 c)
        {
            Vector2 ab = c - a;
            float t = ab.sqrMagnitude < 1e-6f ? 0 : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
