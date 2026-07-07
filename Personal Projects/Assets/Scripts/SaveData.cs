using System;

namespace TaskbarHero
{
    /// <summary>
    /// The JSON progress file payload: a versioned wrapper around an
    /// <see cref="IdleRpgState"/> snapshot plus the metadata needed to compute offline
    /// earnings (when it was saved) and reproduce loot (the RNG seed).
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        public int version = SaveSystem.CurrentVersion;
        public long savedAtUnixUtc;
        public int rngSeed;
        public IdleRpgState sim;

        public static SaveData FromGame(IdleRpg game, int rngSeed, long nowUnixUtc) => new SaveData
        {
            version = SaveSystem.CurrentVersion,
            savedAtUnixUtc = nowUnixUtc,
            rngSeed = rngSeed,
            sim = game.CaptureState(),
        };
    }
}
