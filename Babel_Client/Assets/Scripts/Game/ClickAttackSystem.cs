using System;
using UnityEngine;

namespace Babel
{
    public partial class ClickAttackSystem : MonoBehaviour
    {
        public static event Action<AttackResult> OnAttackExecuted;

        public static void RaiseAttackExecuted(AttackResult result)
        {
            OnAttackExecuted?.Invoke(result);
        }
    }
}
