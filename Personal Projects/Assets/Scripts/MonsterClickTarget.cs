using UnityEngine;
using UnityEngine.EventSystems;

namespace TaskbarHero
{
    /// <summary>
    /// Added to the monster sprite at runtime by <see cref="IdleRpgView"/>. A click deals
    /// a bonus tap hit (routed through the sim's <see cref="IdleRpg.TapStrike"/> so it's
    /// testable) and plays the tap sound. Needs a Collider2D on the same object plus a
    /// Physics2DRaycaster on the camera.
    /// </summary>
    public sealed class MonsterClickTarget : MonoBehaviour, IPointerClickHandler
    {
        IdleRpgView view;
        SfxPlayer sfx;

        public void Setup(IdleRpgView owner)
        {
            view = owner;
            sfx = FindFirstObjectByType<SfxPlayer>();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            var game = view != null ? view.Game : null;
            if (game == null || !game.TapStrike(out bool crit))
                return;

            view.PulseMonster(crit);
            if (sfx != null)
                sfx.PlayTap(crit);
        }
    }
}
