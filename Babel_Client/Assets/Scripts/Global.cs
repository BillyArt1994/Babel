using QFramework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Babel
{
    public class Global
    {
        /// <summary>
        /// 
        /// </summary>
        public static IBindableProperty<int> Exp = new BindableProperty<int>() ;
        public static IBindableProperty<int> Level = new BindableProperty<int>(1) ;
        public static IBindableProperty<float> CurrentTime = new BindableProperty<float>(900.0f) ;

        public static void RestData()
        {
            Exp.Value = 0;
            Level.Value = 1;
            CurrentTime.Value = 900.0f;

        }

    }
}