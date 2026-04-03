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
        public static IBindableProperty<int> Level = new BindableProperty<int>() ;

        public static IBindableProperty<float> CurrentTime = new BindableProperty<float>() ;

    }
}