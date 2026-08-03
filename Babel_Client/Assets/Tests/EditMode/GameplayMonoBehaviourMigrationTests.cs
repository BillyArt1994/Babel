using System;
using NUnit.Framework;
using UnityEngine;

namespace Babel.Tests
{
    public sealed class GameplayMonoBehaviourMigrationTests
    {
        [TestCase(typeof(ClickAttackSystem))]
        [TestCase(typeof(Enemy))]
        [TestCase(typeof(BuildPoint))]
        [TestCase(typeof(EnemyGenerator))]
        [TestCase(typeof(InputSystem))]
        [TestCase(typeof(SkillSystem))]
        [TestCase(typeof(UpgradeSystem))]
        [TestCase(typeof(XpSystem))]
        [TestCase(typeof(TowerManager))]
        public void NonUiGameplayComponent_DerivesDirectlyFromMonoBehaviour(Type componentType)
        {
            Assert.That(
                componentType.BaseType,
                Is.EqualTo(typeof(MonoBehaviour)),
                componentType.FullName + " must not retain an intermediary framework controller base.");
        }
    }
}
