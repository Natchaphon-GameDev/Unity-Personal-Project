using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DefaultNamespace {
    public class PlayerInput : MonoBehaviour {
        public event Action AttackPressed;

        private void Update() {
            if (Keyboard.current.spaceKey.wasPressedThisFrame) {
                AttackPressed?.Invoke();
            }
        }
    }
}