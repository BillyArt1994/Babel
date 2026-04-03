using QFramework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Babel
{
    public class SkillData : ScriptableObject
    {
        [Header("基础信息")]
        public int id;
        public string skillName;
        public string skillDescription;
        public float radius;
        public float damage;
        [TextArea] public string description;
        public Sprite icon;
        public SkillTriggerType triggerType;

    }

    public enum SkillTriggerType
    {
        Manual,      // 主动释放（玩家按键）
        AutoTimer,   // 自动循环（每隔X秒触发）
        OnEvent      // 事件触发（受击、暴击、死亡等）
    }

}