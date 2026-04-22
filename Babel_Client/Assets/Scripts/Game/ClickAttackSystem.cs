using System;
using UnityEngine;
using QFramework;

namespace Babel
{
    public partial class ClickAttackSystem : ViewController
    {
        public static event Action<AttackResult> OnAttackExecuted;

        public static void RaiseAttackExecuted(AttackResult result)
        {
            OnAttackExecuted?.Invoke(result);
        }
    }
}
