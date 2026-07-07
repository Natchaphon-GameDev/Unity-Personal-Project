using UnityEngine;

namespace TaskbarHero
{
    /// <summary>
    /// Drags the OS window by the raw cursor delta while the left mouse button is held.
    /// Uses a manual delta rather than Windows' HTCAPTION modal move loop so the game
    /// keeps rendering (and click-through keeps updating) during a drag. The physical
    /// button state is the release authority, since a fast drag can outrun the window and
    /// lose the mouse-up message. No-op in the editor (no OS window to move).
    /// </summary>
    public sealed class WindowDragController : MonoBehaviour
    {
        [SerializeField] OverlayWindow overlay;

        public bool IsDragging { get; private set; }

        int cursorStartX, cursorStartY, windowStartLeft, windowStartTop;

        /// <summary>Called by <see cref="DragHandle"/> when the strip is pressed.</summary>
        public void BeginDrag()
        {
            if (overlay == null ||
                !overlay.TryGetCursorPosition(out cursorStartX, out cursorStartY) ||
                !overlay.TryGetWindowRect(out windowStartLeft, out windowStartTop, out _, out _))
                return;

            IsDragging = true;
        }

        void Update()
        {
            if (!IsDragging)
                return;

            if (overlay == null || !overlay.IsLeftMouseDown())
            {
                IsDragging = false;
                return;
            }

            if (overlay.TryGetCursorPosition(out int cx, out int cy))
                overlay.MoveTo(windowStartLeft + (cx - cursorStartX), windowStartTop + (cy - cursorStartY));
        }
    }
}
