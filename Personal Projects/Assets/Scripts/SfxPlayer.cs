using UnityEngine;

namespace TaskbarHero
{
    /// <summary>
    /// Plays synthesized sound effects in response to sim events. Clips are built in code
    /// from <see cref="SfxSynth"/> so no audio assets are needed. Master volume is handled
    /// globally by <see cref="AudioListener.volume"/>, which GameController drives from
    /// settings. Events are already suppressed during offline fast-forward, so loading a
    /// save doesn't trigger a burst of sound.
    /// </summary>
    public sealed class SfxPlayer : MonoBehaviour
    {
        const float MinKillSoundInterval = 0.18f; // rate-limit the frequent kill blip

        AudioSource source;
        AudioClip killClip, levelUpClip, bossClip, bossWinClip, lootClip, tapClip;
        IdleRpg boundGame;
        float lastKillSoundTime = -99f;

        void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;

            int sr = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : SfxSynth.DefaultSampleRate;
            killClip = Clip("sfx_kill", SfxSynth.Blip(660f, 0.05f, 0.25f, 28f, sr), sr);
            levelUpClip = Clip("sfx_levelup", SfxSynth.Arpeggio(new[] { 523f, 659f, 784f }, 0.06f, 0.4f, 12f, sr), sr);
            bossClip = Clip("sfx_boss", SfxSynth.Chirp(170f, 90f, 0.5f, 0.5f, 3f, sr), sr);
            bossWinClip = Clip("sfx_bosswin", SfxSynth.Arpeggio(new[] { 523f, 784f, 1047f }, 0.08f, 0.45f, 8f, sr), sr);
            lootClip = Clip("sfx_loot", SfxSynth.Arpeggio(new[] { 880f, 1175f }, 0.05f, 0.35f, 16f, sr), sr);
            tapClip = Clip("sfx_tap", SfxSynth.Blip(1200f, 0.03f, 0.35f, 40f, sr), sr);
        }

        void OnDestroy() => Unbind();

        public void Bind(IdleRpg game)
        {
            Unbind();
            boundGame = game;
            if (game == null)
                return;

            game.OnKill += PlayKill;
            game.OnLevelUp += PlayLevelUp;
            game.OnBossStarted += PlayBossStarted;
            game.OnBossWon += PlayBossWon;
            game.OnLoot += PlayLoot;
        }

        /// <summary>Called by the monster click handler when a tap actually lands.</summary>
        public void PlayTap() => source.PlayOneShot(tapClip);

        void Unbind()
        {
            if (boundGame == null)
                return;

            boundGame.OnKill -= PlayKill;
            boundGame.OnLevelUp -= PlayLevelUp;
            boundGame.OnBossStarted -= PlayBossStarted;
            boundGame.OnBossWon -= PlayBossWon;
            boundGame.OnLoot -= PlayLoot;
            boundGame = null;
        }

        void PlayKill()
        {
            if (Time.unscaledTime - lastKillSoundTime < MinKillSoundInterval)
                return;
            lastKillSoundTime = Time.unscaledTime;
            source.PlayOneShot(killClip);
        }

        void PlayLevelUp() => source.PlayOneShot(levelUpClip);
        void PlayBossStarted() => source.PlayOneShot(bossClip);
        void PlayBossWon() => source.PlayOneShot(bossWinClip);
        void PlayLoot(EquipmentItem item, bool equipped) => source.PlayOneShot(lootClip);

        static AudioClip Clip(string name, float[] data, int sampleRate)
        {
            var clip = AudioClip.Create(name, data.Length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
