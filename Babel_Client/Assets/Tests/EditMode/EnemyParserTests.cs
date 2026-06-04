using System.Collections.Generic;
using NUnit.Framework;

namespace Babel.Tests
{
    public class EnemyParserTests
    {
        [Test]
        public void Parse_ReadsTargetModeColumn_WhenPresent()
        {
            string csv = string.Join("\n", new[]
            {
                "enemyId,enemyName,hp,moveSpeed,buildContribution,buildCharges,expReward,prefab,targetMode",
                "scout,斥候,20,5,25,1,2,Enemies/Scout,scout"
            });

            List<EnemyData> list = EnemyParser.Parse(csv);

            Assert.That(list.Count, Is.EqualTo(1));
            Assert.That(list[0].TargetMode, Is.EqualTo("scout"));
        }

        [Test]
        public void Parse_DefaultsTargetModeToEmpty_WhenColumnMissing()
        {
            string csv = string.Join("\n", new[]
            {
                "enemyId,enemyName,hp,moveSpeed,buildContribution,buildCharges,expReward,prefab",
                "worker,工人,30,1,25,1,1,Enemies/Worker"
            });

            List<EnemyData> list = EnemyParser.Parse(csv);

            Assert.That(list.Count, Is.EqualTo(1));
            Assert.That(list[0].TargetMode, Is.EqualTo(""));
        }

        [Test]
        public void Parse_NormalizesTargetModeToLowerCase()
        {
            // CSV 中写大写 SCOUT，Parser 应统一规范化为小写
            string csv = string.Join("\n", new[]
            {
                "enemyId,enemyName,hp,moveSpeed,buildContribution,buildCharges,expReward,prefab,targetMode",
                "scout,斥候,20,5,25,1,2,Enemies/Scout,SCOUT"
            });

            List<EnemyData> list = EnemyParser.Parse(csv);

            Assert.That(list.Count, Is.EqualTo(1));
            Assert.That(list[0].TargetMode, Is.EqualTo("scout"));
        }
    }
}
