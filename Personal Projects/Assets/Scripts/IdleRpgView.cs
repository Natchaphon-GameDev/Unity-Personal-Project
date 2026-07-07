using UnityEngine;

namespace TaskbarHero
{
    /// <summary>
    /// Renders and drives the <see cref="IdleRpg"/> simulation on the thin taskbar
    /// strip. Builds its own minimal visuals in code (tinted unit-square sprites
    /// plus a text label) so it just works when dropped onto a GameObject with an
    /// orthographic camera — no manual inspector wiring, which matters because the
    /// scene is generated headlessly. Runs in the editor on any platform; the Win32
    /// overlay only changes *where* this is drawn, not the game itself.
    ///
    /// Layout is tuned for an orthographic camera of size ~1 at the origin: hero on
    /// the left, monster on the right, monster HP bar and a stats label between them.
    /// </summary>
    public sealed class IdleRpgView : MonoBehaviour
    {
        [SerializeField] IdleRpgConfig config;

        IdleRpg game;
        bool ownsGame;   // true only for the standalone fallback below (no GameController bound it)
        Transform hero;
        Transform monster;
        Transform hpFillPivot;   // scales on X (0..1) to show remaining monster HP

        int lastStage;
        float heroBobPhase;
        float monsterSpawnFlash;

        static Sprite squareSprite;

        /// <summary>The sim being rendered (bound by GameController, or self-built as a fallback).</summary>
        public IdleRpg Game => game;

        void Awake()
        {
            BuildVisuals();
        }

        void Start()
        {
            // If no GameController bound a sim (e.g. running this scene object on its own
            // in the editor), build a throwaway one so the view still shows a live game.
            if (game == null)
            {
                var rules = config != null ? config.ToRules() : new IdleRpgRules();
                game = new IdleRpg(rules);
                ownsGame = true;
            }

            lastStage = game.Stage;
            Refresh();
        }

        /// <summary>Render the given sim. GameController owns its ticking, so the view won't tick it.</summary>
        public void Bind(IdleRpg boundGame)
        {
            game = boundGame;
            ownsGame = false;
            lastStage = boundGame.Stage;
        }

        void Update()
        {
            if (game == null)
                return;

            if (ownsGame)
                game.Tick(Time.deltaTime);

            // Hero hops in place on its attack cadence for a sign of life.
            heroBobPhase += Time.deltaTime * 9f;
            if (hero != null)
                hero.localPosition = new Vector3(-2.2f, 0.12f * Mathf.Abs(Mathf.Sin(heroBobPhase)), 0f);

            // Brief pop when a fresh (tougher) monster spawns.
            if (game.Stage != lastStage)
            {
                lastStage = game.Stage;
                monsterSpawnFlash = 1f;
            }
            if (monster != null)
            {
                monsterSpawnFlash = Mathf.MoveTowards(monsterSpawnFlash, 0f, Time.deltaTime * 3f);
                float s = 1.4f + 0.35f * monsterSpawnFlash;
                monster.localScale = new Vector3(s, s, 1f);
            }

            Refresh();
        }

        /// <summary>Brief scale pop on the monster, reused for tap feedback. Crits pop harder.</summary>
        public void PulseMonster(bool crit = false) =>
            monsterSpawnFlash = Mathf.Max(monsterSpawnFlash, crit ? 1f : 0.5f);

        void Refresh()
        {
            if (hpFillPivot != null)
            {
                var scale = hpFillPivot.localScale;
                scale.x = Mathf.Clamp01(game.MonsterHpFraction);
                hpFillPivot.localScale = scale;
            }
        }

        void BuildVisuals()
        {
            // Semi-transparent "card" body behind everything: it's the visible widget
            // background AND the drag surface. Drawn as a world sprite (not a UGUI image)
            // so the hero/monster stay crisp on top of it and the desktop shows through
            // its alpha. Its 2D collider (behind the monster's) makes empty body area drag
            // the window while the monster still takes taps.
            var body = MakeSprite("Body", new Color(0.06f, 0.06f, 0.08f, 0.55f), -2)
                .Placed(new Vector3(0f, 0f, 0f), 1f);
            body.localScale = new Vector3(5.7f, 1.8f, 1f);
            body.gameObject.AddComponent<BoxCollider2D>().size = Vector2.one;
            body.gameObject.AddComponent<DragHandle>();

            hero = MakeSprite("Hero", new Color(0.35f, 0.65f, 1f), 0)
                .Placed(new Vector3(-2.2f, 0f, 0f), 1.4f);

            monster = MakeSprite("Monster", new Color(0.9f, 0.3f, 0.35f), 0)
                .Placed(new Vector3(2.2f, 0f, 0f), 1.4f);

            // Make the monster clickable (tap-to-attack). Collider is in the sprite's local
            // space; the 1.4 scale makes it a ~1.4-unit box that a Physics2DRaycaster can hit.
            monster.gameObject.AddComponent<BoxCollider2D>().size = Vector2.one;
            monster.gameObject.AddComponent<MonsterClickTarget>().Setup(this);

            // Monster HP bar: dark background + a green fill anchored to its left edge.
            const float barWidth = 3.2f;
            const float barHeight = 0.26f;
            var barCenter = new Vector3(2.2f, 0.98f, 0f);

            var bg = MakeSprite("HpBarBg", new Color(0.1f, 0.1f, 0.12f), 1).transform;
            bg.localPosition = barCenter;
            bg.localScale = new Vector3(barWidth, barHeight, 1f);

            // Pivot sits at the bar's left edge; scaling it on X grows/shrinks the
            // fill from the left instead of from the centre.
            var pivotGo = new GameObject("HpFillPivot");
            pivotGo.transform.SetParent(transform, false);
            pivotGo.transform.localPosition = new Vector3(barCenter.x - barWidth * 0.5f, barCenter.y, 0f);
            hpFillPivot = pivotGo.transform;

            var fill = MakeSprite("HpFill", new Color(0.35f, 0.85f, 0.35f), 2).transform;
            fill.SetParent(hpFillPivot, false);
            fill.localPosition = new Vector3(barWidth * 0.5f, 0f, 0f); // left edge at the pivot
            fill.localScale = new Vector3(barWidth, barHeight * 0.7f, 1f);
        }

        Transform MakeSprite(string name, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = UnitSquare();
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            return go.transform;
        }

        static Sprite UnitSquare()
        {
            if (squareSprite == null)
            {
                // whiteTexture is always available at runtime; PPU = width => 1 world unit.
                var tex = Texture2D.whiteTexture;
                squareSprite = Sprite.Create(
                    tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
            }
            return squareSprite;
        }
    }

    static class TransformPlacement
    {
        /// <summary>Small fluent helper: position a freshly-made sprite and give it a uniform 2D scale.</summary>
        public static Transform Placed(this Transform t, Vector3 localPosition, float scale)
        {
            t.localPosition = localPosition;
            t.localScale = new Vector3(scale, scale, 1f);
            return t;
        }
    }
}
