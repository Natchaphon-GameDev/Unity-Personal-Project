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
    ///
    /// If pixel-art skins exist under Resources/TaskbarHero (hero, monster_0..N,
    /// boss — CC0 sprites from Kenney's Tiny Dungeon), they replace the tinted
    /// squares: the monster skin cycles with the stage and bosses get their own
    /// bulkier look. With no skins present the colored-square fallback still works.
    /// </summary>
    public sealed class IdleRpgView : MonoBehaviour
    {
        [SerializeField] IdleRpgConfig config;

        IdleRpg game;
        bool ownsGame;   // true only for the standalone fallback below (no GameController bound it)
        Transform hero;
        Transform monster;
        Transform hpFillPivot;   // scales on X (0..1) to show remaining monster HP

        SpriteRenderer monsterRenderer;
        SpriteRenderer hpFillRenderer;
        Sprite[] monsterSkins;   // per-stage look, cycled by stage number
        Sprite bossSkin;
        float monsterBaseScale = UnitScale;

        // Characters stand on the tiled ground strip; its top edge is the shared floor line.
        const float GroundTopY = -0.55f;
        const float GroundTileSize = 0.5f;
        const float HeroX = -2.2f;
        // 2.0 (not further right): keeps the boss inside the +3.0 world-unit strip edge
        // and keeps the HP bar clear of the gear/close buttons in the top-right corner.
        const float MonsterX = 2.0f;
        const float UnitScale = 1.25f;
        const float BossScale = 1.45f;

        static readonly Color HpFillColor = UiTheme.HpGreen;
        static readonly Color BossHpFillColor = UiTheme.HpRed;

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
            ApplyMonsterLook();
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
                hero.localPosition = new Vector3(
                    HeroX, FootedY(UnitScale) + 0.12f * Mathf.Abs(Mathf.Sin(heroBobPhase)), 0f);

            // Brief pop when a fresh (tougher) monster spawns.
            if (game.Stage != lastStage)
            {
                lastStage = game.Stage;
                monsterSpawnFlash = 1f;
                ApplyMonsterLook();
            }
            if (monster != null)
            {
                monsterSpawnFlash = Mathf.MoveTowards(monsterSpawnFlash, 0f, Time.deltaTime * 3f);
                float s = monsterBaseScale + 0.35f * monsterSpawnFlash;
                monster.localScale = new Vector3(s, s, 1f);
                // Keep the feet pinned to the ground line while the scale pops.
                monster.localPosition = new Vector3(MonsterX, FootedY(s), 0f);
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
            if (hpFillRenderer != null)
                hpFillRenderer.color = game.InBossFight ? BossHpFillColor : HpFillColor;
        }

        /// <summary>Swap the monster's skin for the current stage; bosses get their own skin and extra bulk.</summary>
        void ApplyMonsterLook()
        {
            bool boss = game != null && game.InBossFight;
            monsterBaseScale = boss ? BossScale : UnitScale;

            if (monsterRenderer == null)
                return;
            Sprite skin = boss && bossSkin != null
                ? bossSkin
                : monsterSkins.Length > 0 ? monsterSkins[(game.Stage - 1) % monsterSkins.Length] : null;
            if (skin != null)
            {
                monsterRenderer.sprite = skin;
                monsterRenderer.color = Color.white;
            }
        }

        void BuildVisuals()
        {
            var heroSkin = Resources.Load<Sprite>("TaskbarHero/hero");
            bossSkin = Resources.Load<Sprite>("TaskbarHero/boss");
            monsterSkins = LoadMonsterSkins();

            // Near-invisible dark veil behind everything: it's the drag surface, kept
            // subtle so the battle reads as standing directly on the desktop. Drawn as a
            // world sprite (not a UGUI image) so the desktop shows through its alpha. Its
            // 2D collider (behind the monster's) makes empty body area drag the window
            // while the monster still takes taps.
            var body = MakeSprite("Body", new Color(0.05f, 0.05f, 0.07f, 0f), -3)
                .Placed(new Vector3(0f, 0f, 0f), 1f);
            body.localScale = new Vector3(5.7f, 1.8f, 1f);
            body.gameObject.AddComponent<BoxCollider2D>().size = Vector2.one;
            body.gameObject.AddComponent<DragHandle>();

            BuildGround();

            hero = MakeSprite("Hero", heroSkin != null ? Color.white : new Color(0.35f, 0.65f, 1f), 0, heroSkin)
                .Placed(new Vector3(HeroX, FootedY(UnitScale), 0f), UnitScale);

            monster = MakeSprite("Monster", new Color(0.9f, 0.3f, 0.35f), 0)
                .Placed(new Vector3(MonsterX, FootedY(UnitScale), 0f), UnitScale);
            monsterRenderer = monster.GetComponent<SpriteRenderer>();

            // Make the monster clickable (tap-to-attack). Collider is in the sprite's local
            // space; the 1.4 scale makes it a ~1.4-unit box that a Physics2DRaycaster can hit.
            monster.gameObject.AddComponent<BoxCollider2D>().size = Vector2.one;
            monster.gameObject.AddComponent<MonsterClickTarget>().Setup(this);

            BuildMonsterHpBar();
        }

        /// <summary>Sprite-center Y that puts a unit's feet on the ground line at the given scale.</summary>
        static float FootedY(float scale) => GroundTopY + scale * 0.5f;

        /// <summary>
        /// Tiled stone-slab ground strip along the bottom of the play area, alternating
        /// the two Tiny Dungeon floor tiles. Skipped when the tiles aren't present (the
        /// characters then float over the old plain card, which still works).
        /// </summary>
        void BuildGround()
        {
            var tiles = new[]
            {
                Resources.Load<Sprite>("TaskbarHero/ground_0"),
                Resources.Load<Sprite>("TaskbarHero/ground_1"),
            };
            if (tiles[0] == null)
                return;

            int count = Mathf.CeilToInt(6f / GroundTileSize) + 1;
            float y = GroundTopY - GroundTileSize * 0.5f;
            for (int i = 0; i < count; i++)
            {
                var tile = tiles[i % 2] != null ? tiles[i % 2] : tiles[0];
                float x = -3f + GroundTileSize * (0.5f + i);
                MakeSprite($"Ground_{i}", Color.white, -2, tile)
                    .Placed(new Vector3(x, y, 0f), GroundTileSize);
            }
        }

        /// <summary>
        /// Small framed HP bar floating above the monster: near-black frame, dark well,
        /// bright fill — sized to the character rather than the whole strip.
        /// </summary>
        void BuildMonsterHpBar()
        {
            const float barWidth = 1.0f;
            const float barHeight = 0.12f;
            // Just above the monster's visible head (the 16px source sprites carry some
            // transparent padding), and below the stacked corner buttons.
            var barCenter = new Vector3(MonsterX, 0.62f, 0f);

            var frame = MakeSprite("HpBarFrame", UiTheme.Outline, 1).transform;
            frame.localPosition = barCenter;
            frame.localScale = new Vector3(barWidth + 0.06f, barHeight + 0.06f, 1f);

            var bg = MakeSprite("HpBarBg", UiTheme.BarBack, 2).transform;
            bg.localPosition = barCenter;
            bg.localScale = new Vector3(barWidth, barHeight, 1f);

            // Pivot sits at the bar's left edge; scaling it on X grows/shrinks the
            // fill from the left instead of from the centre.
            var pivotGo = new GameObject("HpFillPivot");
            pivotGo.transform.SetParent(transform, false);
            pivotGo.transform.localPosition = new Vector3(barCenter.x - barWidth * 0.5f, barCenter.y, 0f);
            hpFillPivot = pivotGo.transform;

            var fill = MakeSprite("HpFill", HpFillColor, 3).transform;
            fill.SetParent(hpFillPivot, false);
            fill.localPosition = new Vector3(barWidth * 0.5f, 0f, 0f); // left edge at the pivot
            fill.localScale = new Vector3(barWidth, barHeight - 0.04f, 1f);
            hpFillRenderer = fill.GetComponent<SpriteRenderer>();
        }

        Transform MakeSprite(string name, Color color, int sortingOrder, Sprite sprite = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite != null ? sprite : UnitSquare();
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            return go.transform;
        }

        static Sprite[] LoadMonsterSkins()
        {
            var skins = new System.Collections.Generic.List<Sprite>();
            for (int i = 0; ; i++)
            {
                var skin = Resources.Load<Sprite>($"TaskbarHero/monster_{i}");
                if (skin == null)
                    break;
                skins.Add(skin);
            }
            return skins.ToArray();
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
