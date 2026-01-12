using DefaultNamespace;
using NUnit.Framework;
using UnityEngine;

public class TestDamageable : IDamageable {
    private int _damageTaken;
    public int damageTaken => _damageTaken;

    public void TakeDamage(int damage) {
        _damageTaken += damage;
    }
}

public class WeaponTests {
    [Test]
    public void Weapon_Attack_ReducesEnemyHealth() {
        // Arrange
        var config = ScriptableObject.CreateInstance<WeaponConfig>();
        config.damage = 25;
        var weaponLogic = new WeaponLogic(config);
        var enemy = new TestDamageable();

        // Act
        weaponLogic.ExecuteAttack(enemy);

        // Assert
        Assert.AreEqual(25, enemy.damageTaken);
    }
}