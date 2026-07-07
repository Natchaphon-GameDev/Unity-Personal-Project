namespace TaskbarHero
{
    /// <summary>
    /// Pure-C# idle-RPG simulation: a hero auto-attacks the current monster on a
    /// fixed cadence; defeating a monster grants gold + XP and spawns a tougher
    /// one; accumulated XP levels the hero up, raising attack. No Unity
    /// dependencies and no randomness, so it is fully deterministic and unit-
    /// testable outside play mode (see Tests/IdleRpgTests.cs).
    ///
    /// "Pure idle": the hero cannot lose. The only feedback loop is time -> power.
    /// </summary>
    public sealed class IdleRpg
    {
        readonly IdleRpgRules rules;
        float attackTimer;

        public int Level { get; private set; }
        public int Attack { get; private set; }
        public long Gold { get; private set; }
        public int Xp { get; private set; }
        public int XpToNext { get; private set; }

        /// <summary>1-based index of the monster currently being fought.</summary>
        public int Stage { get; private set; }
        public int MonsterMaxHp { get; private set; }
        public int MonsterHp { get; private set; }

        public float MonsterHpFraction => MonsterMaxHp <= 0 ? 0f : (float)MonsterHp / MonsterMaxHp;
        public float XpFraction => XpToNext <= 0 ? 0f : (float)Xp / XpToNext;

        public IdleRpg(IdleRpgRules rules)
        {
            this.rules = rules;
            Level = 1;
            Attack = rules.BaseAttack;
            XpToNext = rules.XpToNextForLevel(Level);
            Stage = 1;
            SpawnMonster();
        }

        /// <summary>Advance the fight by <paramref name="deltaSeconds"/> of real time.</summary>
        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f)
                return;

            attackTimer += deltaSeconds;

            // Cap catch-up so one oversized delta can't resolve a huge number of hits.
            float maxTimer = rules.AttackInterval * rules.MaxCatchUpHits;
            if (attackTimer > maxTimer)
                attackTimer = maxTimer;

            while (attackTimer >= rules.AttackInterval)
            {
                attackTimer -= rules.AttackInterval;
                Strike();
            }
        }

        void Strike()
        {
            MonsterHp -= Attack;
            if (MonsterHp > 0)
                return;

            // Monster defeated: collect the reward, then advance to a tougher one.
            Gold += rules.GoldForStage(Stage);
            GainXp(rules.XpForStage(Stage));
            Stage++;
            SpawnMonster();
        }

        void GainXp(int amount)
        {
            Xp += amount;
            while (Xp >= XpToNext)
            {
                Xp -= XpToNext;
                Level++;
                Attack += rules.AttackPerLevel;
                XpToNext = rules.XpToNextForLevel(Level);
            }
        }

        void SpawnMonster()
        {
            MonsterMaxHp = rules.MonsterHpForStage(Stage);
            MonsterHp = MonsterMaxHp;
        }
    }
}
