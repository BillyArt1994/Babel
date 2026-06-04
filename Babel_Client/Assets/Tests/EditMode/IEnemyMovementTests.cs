using NUnit.Framework;

namespace Babel.Tests
{
    /// <summary>
    /// 接口契约测试：验证接口签名存在于已加载的程序集中。
    /// </summary>
    public class IEnemyMovementTests
    {
        [Test]
        public void IEnemyMovement_InterfaceExists()
        {
            var t = typeof(IEnemyMovement);
            Assert.That(t, Is.Not.Null);
            Assert.That(t.IsInterface, Is.True);
        }

        [Test]
        public void IEnemyMovement_HasRequiredMembers()
        {
            var t = typeof(IEnemyMovement);
            Assert.That(t.GetMethod("Init"), Is.Not.Null, "Init method missing");
            Assert.That(t.GetMethod("Tick"), Is.Not.Null, "Tick method missing");
            Assert.That(t.GetMethod("OnRemoved"), Is.Not.Null, "OnRemoved method missing");
            Assert.That(t.GetProperty("IsMoving"), Is.Not.Null, "IsMoving property missing");
        }

        [Test]
        public void IBuildInterruptible_InterfaceExists()
        {
            var t = typeof(IBuildInterruptible);
            Assert.That(t, Is.Not.Null);
            Assert.That(t.IsInterface, Is.True);
        }

        [Test]
        public void IBuildInterruptible_HasOnTargetBuildCompleted()
        {
            var t = typeof(IBuildInterruptible);
            Assert.That(t.GetMethod("OnTargetBuildCompleted"), Is.Not.Null);
        }
    }
}
