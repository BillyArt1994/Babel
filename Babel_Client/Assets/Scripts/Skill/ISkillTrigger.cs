using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class SkillTrigger 
{
    public virtual void Gain() { }
    public virtual void Lose() { }
    public virtual void Tick(float deltaTime) { }
    public virtual bool Force() => false;
   // public virtual void Repeat(in TriggerContext context) => Fire(context);
}
