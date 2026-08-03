using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Babel.Tests
{
    public class SkillSystemStartupTests
    {
        [Test]
        public void Start_WhenSkillsCsvExists_InitializesDatabaseAndEquipsDivineFinger()
        {
            Type skillDatabaseType = RequireType("Babel.SkillDatabase");
            MethodInfo initDatabaseMethod = skillDatabaseType.GetMethod(
                "Init",
                BindingFlags.Public | BindingFlags.Static);
            PropertyInfo databaseCountProperty = skillDatabaseType.GetProperty(
                "Count",
                BindingFlags.Public | BindingFlags.Static);
            MethodInfo getEquippedSkillsMethod = RequireType("Babel.SkillSystem").GetMethod(
                "GetEquippedSkills",
                BindingFlags.Public | BindingFlags.Instance);
            MethodInfo startMethod = RequireType("Babel.SkillSystem").GetMethod(
                "Start",
                BindingFlags.Instance | BindingFlags.NonPublic);
            TextAsset skillsCsv = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Babel/Content/Data/Skills/skills.csv");
            var skillSystemObject = new GameObject("SkillSystemStartupTest");

            try
            {
                Assert.That(skillsCsv, Is.Not.Null, "Test fixture requires the production skills CSV.");
                initDatabaseMethod.Invoke(null, new object[] { string.Empty });
                Component skillSystem = skillSystemObject.AddComponent(RequireType("Babel.SkillSystem"));
                FieldInfo skillsField = skillSystem.GetType().GetField("skillsCSV", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(skillsField, Is.Not.Null);
                skillsField.SetValue(skillSystem, skillsCsv);

                startMethod.Invoke(skillSystem, Array.Empty<object>());

                Assert.That((int)databaseCountProperty.GetValue(null), Is.GreaterThan(0));
                IEnumerable equippedSkills = (IEnumerable)getEquippedSkillsMethod.Invoke(skillSystem, Array.Empty<object>());
                Assert.That(ContainsSkill(equippedSkills, "divine_finger"), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(skillSystemObject);
            }
        }

        private static bool ContainsSkill(IEnumerable skills, string expectedSkillId)
        {
            foreach (object skill in skills)
            {
                object config = skill.GetType().GetProperty("Config").GetValue(skill);
                string skillId = (string)config.GetType().GetField("SkillId").GetValue(config);
                if (skillId == expectedSkillId)
                {
                    return true;
                }
            }

            return false;
        }

        private static Type RequireType(string fullName)
        {
            Type type = Type.GetType($"{fullName}, Babel")
                      ?? Type.GetType($"{fullName}, Assembly-CSharp")
                      ?? Type.GetType(fullName);
            if (type == null)
            {
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = asm.GetType(fullName);
                    if (type != null) break;
                }
            }
            Assert.That(type, Is.Not.Null, $"{fullName} should exist in a loaded assembly.");
            return type;
        }
    }
}
