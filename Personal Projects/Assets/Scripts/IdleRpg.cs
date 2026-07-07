using System;

namespace TaskbarHero
{
    /// <summary>
    /// Pure-C# idle-RPG simulation. A hero auto-attacks the current monster on a fixed
    /// cadence; defeating a monster grants gold + XP, may drop equipment, and spawns a
    /// tougher one. Every Nth stage is a timed boss. The player can also tap to deal
    /// bonus damage. No Unity dependencies and the only randomness comes from an
    /// injected seed, so the whole thing is deterministic and unit-testable outside
    /// play mode (see Tests/).
    ///
    /// Observable values that are fully derived (Attack, MonsterMaxHp, XpToNext) are
    /// recomputed from level, equipment and stage rather than stored, so a restored
    /// save can never disagree with the current rules.
    /// </summary>
    public sealed class IdleRpg
    {
        const int DefaultSeed = 12345;

        readonly IdleRpgRules rules;
        readonly Random rng;
        readonly EquipmentItem[] equippedBySlot = new EquipmentItem[3];

        float attackTimer;
        float bossTimeRemaining;
        float tapCooldownRemaining;
        bool suppressEvents;

        public int Level { get; private set; }
        public long Gold { get; private set; }
        public int Xp { get; private set; }
        public int XpToNext { get; private set; }

        /// <summary>1-based index of the monster currently being fought.</summary>
        public int Stage { get; private set; }
        public int MonsterMaxHp { get; private set; }
        public int MonsterHp { get; private set; }

        public bool InBossFight { get; private set; }
        public float BossTimeRemaining => bossTimeRemaining;

        /// <summary>Current attack, derived from level and equipped attack-percent bonuses.</summary>
        public int Attack => ComputeAttack();

        public float MonsterHpFraction => MonsterMaxHp <= 0 ? 0f : (float)MonsterHp / MonsterMaxHp;
        public float XpFraction => XpToNext <= 0 ? 0f : (float)Xp / XpToNext;

        // Feedback hooks (view/audio subscribe). Never fired while fast-forwarding offline.
        public event Action OnHit;
        public event Action OnKill;
        public event Action OnLevelUp;
        public event Action OnBossStarted;
        public event Action OnBossWon;
        public event Action OnBossFailed;
        public event Action<EquipmentItem, bool> OnLoot; // (item, wasEquipped)

        public IdleRpg(IdleRpgRules rules) : this(rules, IdleRpgState.NewGame(), DefaultSeed) { }

        /// <summary>Restore (or start) a game from a state snapshot with a given RNG seed.</summary>
        public IdleRpg(IdleRpgRules rules, IdleRpgState state, int rngSeed)
        {
            this.rules = rules;
            rng = new Random(rngSeed);

            Level = Math.Max(1, state.Level);
            Xp = Math.Max(0, state.Xp);
            Gold = state.Gold;
            Stage = Math.Max(1, state.Stage);
            XpToNext = rules.XpToNextForLevel(Level);

            if (state.Equipped != null)
                foreach (var item in state.Equipped)
                    if (item != null)
                        equippedBySlot[(int)item.Slot] = item;

            InBossFight = state.InBossFight;
            bossTimeRemaining = state.BossTimeRemaining;
            MonsterMaxHp = ComputeMonsterMaxHp(Stage, InBossFight);
            MonsterHp = state.MonsterHp > 0 ? Math.Min(state.MonsterHp, MonsterMaxHp) : MonsterMaxHp;
        }

        /// <summary>Snapshot the earned/chosen state so it can be saved and later restored.</summary>
        public IdleRpgState CaptureState()
        {
            var state = new IdleRpgState
            {
                Level = Level,
                Xp = Xp,
                Gold = Gold,
                Stage = Stage,
                MonsterHp = MonsterHp,
                InBossFight = InBossFight,
                BossTimeRemaining = bossTimeRemaining,
            };
            foreach (var item in equippedBySlot)
                if (item != null)
                    state.Equipped.Add(item);
            return state;
        }

        /// <summary>Advance the fight by <paramref name="deltaSeconds"/> of real time.</summary>
        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f)
                return;

            if (tapCooldownRemaining > 0f)
                tapCooldownRemaining = Math.Max(0f, tapCooldownRemaining - deltaSeconds);

            attackTimer += deltaSeconds;

            // Cap catch-up so one oversized delta can't resolve a huge number of hits.
            float maxTimer = rules.AttackInterval * rules.MaxCatchUpHits;
            if (attackTimer > maxTimer)
                attackTimer = maxTimer;

