using UnityEngine;
using UnityEngine.EventSystems;

namespace TaskbarHero
{
    /// <summary>
    /// Put on the HUD strip background: pressing it starts a window drag. Because it's a
    /// UGUI raycast target, presses on the buttons layered above it don't reach here, so
    /// only the empty strip area drags the window.
    /// </summary>
    public sealed class DragHandle : MonoBehaviour, IPointerDownHandler
    {
        [SerializeField] WindowDragController controller;

        void Awake()
        {
            if (controller == null)
                controller = FindFirstObjectByType<WindowDragController>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (controller != null)
                controller.BeginDrag();
        }
    }
}
