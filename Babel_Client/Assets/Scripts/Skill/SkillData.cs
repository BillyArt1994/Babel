using QFramework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Babel
{
    public abstract class SkillData : ScriptableObject
    {
        [Header("基础信息")]
        public string skillName;
        [TextArea] public string skillDescription;
        public float radius;
        public float damage;
        public Sprite icon;
    }

}