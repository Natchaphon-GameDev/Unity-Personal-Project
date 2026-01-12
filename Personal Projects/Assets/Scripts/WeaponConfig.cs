using UnityEngine;

namespace DefaultNamespace {
    [CreateAssetMenu(fileName = "WeaponConfig", menuName = "Configs/WeaponConfig", order = 0)]
    public class WeaponConfig : ScriptableObject {
        public int damage;

        public WeaponConfig(int damage) => this.damage = damage;
    }
}