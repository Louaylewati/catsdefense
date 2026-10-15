using UnityEngine;

namespace CatDefense
{
    /// <summary>
    /// Level 2: the boss carries a captured cat in front of it. Shots and blasts that touch it hurt it,
    /// and the level is lost if its HP reaches 0. Killing the boss sets it free.
    /// </summary>
    public class Hostage : MonoBehaviour
    {
        public float Hp { get; private set; }
        public float MaxHp { get; private set; }
        public bool Alive => !dead && !freed;
        public bool Freed => freed;

        public Vector2 Center => (Vector2)transform.position + Vector2.up * unit.cy * scale;
        public float Radius => unit.r * scale;
        public Vector3 Top => transform.position + Vector3.up * unit.top * scale;

        Battle b;
        Enemy boss;
        SpriteRenderer sr;
        SpriteAnim anim;
        MaterialPropertyBlock mpb;
        Art.UnitInfo unit;
        float scale, flashT, t;
        bool dead, freed;
        Vector3 freedAt;

        public void Init(Battle battle, Enemy captor, CatDef look, float hp)
        {
            b = battle;
            boss = captor;
            MaxHp = Hp = hp;
            unit = Art.Unit(look.sprite);
            scale = look.scale * 0.8f;
            // faces the player, pleading
            transform.localScale = new Vector3(-scale, scale, 1);
            sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = Art.FlashMaterial;
            mpb = new MaterialPropertyBlock();
            anim = gameObject.AddComponent<SpriteAnim>();
            anim.Play(look.sprite + "_idle", true, 0.7f);
            transform.position = HoldPoint();
        }

        // held low in front of the boss: shots at the boss's head fly over it, shots at its legs hit the hostage
        Vector3 HoldPoint() => boss.transform.position + new Vector3(-boss.Radius * 1.05f, -0.35f, 0);

        void Update()
        {
            float dt = Time.deltaTime;
            t += dt;
            if (Alive && boss != null)
            {
                // struggles a little while it's carried
                transform.position = HoldPoint() + new Vector3(0, Mathf.Abs(Mathf.Sin(t * 6f)) * 0.08f, 0);
            }
            else if (freed)
            {
                // happy hops
                transform.position = freedAt + Vector3.up * Mathf.Abs(Mathf.Sin(t * 7f)) * 0.5f;
            }

            flashT -= dt;
            sr.GetPropertyBlock(mpb);
            mpb.SetFloat("_Flash", flashT > 0 ? 0.8f : 0f);
            mpb.SetColor("_FlashColor", new Color(1f, 0.3f, 0.25f));
            sr.SetPropertyBlock(mpb);
            sr.sortingOrder = 1000 + Mathf.RoundToInt(-transform.position.y * 100f) + 60;
        }

        public void Hit(float damage)
        {
            if (!Alive) return;
            Hp = Mathf.Max(0, Hp - damage);
            flashT = 0.12f;
            Sfx.Play("hurt", 0.7f, 0.08f);
            if (Hp > 0) return;
            dead = true;
            anim.Speed = 0;
            sr.color = new Color(0.6f, 0.6f, 0.6f);
            transform.rotation = Quaternion.Euler(0, 0, 90);
            b.HostageDown();
        }

        public void Free()
        {
            if (!Alive) return;
            freed = true;
            freedAt = transform.position;
            t = 0;
            transform.localScale = new Vector3(scale, scale, 1);
            anim.Speed = 1.4f;
            Sfx.Play("free");
        }
    }
}
