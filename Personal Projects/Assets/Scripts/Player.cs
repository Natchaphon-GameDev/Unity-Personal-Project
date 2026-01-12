using System;
using System.Collections.Generic;
using UnityEngine;

namespace DefaultNamespace {
    public class Player : MonoBehaviour {
        [SerializeReference] private PlayerInput input;
        [SerializeReference] private MonoBehaviour weapon;

        private IWeapon _weapon;
        private IDamageable _target;

        private void Awake() {
            _weapon = weapon as IWeapon;

            if (_weapon == null) {
                Debug.LogError("Weapon does not implement IWeapon interface");
            }

            input = GetComponent<PlayerInput>();
        }

        private void OnEnable() => input.AttackPressed += OnAttack;
        private void OnDisable() => input.AttackPressed -= OnAttack;

        private void OnAttack() {
            // _target = Registry<IDamageable>.GetFirst();
            _target = Registry<IDamageable>.Get(GetClosestTarget);
            _weapon?.Attack(_target);
        }

        private IDamageable GetClosestTarget(IEnumerable<IDamageable> targets) {
            IDamageable closestTarget = null;
            var closestDistance = float.MaxValue;

            foreach (var target in targets) {
                if (target == null) continue;
                if (target is not Component component) continue;
                var distance = Vector3.Distance(transform.position, component.transform.position);
                if (!(distance < closestDistance)) continue;
                closestDistance = distance;
                closestTarget = target;
            }

            return closestTarget;
        }
    }
}