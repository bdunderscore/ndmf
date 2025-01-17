using System;
using System.Threading;
using UnityEditor;

namespace nadena.dev.ndmf.preview
{
    internal class TaskUtil
    {
        private static Thread _mainThread;
        
        [InitializeOnLoadMethod]
        static void Init()
        {
            _mainThread = Thread.CurrentThread;
        }
    }
}