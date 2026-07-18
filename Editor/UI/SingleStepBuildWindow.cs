#nullable enable

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using nadena.dev.ndmf.platform;
using nadena.dev.ndmf.preview;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UnityObject = UnityEngine.Object;

#endregion

namespace nadena.dev.ndmf.ui
{
    internal sealed class SingleStepBuildSession : IDisposable
    {
        internal const string GeneratedAssetRoot =
            "Packages/nadena.dev.ndmf/__Generated/SingleStepBuildDebugger";

        internal GameObject TargetAvatar { get; }
        internal GameObject DebugClone { get; }
        internal BuildContext Context { get; }
        internal BuildStepPlan Plan { get; }
        internal int Cursor { get; private set; }

        private IDisposable? _previewExclusion;
        private AvatarBuildStateTracker? _tracker;
        private bool _disposed;

        private SingleStepBuildSession(
            GameObject targetAvatar,
            GameObject debugClone,
            BuildContext context,
            BuildStepPlan plan,
            IDisposable previewExclusion,
            AvatarBuildStateTracker tracker
        )
        {
            TargetAvatar = targetAvatar;
            DebugClone = debugClone;
            Context = context;
            Plan = plan;
            _previewExclusion = previewExclusion;
            _tracker = tracker;
        }

        internal static SingleStepBuildSession Create(
            GameObject targetAvatar,
            INDMFPlatformProvider platform
        )
        {
            CleanupGeneratedAssets();

            var clone = UnityObject.Instantiate(targetAvatar);
            clone.name = targetAvatar.name + " (NDMF Single-Step)";
            clone.transform.position += Vector3.forward * 2f;
            clone.hideFlags |= HideFlags.DontSave;

            var tracker = clone.AddComponent<AvatarBuildStateTracker>();
            tracker.singleStepDebugClone = true;

            var previewExclusion = NDMFPreview.ExcludeAvatarFromDefaultPreview(clone);
            try
            {
                using var _platformScope = new AmbientPlatform.Scope(platform);
                var context = new BuildContext(clone, GeneratedAssetRoot, platform);
                tracker.buildContext = context;

                return new SingleStepBuildSession(
                    targetAvatar,
                    clone,
                    context,
                    BuildStepPlan.Resolve(platform),
                    previewExclusion,
                    tracker
                );
            }
            catch
            {
                previewExclusion.Dispose();
                UnityObject.DestroyImmediate(clone);
                CleanupGeneratedAssets();
                throw;
            }
        }

