using System;
using UnityEngine;

namespace DefaultNamespace {
    public class Enemy : MonoBehaviour, IDamageable {
        private int _health = 100;

        private void Awake() => Registry<IDamageable>.TryAdd(this);

        private void Destroy() => Registry<IDamageable>.Remove(this);

        public void TakeDamage(int damage) {
            _health -= damage;
            Debug.Log($"Enemy took {damage} damage, remaining health: {_health}");
        }
    }
}