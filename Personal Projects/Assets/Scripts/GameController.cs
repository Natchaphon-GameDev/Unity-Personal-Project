using UnityEngine;

namespace TaskbarHero
{
    /// <summary>
    /// Owns the <see cref="IdleRpg"/> sim lifecycle: builds it from the save file (or a
    /// fresh state), grants capped offline earnings, drives the per-frame tick, and
    /// autosaves periodically and on quit. Rendering lives in <see cref="IdleRpgView"/>,
    /// which this binds the sim to; window/UI orchestration lives in AppFlow.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        [SerializeField] IdleRpgConfig config;
        [SerializeField] IdleRpgView view;
        [SerializeField] SfxPlayer sfx;

        const float AutosaveInterval = 30f;

        public IdleRpg Game { get; private set; }
        public OfflineResult PendingOffline { get; private set; }
        public AppSettings Settings { get; private set; }

        IdleRpgRules rules;
        int rngSeed;
        float autosaveTimer;

        void Awake()
        {
            Settings = AppSettings.Load();
            AudioListener.volume = Settings.MasterVolume;

            rules = config != null ? config.ToRules() : new IdleRpgRules();

            if (SaveSystem.TryLoad(out var data))
            {
                rngSeed = data.rngSeed;
                Game = new IdleRpg(rules, data.sim, DeriveSeed(rngSeed, data.sim));
                double elapsed = SaveSystem.NowUnixUtc() - data.savedAtUnixUtc;
                PendingOffline = Game.FastForward(elapsed);
            }
            else
            {
                rngSeed = new System.Random().Next();
                Game = new IdleRpg(rules, IdleRpgState.NewGame(), rngSeed);
                PendingOffline = default;
            }

            if (view == null)
                view = FindFirstObjectByType<IdleRpgView>();
            if (view != null)
                view.Bind(Game);

            if (sfx == null)
                sfx = FindFirstObjectByType<SfxPlayer>();
            if (sfx != null)
                sfx.Bind(Game);
        }

        void Update()
        {
            if (Game == null)
                return;

            Game.Tick(Time.deltaTime);

            autosaveTimer += Time.deltaTime;
            if (autosaveTimer >= AutosaveInterval)
            {
                autosaveTimer = 0f;
                SaveNow();
            }
        }

        void OnApplicationQuit() => SaveNow();

        void OnApplicationPause(bool paused)
        {
            if (paused)
                SaveNow();
        }

        public void SaveNow()
        {
            if (Game != null)
                SaveSystem.Save(SaveData.FromGame(Game, rngSeed, SaveSystem.NowUnixUtc()));
        }

        // Vary the loot stream by progress so reloads don't replay the exact same drops,
        // while staying deterministic for a given (seed, stage).
        static int DeriveSeed(int baseSeed, IdleRpgState state) => baseSeed ^ (state != null ? state.Stage : 0);
    }
}