        internal void ExecuteCurrentStep()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SingleStepBuildSession));
            if (Cursor >= Plan.CompleteStepIndex) return;

            ExecuteAndSerialize(ExecuteCurrentStepWithoutSerialization);
        }

        private void ExecuteCurrentStepWithoutSerialization()
        {
            var step = Plan.Steps[Cursor];
            ExecuteStep(step);

            Cursor++;
            if (Cursor == Plan.CompleteStepIndex)
            {
                Context.Finish();
            }
        }

        private void ExecuteStep(BuildStep step)
        {
            if (step.Kind == BuildStepKind.Complete)
            {
                throw new InvalidOperationException("The build-complete sentinel cannot be executed");
            }

            switch (step.Kind)
            {
                case BuildStepKind.DeactivateExtension:
                    Context.RunExtensionContextDeactivation(step.Pass!, step.ExtensionType!);
                    break;
                case BuildStepKind.ActivateExtension:
                    Context.RunExtensionContextActivation(step.Pass!, step.ExtensionType!);
                    break;
                case BuildStepKind.ExecutePass:
                    Context.RunPassBody(step.Pass!);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        internal void ExecuteUntil(int targetIndex)
        {
            if (targetIndex < Cursor || targetIndex > Plan.CompleteStepIndex)
            {
                throw new ArgumentOutOfRangeException(nameof(targetIndex));
            }

            if (Cursor == targetIndex) return;

            ExecuteAndSerialize(() =>
            {
                while (Cursor < targetIndex)
                {
                    ExecuteCurrentStepWithoutSerialization();
                }
            });
        }

        private void ExecuteAndSerialize(Action execute)
        {
            Exception? executionException = null;

            try
            {
                execute();
            }
            catch (Exception e)
            {
                executionException = e;
            }

            try
            {
                Context.Serialize();
            }
            catch (Exception serializationException)
            {
                if (executionException != null)
                {
                    throw new AggregateException(executionException, serializationException);
                }

                throw;
            }

            if (executionException != null)
            {
                ExceptionDispatchInfo.Capture(executionException).Throw();
            }
        }

        internal GameObject StopAndKeepClone()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SingleStepBuildSession));

            PreserveGeneratedAssets();

            _disposed = true;
            _previewExclusion?.Dispose();
            _previewExclusion = null;

            DebugClone.hideFlags = HideFlags.None;
            if (_tracker != null)
            {
                _tracker.buildContext = null;
                _tracker.singleStepDebugClone = false;
                UnityObject.DestroyImmediate(_tracker);
                _tracker = null;
            }

            return DebugClone;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _previewExclusion?.Dispose();
            _previewExclusion = null;

            if (DebugClone != null)
            {
                UnityObject.DestroyImmediate(DebugClone);
            }

            CleanupGeneratedAssets();
        }

        internal static void CleanupStaleDebugState()
        {
            var hasLiveSession = false;
            foreach (var tracker in Resources.FindObjectsOfTypeAll<AvatarBuildStateTracker>())
            {
                if (tracker == null || !tracker.singleStepDebugClone) continue;
                if (tracker.gameObject == null) continue;

                if (tracker.buildContext != null)
                {
                    hasLiveSession = true;
                    continue;
                }

                UnityObject.DestroyImmediate(tracker.gameObject);
            }

            if (!hasLiveSession)
            {
                CleanupGeneratedAssets();
            }
        }

        internal static void CleanupGeneratedAssets()
        {
            if (AssetDatabase.IsValidFolder(GeneratedAssetRoot))
            {
                AssetDatabase.DeleteAsset(GeneratedAssetRoot);
            }
        }

        private static void PreserveGeneratedAssets()
        {
            if (!AssetDatabase.IsValidFolder(GeneratedAssetRoot)) return;

            var retainedPath = AssetDatabase.GenerateUniqueAssetPath(GeneratedAssetRoot + "-Kept");
            var error = AssetDatabase.MoveAsset(GeneratedAssetRoot, retainedPath);
            if (!string.IsNullOrEmpty(error))
            {
                throw new InvalidOperationException($"Could not retain generated assets: {error}");
            }

            AssetDatabase.SaveAssets();
        }
    }

    internal sealed class SingleStepBuildWindow : EditorWindow
    {
        private const string MenuPath = "Tools/NDM Framework/Debug Tools/Single-step build";
        private const string ResourcesRoot = "Packages/nadena.dev.ndmf/Editor/UI";
        private const string UxmlPath = ResourcesRoot + "/SingleStepBuildWindow.uxml";
        private const string UssPath = ResourcesRoot + "/SingleStepBuildWindow.uss";

        [SerializeField] private GameObject? _targetAvatar;
        [SerializeField] private BuildStepBookmark _currentStep = new();
        [SerializeField] private bool _hasSuspendedSession;

        [NonSerialized] private SingleStepBuildSession? _session;
        [NonSerialized] private string? _statusMessage;
        [NonSerialized] private MessageType _statusType = MessageType.Info;
        [NonSerialized] private bool _assemblyReloadPending;
        [NonSerialized] private bool _resumeBookmarkValid;
        [NonSerialized] private ObjectField? _targetField;
        [NonSerialized] private ObjectField? _cloneField;
        [NonSerialized] private Label? _statusLabel;
        [NonSerialized] private VisualElement? _inactiveControls;
        [NonSerialized] private VisualElement? _activeControls;
        [NonSerialized] private Button? _resumeButton;
        [NonSerialized] private Button? _startButton;
        [NonSerialized] private Button? _backButton;
        [NonSerialized] private Button? _forwardButton;
        [NonSerialized] private ScrollView? _stepList;
        [NonSerialized] private readonly HashSet<string> _expandedGroups = new();

        [MenuItem(MenuPath, false, 100)]
        private static void ShowWindow()
        {
            GetWindow<SingleStepBuildWindow>("NDMF Single-Step");
        }

        [InitializeOnLoadMethod]
        private static void CleanupAfterDomainReload()
        {
            EditorApplication.delayCall += SingleStepBuildSession.CleanupStaleDebugState;
        }

        private void OnEnable()
        {
            _currentStep ??= new BuildStepBookmark();
            AssemblyReloadEvents.beforeAssemblyReload += BeforeAssemblyReload;

            if (_targetAvatar == null)
            {
                _targetAvatar = Selection.activeGameObject;
            }
        }

        private void BeforeAssemblyReload()
        {
            if (_session != null)
            {
                _hasSuspendedSession = true;
            }

            _assemblyReloadPending = true;
            MarkWindowDirty();
            DisposeSession();
        }

        private void OnDisable()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= BeforeAssemblyReload;
            DisposeSession();

            if (!_assemblyReloadPending)
            {
                _targetAvatar = null;
                _currentStep.Clear();
                _hasSuspendedSession = false;
            }
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            root.AddToClassList("single-step-host");

            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (visualTree == null || styleSheet == null)
            {
                Debug.LogError($"Failed to load NDMF single-step UI from {UxmlPath} and {UssPath}");
                root.Add(new Label("Failed to load the NDMF single-step UI."));
                return;
            }

            root.styleSheets.Add(styleSheet);
            visualTree.CloneTree(root);

            _targetField = root.Q<ObjectField>("target-avatar");
            _targetField.objectType = typeof(GameObject);
            _targetField.allowSceneObjects = true;
            _targetField.RegisterValueChangedCallback(evt =>
            {
                _targetAvatar = evt.newValue as GameObject;
                _currentStep.Clear();
                _hasSuspendedSession = false;
                _resumeBookmarkValid = false;
                MarkWindowDirty();
                RefreshUI();
            });

            _cloneField = root.Q<ObjectField>("debug-clone");
            _cloneField.objectType = typeof(GameObject);
            _cloneField.allowSceneObjects = true;

            _statusLabel = root.Q<Label>("status");
            _inactiveControls = root.Q("inactive-controls");
            _activeControls = root.Q("active-controls");
            _resumeButton = root.Q<Button>("resume-debug-session");
            _startButton = root.Q<Button>("start-debugging");
            _backButton = root.Q<Button>("step-back");
            _forwardButton = root.Q<Button>("step-forward");
            _stepList = root.Q<ScrollView>("step-list");

            _resumeButton.clicked += ResumeSavedSession;
            _startButton.clicked += StartFromBeginning;
            _backButton.clicked += StepBack;
            _forwardButton.clicked += ExecuteOneStep;
            root.Q<Button>("stop-and-keep").clicked += StopAndKeepClone;

            RefreshResumeAvailability();
            RefreshUI();
        }

        private void RefreshUI()
        {
            if (_targetField == null || _cloneField == null || _statusLabel == null ||
                _inactiveControls == null || _activeControls == null || _resumeButton == null ||
                _startButton == null || _backButton == null || _forwardButton == null || _stepList == null)
            {
                return;
            }

            var isActive = _session != null;
            var canStart = CanStart();

            _targetField.SetValueWithoutNotify(_targetAvatar);
            _targetField.SetEnabled(!isActive);
            SetVisible(_startButton, !isActive);
            _startButton.SetEnabled(canStart);

            _cloneField.SetValueWithoutNotify(_session?.DebugClone);
            SetVisible(_cloneField, isActive);

            _statusLabel.text = _statusMessage ?? "";
            SetVisible(_statusLabel, !string.IsNullOrEmpty(_statusMessage));
            _statusLabel.EnableInClassList("status-error", _statusType == MessageType.Error);
            _statusLabel.EnableInClassList("status-warning", _statusType == MessageType.Warning);

            SetVisible(_inactiveControls, !isActive);
            SetVisible(_resumeButton, _hasSuspendedSession && _resumeBookmarkValid);
            _resumeButton.SetEnabled(canStart);

            SetVisible(_activeControls, isActive);
            SetVisible(_stepList, isActive);
            _backButton.SetEnabled(isActive && _session!.Cursor > 0);
            _forwardButton.SetEnabled(isActive && _session!.Cursor < _session.Plan.CompleteStepIndex);

            RefreshStepList();
        }

        private void RefreshStepList()
        {
            _stepList!.Clear();
            if (_session == null) return;

            string? priorPhase = null;

            foreach (var group in BuildStepGrouping.Group(_session.Plan.Steps))
            {
                if (group.PhaseName != priorPhase)
                {
                    var phaseLabel = new Label(group.PhaseName);
                    phaseLabel.AddToClassList("phase-label");
                    _stepList.Add(phaseLabel);
                    priorPhase = group.PhaseName;
                }

                if (!group.IsFoldout)
                {
                    AddStepRow(_stepList, group.Steps[0], true);
                    continue;
                }

                var groupKey = string.Join("|", group.PhaseName, group.PluginQualifiedName,
                    group.Steps[0].Index.ToString(), group.Steps[group.Steps.Count - 1].Index.ToString());
                var containsCursor = group.Steps.Any(step => step.Index == _session.Cursor);
                var isComplete = group.Steps.All(step => step.Index < _session.Cursor);
                if (containsCursor) _expandedGroups.Add(groupKey);

                var foldout = new Foldout
                {
                    text = isComplete ? $"✓ {group.PluginName}" : group.PluginName
                };
                foldout.AddToClassList("plugin-foldout");
                if (isComplete) foldout.AddToClassList("plugin-foldout-executed");
                foldout.SetValueWithoutNotify(_expandedGroups.Contains(groupKey));
                foldout.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue)
                    {
                        _expandedGroups.Add(groupKey);
                    }
                    else
                    {
                        _expandedGroups.Remove(groupKey);
                    }
                });

                foreach (var step in group.Steps)
                {
                    AddStepRow(foldout, step, false);
                }

                _stepList.Add(foldout);
            }
        }

        private void AddStepRow(VisualElement parent, BuildStep step, bool showPlugin)
        {
            var statePrefix = step.Index < _session!.Cursor ? "✓ " : "  ";
            var isExtensionTransition = step.Kind is BuildStepKind.ActivateExtension or
                BuildStepKind.DeactivateExtension;
            var pluginPrefix = showPlugin && !isExtensionTransition ? $"{step.PluginName}: " : "";
            var skippedPrefix = step.IsSkipped ? "(skipped) " : "";
            var row = new Label(
                $"{statePrefix}{skippedPrefix}{pluginPrefix}{step.DisplayName}"
            );
            row.AddToClassList("step-row");
            if (showPlugin) row.AddToClassList("step-row-standalone");
            if (step.IsSkipped) row.AddToClassList("step-row-skipped");
            row.tooltip = "Double-click to run until just before this step.";

            if (step.Index == _session.Cursor)
            {
                row.AddToClassList("step-row-current");
            }
            else if (step.Index < _session.Cursor)
            {
                row.AddToClassList("step-row-executed");
            }

            var targetIndex = step.Index;
            row.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 0 && evt.clickCount == 2)
                {
                    evt.StopPropagation();
                    RunToStep(targetIndex);
                }
            });
            parent.Add(row);
        }

        private static void SetVisible(VisualElement element, bool visible)
        {
            element.EnableInClassList("hidden", !visible);
        }

        private void StepBack()
        {
            if (_session == null || _session.Cursor == 0) return;
            RebuildToIndex(_session.Cursor - 1);
        }

        private void RefreshResumeAvailability()
        {
            _resumeBookmarkValid = false;
            if (!_hasSuspendedSession || !_currentStep.IsSet || !CanStart()) return;

            try
            {
                var plan = BuildStepPlan.Resolve(AmbientPlatform.CurrentPlatform);
                _resumeBookmarkValid = _currentStep.TryResolve(plan, out _);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private bool CanStart()
        {
            return _targetAvatar != null && AvatarProcessor.CanProcessObject(_targetAvatar);
        }

        private void StartFromBeginning()
        {
            _hasSuspendedSession = false;
            _resumeBookmarkValid = false;
            MarkWindowDirty();
            if (!TryCreateSession()) return;

            UpdateCurrentStepBookmark();
            _statusMessage = null;
            _statusType = MessageType.Info;
            RefreshUI();
        }

        private void ResumeSavedSession()
        {
            if (!_hasSuspendedSession || !_resumeBookmarkValid) return;
            var savedBookmark = _currentStep;

            if (!TryCreateSession()) return;

            if (!savedBookmark.TryResolve(_session!.Plan, out var targetIndex))
            {
                UpdateCurrentStepBookmark();
                _statusMessage =
                    "The saved step no longer maps uniquely to the updated build plan. Start debugging to begin from scratch.";
                _statusType = MessageType.Warning;
                _hasSuspendedSession = false;
                _resumeBookmarkValid = false;
                MarkWindowDirty();
                RefreshUI();
                return;
            }

            ExecuteTo(targetIndex);
        }

        private bool TryCreateSession()
        {
            DisposeSession();
            _expandedGroups.Clear();
            _statusMessage = null;

            if (!CanStart())
            {
                _statusMessage = "Select a processable avatar root before starting the debugger.";
                _statusType = MessageType.Warning;
                RefreshUI();
                return false;
            }

            try
            {
                var platform = AmbientPlatform.CurrentPlatform;
                _session = SingleStepBuildSession.Create(_targetAvatar!, platform);
                _hasSuspendedSession = false;
                _resumeBookmarkValid = false;
                MarkWindowDirty();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _statusMessage = e.Message;
                _statusType = MessageType.Error;
                DisposeSession();
                RefreshUI();
                return false;
            }
        }

        private void ExecuteOneStep()
        {
            try
            {
                _session!.ExecuteCurrentStep();
                UpdateCurrentStepBookmark();
                _statusMessage = null;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                UpdateCurrentStepBookmark();
                _statusMessage = $"Step failed: {e.Message}";
                _statusType = MessageType.Error;
            }

            RefreshUI();
        }

        private void ExecuteTo(int targetIndex)
        {
            try
            {
                _session!.ExecuteUntil(targetIndex);
                UpdateCurrentStepBookmark();
                _statusMessage = targetIndex == _session.Plan.CompleteStepIndex
                    ? "Build complete. The final debug clone is ready for inspection."
                    : null;
                _statusType = MessageType.Info;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                UpdateCurrentStepBookmark();
                _statusMessage = $"Replay stopped: {e.Message}";
                _statusType = MessageType.Error;
            }

            RefreshUI();
        }

        private void RunToStep(int targetIndex)
        {
            if (targetIndex < _session!.Cursor)
            {
                RebuildToIndex(targetIndex);
            }
            else
            {
                ExecuteTo(targetIndex);
            }
        }

        private void StopAndKeepClone()
        {
            try
            {
                var clone = _session!.StopAndKeepClone();
                _session = null;
                _hasSuspendedSession = false;
                _resumeBookmarkValid = false;
                MarkWindowDirty();
                Selection.activeGameObject = clone;
                _statusMessage = "Stopped debugging and kept the clone in the scene.";
                _statusType = MessageType.Info;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _statusMessage = $"Could not keep the clone: {e.Message}";
                _statusType = MessageType.Error;
            }

            RefreshUI();
        }

        private void RebuildToIndex(int targetIndex)
        {
            var targetBookmark = new BuildStepBookmark();
            targetBookmark.Set(_session!.Plan.Steps[targetIndex]);

            if (!TryCreateSession()) return;
            if (!targetBookmark.TryResolve(_session!.Plan, out var remappedIndex))
            {
                UpdateCurrentStepBookmark();
                _statusMessage = "The selected step changed while rebuilding the execution plan.";
                _statusType = MessageType.Warning;
                RefreshUI();
                return;
            }

            ExecuteTo(remappedIndex);
        }

        private void UpdateCurrentStepBookmark()
        {
            if (_session == null) return;
            _currentStep.Set(_session.Plan.Steps[_session.Cursor]);
            MarkWindowDirty();
        }

        private void MarkWindowDirty()
        {
            EditorUtility.SetDirty(this);
        }

        private void DisposeSession()
        {
            _session?.Dispose();
            _session = null;
        }
    }
}