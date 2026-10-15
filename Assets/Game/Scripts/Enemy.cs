using System.Collections;
using UnityEngine;

namespace CatDefense
{
    /// <summary>
    /// Shared zombie behaviour: walk left, stop at the barricade, hit it on a timer, die at 0 HP.
    /// A zombie that bumps into the player's cat stops and hits the cat instead, until the cat gets away.
    /// Types differ only by EnemyDef data. The boss adds the two-phase logic from the design doc.
    /// </summary>
    public class Enemy : MonoBehaviour
    {
        enum State { Walk, Attack, AttackCat, Dead }

        public EnemyDef Def { get; private set; }
        public float Hp { get; private set; }
        public float MaxHp { get; private set; }
        public bool IsBoss { get; private set; }
        public bool IsMini { get; private set; }
        public bool CountsForWave { get; private set; }
        public bool Alive => state != State.Dead;

        public Vector2 Center => (Vector2)transform.position + new Vector2(unit.cx, unit.cy) * scale;
        public float Radius => unit.r * scale;
        public Vector3 Top => transform.position + Vector3.up * unit.top * scale;

        State state;
        Battle b;
        SpriteRenderer sr;
        SpriteAnim anim;
        MaterialPropertyBlock mpb;
        Art.UnitInfo unit;
        string design;
        float scale, speed, hitTimer, flashT, stopX;
        Color flashColor = Color.white;

        // boss
        bool phase2, slamWarned;
        float spawnT, slamT, warnT;

        public void Init(Battle battle, EnemyDef def, float hp, Vector3 pos, bool countsForWave, bool boss, bool mini)
        {
            b = battle;
            Def = def;
            MaxHp = Hp = hp;
            IsBoss = boss;
            IsMini = mini;
            CountsForWave = countsForWave;
            design = def.designs[Random.Range(0, def.designs.Length)];
            unit = Art.Unit(design);
            scale = def.scale * (mini ? 1.4f : 1f);
            speed = def.speed;

            transform.position = pos;
            transform.localScale = Vector3.one * scale;
            sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = Art.FlashMaterial;
            mpb = new MaterialPropertyBlock();
            anim = gameObject.AddComponent<SpriteAnim>();
            anim.Play(design + "_walk", true, WalkAnimSpeed());

            stopX = Battle.BarricadeFront + Radius * 0.55f + def.stopDistance * GameData.Px;
            if (boss)
            {
                spawnT = GameData.Boss.phase1Every;
                slamT = GameData.Boss.slamEvery;
            }
            UpdateOrder();
        }

        void PlayAttack()
        {
            anim.Play(design + "_attack", true, 1f);
            anim.Speed = anim.Length / Def.hitEvery;
        }

        float WalkAnimSpeed() => Mathf.Clamp(speed / 45f, 0.6f, 1.8f);

        void Update()
        {
            if (state == State.Dead || b == null) return;
            float dt = Time.deltaTime;

            if (b.Playing)
            {
                if (state == State.Walk && b.TouchesCat(this))
                {
                    state = State.AttackCat;
                    hitTimer = Def.hitEvery * 0.5f;
                    PlayAttack();
                }
                else if (state == State.AttackCat && !b.TouchesCat(this))
                {
                    state = State.Walk;
                    anim.Play(design + "_walk", true, WalkAnimSpeed());
                }

                if (state == State.Walk)
                {
                    var p = transform.position;
                    p.x -= speed * GameData.Px * dt;
                    if (p.x <= stopX)
                    {
                        p.x = stopX;
                        state = State.Attack;
                        hitTimer = Def.hitEvery * 0.5f;
                        PlayAttack();
                    }
                    transform.position = p;
                }
                else if (state == State.AttackCat)
                {
                    hitTimer -= dt;
                    if (hitTimer <= 0)
                    {
                        hitTimer += Def.hitEvery;
                        b.DamageCat(Def.damage);
                    }
                }
                else if (state == State.Attack)
                {
                    hitTimer -= dt;
                    if (hitTimer <= 0)
                    {
                        hitTimer += Def.hitEvery;
                        b.DamageBarricade(Def.damage);
                    }
                }
                if (IsBoss) BossUpdate(dt);
            }

            UpdateFlash(dt);
            UpdateOrder();
        }

