using System;
using UnityEngine;

namespace DefaultNamespace {
    public class Weapon : MonoBehaviour,IWeapon {
        [SerializeField] private WeaponConfig config;
        
        private WeaponLogic _logic;

        private void Awake() => _logic = new WeaponLogic(config);
        public void Attack(IDamageable target) => _logic.ExecuteAttack(target);
    }

    public class WeaponLogic {
        private readonly WeaponConfig _config;

        public WeaponLogic(WeaponConfig config) => _config = config;
        public void ExecuteAttack(IDamageable target) => target.TakeDamage(_config.damage);
    }
}