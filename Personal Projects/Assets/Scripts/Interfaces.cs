namespace DefaultNamespace {
    public interface IWeapon {
        public void Attack(IDamageable target);
    }

    public interface IDamageable {
        public void TakeDamage(int damage);
    }
}