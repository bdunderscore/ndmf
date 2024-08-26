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
        
        internal static void OnMainThread(EditorApplication.CallbackFunction action)
        {
            if (Thread.CurrentThread == _mainThread)
            {
                action();
            }
            else
            {
                EditorApplication.delayCall += action;
            }
        }

        internal static void OnMainThread<T>(T target, Action<T> receiver)
        {
            if (Thread.CurrentThread == _mainThread)
            {
                receiver((T) target);
            }
            else
            {
                EditorApplication.delayCall += () => receiver((T) target);
            }
        }
    }
}