        void BossUpdate(float dt)
        {
            var bd = GameData.Boss;
            if (!phase2 && Hp < MaxHp * 0.5f)
            {
                phase2 = true;
                speed = bd.phase2Speed;
                spawnT = bd.phase2Every;
                if (state == State.Walk) anim.Speed = WalkAnimSpeed();
                Sfx.Play("roar");
                b.Shake(0.15f, 0.5f);
            }

            spawnT -= dt;
            if (spawnT <= 0)
            {
                spawnT = phase2 ? bd.phase2Every : bd.phase1Every;
                int n = phase2 ? bd.phase2Count : bd.phase1Count;
                string type = phase2 ? bd.phase2Spawn : bd.phase1Spawn;
                for (int i = 0; i < n; i++)
                    b.SpawnAt(type, transform.position + new Vector3(Random.Range(0.3f, 1.2f), (i == 0 ? 1 : -1) * Random.Range(0.6f, 1.4f)));
            }

            if (!phase2) return;
            slamT -= dt;
            if (!slamWarned && slamT <= bd.slamWarning)
            {
                // telegraph: flash red + shake for 1 s before the slam
                slamWarned = true;
                warnT = bd.slamWarning;
                Sfx.Play("warn");
                b.Shake(0.07f, bd.slamWarning);
            }
            if (slamWarned) warnT -= dt;
            if (slamT <= 0)
            {
                slamT = bd.slamEvery;
                slamWarned = false;
                b.DamageBarricade(bd.slamDamage);
                b.Shake(0.35f, 0.5f);
                Sfx.Play("boom");
                Sfx.Vibrate();
            }
        }

        void UpdateFlash(float dt)
        {
            flashT -= dt;
            float amount = flashT > 0 ? 1f : 0f;
            Color c = flashColor;
            if (slamWarned && warnT > 0)
            {
                amount = 0.35f + 0.35f * Mathf.Sin(Time.time * 30f);
                c = new Color(1f, 0.15f, 0.1f);
            }
            sr.GetPropertyBlock(mpb);
            mpb.SetFloat("_Flash", amount);
            mpb.SetColor("_FlashColor", c);
            sr.SetPropertyBlock(mpb);
        }

        void UpdateOrder()
        {
            sr.sortingOrder = 1000 + Mathf.RoundToInt(-transform.position.y * 100f) + (IsBoss ? 50 : 0);
        }

        public void Hit(float damage)
        {
            if (state == State.Dead) return;
            Hp -= damage;
            flashT = 0.08f;
            flashColor = Color.white;
            if (Def.knockback && (state == State.Walk || state == State.AttackCat))
                transform.position += Vector3.right * 5f * GameData.Px;
            Sfx.Play("hit", 0.6f, 0.05f);
            if (Hp <= 0) Die();
        }

        public void Kill()
        {
            if (state != State.Dead) Die();
        }

        void Die()
        {
            state = State.Dead;
            Hp = 0;
            flashT = 0;
            slamWarned = false;
            UpdateFlash(0);
            b.OnEnemyKilled(this);
            Sfx.Play("death", 0.7f, 0.05f);
            Fx.Explosion(Center, Radius * 3.2f);
            Fx.Coin(Top);
            if (IsBoss) b.StartCoroutine(BossDeath(Center, Radius));
            anim.Play(design + "_dead", false, 1.5f, () =>
            {
                var f = gameObject.AddComponent<FadeOut>();
                f.Delay = 0.3f;
            });
        }

        IEnumerator BossDeath(Vector2 c, float r)
        {
            for (int i = 0; i < 3; i++)
            {
                Fx.Explosion(c + Random.insideUnitCircle * r, r * 3.5f);
                Sfx.Play("boom");
                b.Shake(0.3f, 0.3f);
                yield return new WaitForSeconds(0.4f);
            }
        }
    }
}
