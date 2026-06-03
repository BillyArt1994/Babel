using NUnit.Framework;
using UnityEngine;

namespace Babel.Tests
{
    public class XpSystemTests
    {
        private GameObject _go;
        private XpSystem _xp;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("XpSystem");
            _xp = _go.AddComponent<XpSystem>();
            // 简单 3 级曲线：升到 2 级需要 10 XP，升到 3 级需要 25 XP
            _xp.InitForTests(new float[] { 10f, 25f });
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        // Behavior 1: 获得 XP 不够升级 → 等级不变，XP 累积
        [Test]
        public void GainXp_BelowThreshold_LevelUnchangedAndXpAccumulates()
        {
            _xp.GainXp(6f);

            Assert.That(_xp.CurrentLevel, Is.EqualTo(1));
            Assert.That(_xp.CurrentXp, Is.EqualTo(6f).Within(0.001f));
        }

        // Behavior 2: 恰好够升级 → 等级+1，剩余 XP=0，触发 OnLevelsGained(1)
        [Test]
        public void GainXp_ExactThreshold_LevelUpAndZeroRemainder()
        {
            int levelsGained = 0;
            _xp.OnLevelsGained += n => levelsGained = n;

            _xp.GainXp(10f);

            Assert.That(_xp.CurrentLevel, Is.EqualTo(2));
            Assert.That(_xp.CurrentXp, Is.EqualTo(0f).Within(0.001f));
            Assert.That(levelsGained, Is.EqualTo(1));
        }

        // Behavior 3: 超过一级所需 XP → 连升多级，剩余 XP 正确，触发 OnLevelsGained(2)
        [Test]
        public void GainXp_OverMultipleThresholds_MultiLevelUpWithCorrectRemainder()
        {
            int levelsGained = 0;
            _xp.OnLevelsGained += n => levelsGained = n;

            // 10(升2级) + 25(升3级) + 3(剩余) = 38
            _xp.GainXp(38f);

            Assert.That(_xp.CurrentLevel, Is.EqualTo(3));
            Assert.That(_xp.CurrentXp, Is.EqualTo(3f).Within(0.001f));
            Assert.That(levelsGained, Is.EqualTo(2));
        }

        // Behavior 4: 每级所需 XP 递增（从表里读）
        [Test]
        public void XpForNextLevel_IncreasesWithLevel()
        {
            float level1Needed = _xp.XpForNextLevel; // 升到2级需10

            _xp.GainXp(10f); // 升到2级

            Assert.That(_xp.XpForNextLevel, Is.GreaterThan(level1Needed));
            Assert.That(_xp.XpForNextLevel, Is.EqualTo(25f).Within(0.001f));
        }

        // Behavior 5: XpProgress 返回 [0,1] 比例
        [Test]
        public void XpProgress_ReturnsCorrectFraction()
        {
            _xp.GainXp(5f); // 5/10 = 0.5

            Assert.That(_xp.XpProgress, Is.EqualTo(0.5f).Within(0.001f));
        }

        // Behavior 6: GainXp(0) 安全，等级不变
        [Test]
        public void GainXp_Zero_NoChange()
        {
            _xp.GainXp(0f);

            Assert.That(_xp.CurrentLevel, Is.EqualTo(1));
            Assert.That(_xp.CurrentXp, Is.EqualTo(0f).Within(0.001f));
        }

        // Behavior 7: 满级后继续 GainXp → 不升级，不触发事件
        [Test]
        public void GainXp_AtMaxLevel_NoFurtherLevelUp()
        {
            int calls = 0;
            _xp.OnLevelsGained += _ => calls++;

            _xp.GainXp(10f); // → level 2
            _xp.GainXp(25f); // → level 3 (max，曲线只有2项)
            calls = 0;        // 重置，只统计满级后的调用

            _xp.GainXp(999f); // 满级后大量 XP

            Assert.That(_xp.CurrentLevel, Is.EqualTo(3));
            Assert.That(calls, Is.EqualTo(0));
        }
    }
}
