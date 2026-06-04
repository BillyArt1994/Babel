using System.Collections.Generic;
using NUnit.Framework;

namespace Babel.Tests
{
    public class EnemyParserTests
    {
        [Test]
        public void Parse_ReadsMoveMode_WhenPresent()
        {
            string csv = string.Join("\n", new[]
            {
                "enemyId,enemyName,hp,moveSpeed,buildContribution,buildCharges,expReward,prefab,moveMode",
                "scout,斥候,20,5,25,1,2,Enemies/Scout,scout"
            });

            List<EnemyData> list = EnemyParser.Parse(csv);

            Assert.That(list.Count, Is.EqualTo(1));
            Assert.That(list[0].MoveMode, Is.EqualTo("scout"));
        }

        [Test]
        public void Parse_DefaultsMoveModeToEmpty_WhenColumnMissing()
        {
            string csv = string.Join("\n", new[]
            {
                "enemyId,enemyName,hp,moveSpeed,buildContribution,buildCharges,expReward,prefab",
                "worker,工人,30,1,25,1,1,Enemies/Worker"
            });

            List<EnemyData> list = EnemyParser.Parse(csv);

            Assert.That(list.Count, Is.EqualTo(1));
            Assert.That(list[0].MoveMode, Is.EqualTo(""));
        }

        [Test]
        public void Parse_NormalizesMoveModeToLowerCase()
        {
            string csv = string.Join("\n", new[]
            {
                "enemyId,enemyName,hp,moveSpeed,buildContribution,buildCharges,expReward,prefab,moveMode",
                "scout,斥候,20,5,25,1,2,Enemies/Scout,SCOUT"
            });

            List<EnemyData> list = EnemyParser.Parse(csv);

            Assert.That(list.Count, Is.EqualTo(1));
            Assert.That(list[0].MoveMode, Is.EqualTo("scout"));
        }

        [Test]
        public void Parse_ReadsSenseRadius_WhenPresent()
        {
            string csv = string.Join("\n", new[]
            {
                "enemyId,enemyName,hp,moveSpeed,buildContribution,buildCharges,expReward,prefab,moveMode,senseRadius",
                "priest,祭司,60,1.5,25,1,3,Enemies/Priest,support,8"
            });

            List<EnemyData> list = EnemyParser.Parse(csv);

            Assert.That(list.Count, Is.EqualTo(1));
            Assert.That(list[0].SenseRadius, Is.EqualTo(8f).Within(0.001f));
        }

        [Test]
        public void Parse_DefaultsSenseRadiusToEight_WhenColumnMissing()
        {
            // SenseRadius 字段默认值为 8f（EnemyData 初始化）
            string csv = string.Join("\n", new[]
            {
                "enemyId,enemyName,hp,moveSpeed,buildContribution,buildCharges,expReward,prefab",
                "worker,工人,30,1,25,1,1,Enemies/Worker"
            });

            List<EnemyData> list = EnemyParser.Parse(csv);

            Assert.That(list.Count, Is.EqualTo(1));
            Assert.That(list[0].SenseRadius, Is.EqualTo(8f).Within(0.001f));
        }
    }
}
