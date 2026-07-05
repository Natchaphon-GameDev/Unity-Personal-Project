using UnityEngine;

namespace TaskbarHero
{
    /// <summary>
    /// Spins the object it is attached to. Exists only to prove the overlay
    /// is alive on first Windows test: if the cube on the taskbar rotates,
    /// rendering, transparency and Run In Background are all working.
    /// </summary>
    public class Rotator : MonoBehaviour
    {
        [SerializeField] float degreesPerSecond = 90f;
        [SerializeField] Vector3 axis = Vector3.up;

        void Update()
        {
            transform.Rotate(axis, degreesPerSecond * Time.deltaTime);
        }
    }
}