            while (attackTimer >= rules.AttackInterval)
            {
                attackTimer -= rules.AttackInterval;

                if (InBossFight)
                {
                    bossTimeRemaining -= rules.AttackInterval;
                    if (bossTimeRemaining <= 0f)
                    {
                        BossFailed();
                        continue;
                    }
                }

                ApplyDamage(Attack);
            }
        }

        /// <summary>Player-triggered bonus hit. Returns false (and does nothing) while on cooldown.</summary>
        public bool TapStrike()
        {
            if (tapCooldownRemaining > 0f)
                return false;

            tapCooldownRemaining = rules.TapCooldownSeconds;
            ApplyDamage((int)(Attack * rules.TapDamageMultiplier));
            return true;
        }

        /// <summary>
        /// Simulate elapsed offline time, clamped to <see cref="IdleRpgRules.OfflineCapSeconds"/>.
        /// Events are suppressed so a few hours of catch-up don't fire thousands of callbacks.
        /// </summary>
        public OfflineResult FastForward(double elapsedSeconds)
        {
            double clamped = Math.Max(0.0, Math.Min(elapsedSeconds, rules.OfflineCapSeconds));
            long goldBefore = Gold;
            int stageBefore = Stage;
            int levelBefore = Level;

            suppressEvents = true;
            int steps = (int)(clamped / rules.AttackInterval);
            for (int i = 0; i < steps; i++)
                Tick(rules.AttackInterval);
            suppressEvents = false;

            return new OfflineResult
            {
                GoldGained = Gold - goldBefore,
                StagesGained = Stage - stageBefore,
                LevelsGained = Level - levelBefore,
                SecondsSimulated = clamped,
            };
        }

        void ApplyDamage(int amount)
        {
            MonsterHp -= amount;
            Raise(OnHit);
            if (MonsterHp > 0)
                return;

            ResolveKill();
        }

        void ResolveKill()
        {
            bool wasBoss = InBossFight;

            long gold = StageGold(Stage);
            if (wasBoss)
                gold = (long)Math.Round(gold * (double)rules.BossGoldMultiplier);
            Gold += gold;

            GainXp(rules.XpForStage(Stage));
            RollLoot();

            if (wasBoss)
            {
                InBossFight = false;
                Raise(OnBossWon);
            }
            else
            {
                Raise(OnKill);
            }

            Stage++;
            SpawnMonster();
        }

        void BossFailed()
        {
            InBossFight = false;
            Raise(OnBossFailed);
            Stage = Math.Max(1, Stage - 1);
            SpawnMonster();
        }

        void GainXp(int amount)
        {
            Xp += amount;
            while (Xp >= XpToNext)
            {
                Xp -= XpToNext;
                Level++;
                XpToNext = rules.XpToNextForLevel(Level);
                Raise(OnLevelUp);
            }
        }

        void RollLoot()
        {
            if (rules.DropChance <= 0f || rng.NextDouble() >= rules.DropChance)
                return;

            var item = LootTable.Roll(rules, Stage, rng);
            var current = equippedBySlot[(int)item.Slot];

            bool equipped;
            if (current == null || item.PowerScore > current.PowerScore)
            {
                if (current != null)
                    Gold += current.SellValue(rules); // sell the item we're replacing
                equippedBySlot[(int)item.Slot] = item;
                equipped = true;
            }
            else
            {
                Gold += item.SellValue(rules);
                equipped = false;
            }

            if (!suppressEvents)
                OnLoot?.Invoke(item, equipped);
        }

        void SpawnMonster()
        {
            InBossFight = IsBossStage(Stage);
            MonsterMaxHp = ComputeMonsterMaxHp(Stage, InBossFight);
            MonsterHp = MonsterMaxHp;

            if (InBossFight)
            {
                bossTimeRemaining = rules.BossTimeLimitSeconds;
                Raise(OnBossStarted);
            }
        }

        int ComputeAttack()
        {
            int baseAttack = rules.BaseAttack + (Level - 1) * rules.AttackPerLevel;
            double mult = 1.0 + TotalStatPercent(StatKind.AttackPercent) / 100.0;
            return (int)(baseAttack * mult);
        }

        long StageGold(int stage)
        {
            double mult = 1.0 + TotalStatPercent(StatKind.GoldFindPercent) / 100.0;
            return (long)Math.Round(rules.GoldForStage(stage) * mult, MidpointRounding.AwayFromZero);
        }

        int TotalStatPercent(StatKind kind)
        {
            int sum = 0;
            foreach (var item in equippedBySlot)
                if (item != null && item.Stat == kind)
                    sum += item.MagnitudePercent;
            return sum;
        }

        int ComputeMonsterMaxHp(int stage, bool boss)
        {
            int hp = rules.MonsterHpForStage(stage);
            if (boss)
                hp = (int)(hp * rules.BossHpMultiplier);
            return hp;
        }

        bool IsBossStage(int stage) => rules.BossEveryNStages > 0 && stage % rules.BossEveryNStages == 0;

        void Raise(Action e)
        {
            if (!suppressEvents)
                e?.Invoke();
        }
    }
}
