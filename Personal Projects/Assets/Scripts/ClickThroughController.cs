using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TaskbarHero
{
    /// <summary>
    /// Per-frame during gameplay: makes the window interactive only when the cursor is
    /// over game content (a UI graphic or the monster collider), and click-through
    /// everywhere else so clicks on the transparent desktop area fall through. The cursor
    /// is polled via Win32 because a click-through window receives no Unity mouse input.
    /// AppFlow disables this component in settings mode (whole window interactive).
    /// </summary>
    public sealed class ClickThroughController : MonoBehaviour
    {
        [SerializeField] OverlayWindow overlay;
        [SerializeField] WindowDragController drag;

        readonly List<RaycastResult> raycastResults = new List<RaycastResult>();

        void Update()
        {
            if (overlay == null)
                return;

            // Never turn on click-through mid-drag, or the window would stop following the cursor.
            if (drag != null && drag.IsDragging)
            {
                overlay.SetClickThrough(false);
                return;
            }

            if (!overlay.TryGetCursorPosition(out int cx, out int cy) ||
                !overlay.TryGetWindowRect(out int left, out int top, out int right, out int bottom))
                return; // editor / no window: leave the state untouched

            if (cx < left || cx >= right || cy < top || cy >= bottom)
            {
                overlay.SetClickThrough(true); // cursor is off the window entirely
                return;
            }

            var screenPoint = new Vector2(cx - left, Screen.height - (cy - top));
            overlay.SetClickThrough(!HitsContent(screenPoint));
        }

        bool HitsContent(Vector2 screenPoint)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
                return true; // fail safe: capture rather than swallow the click

            var data = new PointerEventData(eventSystem) { position = screenPoint };
            raycastResults.Clear();
            eventSystem.RaycastAll(data, raycastResults);
            return raycastResults.Count > 0;
        }
    }
}
