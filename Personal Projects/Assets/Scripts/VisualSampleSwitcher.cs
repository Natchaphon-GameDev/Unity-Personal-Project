using UnityEngine;
using UnityEngine.InputSystem;

namespace TaskbarHero
{
    /// <summary>
    /// Simple config to switch between the 3D and 2D sample visuals.
    /// Pick the mode in the inspector, or press Tab at runtime — the overlay
    /// window must have keyboard focus for that (click it first).
    /// </summary>
    public class VisualSampleSwitcher : MonoBehaviour
    {
        public enum SampleMode
        {
            Sample3D,
            Sample2D
        }

        [SerializeField] SampleMode mode = SampleMode.Sample3D;
        [SerializeField] GameObject sample3D;
        [SerializeField] GameObject sample2D;

        void Awake()
        {
            Apply();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame)
            {
                mode = mode == SampleMode.Sample3D ? SampleMode.Sample2D : SampleMode.Sample3D;
                Apply();
            }
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            // SetActive is not allowed directly inside OnValidate; defer it.
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null)
                    Apply();
            };
        }
#endif

        void Apply()
        {
            if (sample3D != null)
                sample3D.SetActive(mode == SampleMode.Sample3D);
            if (sample2D != null)
                sample2D.SetActive(mode == SampleMode.Sample2D);
        }
    }
}
