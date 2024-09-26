using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace nadena.dev.ndmf.preview
{
    // This is basically the same as ScaleAdjustedBones in ModularAvatar with slight changes, but I'm not ready to
    // create a public API just yet, so copied here...
    internal class ShadowBoneManager
    {
        internal static ShadowBoneManager Instance { get; } = new();
        
        private static int editorFrameCount = 0;
        private static int lastUpdateFrame = 0;
        private static int lastMutatingUpdate = 0;
        private static int mutatingUpdateCount = 0;

        [InitializeOnLoadMethod]
        static void Init()
        {
            EditorApplication.update += () => editorFrameCount++;
        }

        internal class BoneState
        {
            public Transform original;
            public Transform proxy;
            public int lastUsedFrame;
            public BoneState parentHint;
        }

        private readonly Dictionary<Component, BoneState> _bones = new();
        //private List<BoneState> _states = new List<BoneState>();

        public void Clear()
        {
            foreach (var state in _bones.Values)
            {
                if (state.proxy != null) Object.DestroyImmediate(state.proxy.gameObject);
            }

            _bones.Clear();
        }
        
        public BoneState GetBone(Transform src, bool force = true)
        {
            if (src == null) return null;

            if (_bones.TryGetValue(src, out var state))
            {
                state.lastUsedFrame = mutatingUpdateCount;
                return state;
            }

            if (!force) return null;

            var proxyObj = new GameObject(src.name);
            SceneManager.MoveGameObjectToScene(proxyObj, NDMFPreviewSceneManager.GetPreviewScene());
            proxyObj.AddComponent<SelfDestructComponent>().KeepAlive = this;
            proxyObj.hideFlags = HideFlags.DontSave;

            var boneState = new BoneState();
            boneState.original = src;
            boneState.proxy = proxyObj.transform;
            boneState.parentHint = null;
            boneState.lastUsedFrame = Time.frameCount;

            _bones[src] = boneState;

            CheckParent(CopyState(boneState), boneState);

            return boneState;
        }

        private List<Component> toRemove = new List<Component>();
        private List<BoneState> stateList = new List<BoneState>();

        public void Update()
        {
            if (lastUpdateFrame == editorFrameCount)
            {
                return;
            }

            lastUpdateFrame = editorFrameCount;
            
            if (lastMutatingUpdate != editorFrameCount)
            {
                mutatingUpdateCount++;
                lastMutatingUpdate = editorFrameCount;
            }

            toRemove.Clear();

            stateList.Clear();
            stateList.AddRange(_bones.Values);

            foreach (var entry in stateList)
            {
                if (entry.original == null || entry.proxy == null)
                {
                    if (entry.proxy != null)
                    {
                        Object.DestroyImmediate(entry.proxy.gameObject);
                    }

                    toRemove.Add(entry.original);
                    continue;
                }

                if (mutatingUpdateCount - entry.lastUsedFrame > 5 && entry.proxy.childCount == 0)
                {
                    Object.DestroyImmediate(entry.proxy.gameObject);
                    toRemove.Add(entry.original);
                    continue;
                }

                Transform parent = CopyState(entry);

                CheckParent(parent, entry);
            }

            foreach (var remove in toRemove)
            {
                _bones.Remove(remove);
            }
        }

        private void CheckParent(Transform parent, BoneState entry)
        {
            if (parent != entry.parentHint?.original)
            {
                entry.parentHint = GetBone(parent);
                entry.proxy.SetParent(entry.parentHint?.proxy, false);
            }
        }

        private static Transform CopyState(BoneState entry)
        {
            Transform parent;
            var t = entry.original;
            
            parent = t.parent;

            entry.proxy.localPosition = t.localPosition;
            entry.proxy.localRotation = t.localRotation;
            entry.proxy.localScale = t.localScale;

            return parent;
        }
    }
}