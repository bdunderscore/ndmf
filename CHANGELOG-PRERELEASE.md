# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

### Fixed

### Changed

### Removed

### Security

### Deprecated

## [1.10.1] - [2025-12-15]

### Added
- [#744] Added `AvatarProcessor.OnManualProcessAvatar` event, which allows subscribing to manual avatar builds.

### Fixed
- [#749] Tolerated some cases of null nodes being found in animator traversal, and added additional debugging for this
  case.

## [1.10.0] - [2025-12-03]

### Changed
- [#740] Localization key will be shown as a fallback if no localized title is provided.

## [1.10.0-rc.2] - [2025-11-21]

### Fixed
- Dependencies folder was not actually included, oops.

## [1.10.0-rc.1] - [2025-11-21]

### Added
- [#738] Included depenendencies directory in build for use in non-VRCSDK projects

## [1.10.0-rc.0] - [2025-11-19]

### Fixed
- [#737] Fixed an issue that occasionally resulted in errors while processing previews

## [1.10.0-beta.4] - [2025-11-16]

### Added
- [#734] Exposes the `PropCache` class (previously a Modular Avatar private API), which allows for ComputeContext-aware
  memoization and caching.

### Fixed
- [#733] Improve preview system performance
- [#734] Sometimes, `ComputeContext.FlushInvalidates` would not reliably flush all pending invalidate calls

### Changed

- [#733] `ComputeContext.GetAvatarRoots` will now return only active-in-hierarchy avatar roots

## [1.10.0-beta.3] - [2025-11-03]

### Fixed
- [#731] Mip streaming checks now ignore built-in resources
- [#731] Mip streaming checks now produce a non-fatal error

## [1.10.0-beta.2] - [2025-11-02]

### Added
- [#730] Added `PreviewSession.HiddenRenderers` and `HiddenRenderersDelegate`
- [#719] Added `AvatarProcessor.ManualProcessAvatar()`, which handles platform aware manual avatar builds.

### Fixed
- [#718] Fix NDMF preview in cross platform projects not handling cross platform avatar roots appropriately.
- [#729] Fixed some false positives and build failures with CheckMipStreamingPass

## [1.10.0-beta.1] - [2025-10-27]

### Added
- [#723] Added better error reporting when mip streaming is missing on a texture in a VRChat avatar build

## [1.10.0-beta.0] - [2025-09-20]

### Added

- Exposed `PreviewSession` class, allowing for custom preview behavior to be set for specific cameras.

### Fixed
- [#712] Fix an issue where preview might break if certain internal objects are destroyed unexpectedly.

## [1.9.4] - [2025-09-20]

### Fixed
- [#710] Scene view selection would (still) select proxy renderers instead of originals
- [#710] Sometimes, NDMF preview would get "stuck", particularly after domain reload

## [1.9.3] - [2025-09-19]

### Fixed
- [#709] Scene view selection would select proxy renderers instead of originals

## [1.9.2] - [2025-09-18]

### Fixed
- [#706] `NDMFPreview.GetOriginalObjectForProxy` would return null when called on a proxy renderer while the proxy
  pipeline was not yet fully built (from a preview filter)

## [1.9.1] - [2025-09-17]

### Fixed
- [#705] Fixed an issue where parameter drivers in Add mode might be handled incorrectly when parameter types are harmonized.

## [1.9.0] - [2025-09-13]

### Fixed
- [#702] Converting non-VRChat platform avatars to VRChat resulted in incompletely initialized avatar descriptors.

## [1.9.0-rc.3] - [2025-09-07]

### Changed

- Reverted changes to clone AnimatorControllerParameters on the getter for `VirtualAnimatorController.Parameters`
  due to backwards-compatibility concerns.

### Deprecated

Currently, `VirtualAnimatorController.Parameters` returns an immutable dictionary, but the values in that dictionary
are mutable. This is not intended behavior; a warning has been added that will be logged if the type of parameters
are mutated in-place, and this behavior will break in the future.

Plugins that want to change the type of a parameter should construct a new `AnimatorControllerParameter` object,
and assign an ImmutableDictionary containing it to `VirtualAnimatorController.Parameters` instead.

## [1.9.0-rc.2] - [2025-09-02]

### Added
- Added `IPlatformAnimatorBindings.PreCommitController`

### Fixed
- [#696] Fixed compatibility issue with the new parameter driver correction logic.

## [1.9.0-rc.1] - [2025-09-01]

### Fixed

- [#694] When correcting parameter drivers when parameter types change, multiple intermediate copy stages would be
  generated. This has no negative effect, but made writing tests in downstream packages more complex.

## [1.9.0-rc.0] - [2025-08-31]

### Added
- Expose `NormalizedBlendValues` from `VirtualBlendTree`

### Fixed
- [#675] Fixed comparison function in `SingleObjectQuery.ObserveTransformPosition`
- [#692] Fixed issues where mesh manipulation would fail on readonly meshes, potentially breaking the preview system
  entirely.

### Changed
- [#693] `VirtualAnimatorController` will adjust VRChat parameter drivers to preserve behavior when parameter types are changed
- [#693] `VirtualAnimatorController.Parameters` will now clone `AnimatorControllerParameter` objects on get and set.
- [#690] Now `UIElementLocalizer` prefers `label` property over `text` property

## [1.8.3] - [2025-08-02]

### Fixed
- [#670] Removed noisy debug log
- [#673] Fix issue where `AvatarProcessor.ProcessAvatar` would not respect the passed `INDMFPlatformProvider`.

## [1.8.2] - [2025-07-19]

### Fixed
- [#669] Fixed an issue where viseme modes other than blendshapes were not properly handled

## [1.8.1] - [2025-07-13]

### Fixed
- [#667] NullReferenceException when `VRCPhysBone.radiusCurve` is null

## [1.8.0] - [2025-07-12]

## [1.8.0-rc.0] - [2025-07-02]

### Added
- [#654] [#660] Marked a number of platform APIs as public
- [#661] Added `CompatibleWithContext` attribute for plugin passes.
- [#665] Added `AnimationIndex.GetPPtrReferencedObjectsWithBinding` and a new overload for  
  `AnimationIndex.RewriteObjectCurves` which provides the curve binding to the rewrite callback.

### Fixed
- [#655] Missing dependency to Animation module
- [#664] VRChat proxy animation clips would be reserialized in some cases, causing noise in `git diff` and the like

### Changed
- [#662] The platform selector will now be shown if any platforms other than Generic and VRChat are detected, even without
  experimental features enabled.
  - Experimental features still gate access to the "Generic" platform when no other platforms are detected, as well as
    access to portable components.

## [1.8.0-beta.4] - [2025-06-22]

### Changed
- [#653] Move bone template name determination into the resonite platform plugin

## [1.8.0-beta.3] - [2025-06-22]

### Added
- [#650] Expose radius curves and multiChildType to portable dynamic bones
- [#651] Expose inside bounds flag to portable dynamic bone colliders

## [1.8.0-beta.2] - [2025-06-20]

### Added
- [#644] No alloc overload for `ComputeContext.GetComponentsXXX`
- Made a new `BuildContext` constructor overload with a platform specifier public

### Fixed
- [#642] NDMFViewpoint was handled incorrectly in avatars with a scaled avatar root
- [#647] The VRChat platform extension ignored component enable state when creating portable PB components

## [1.8.0-beta.1] - [2025-05-29]

### Fixed
- [#634] Avoid infinite recursion if an avatar is duplicated during play mode activation
- [#640] Fixed an issue where the viewpoint would be detected incorrectly for VRChat avatars with a scaled avatar root,
  when building for other platforms.
- [#641] During VRChat builds, if a non-VRChat platform was selected, that platform would be used for build transformations.

### Changed
- [#635] The `IVirtualizeMotion.Motion` and `IVirtualizeAnimatorController.AnimatorController` properties now have a nullable type.
  - While this is an API change, it should not result in build failures (but rather, warnings, which should have been there in the first place...)

## [1.8.0-beta.0] - [2025-05-21]

### Changed
- Various changes to `PortableDynamicBone` APIs
- Marked the new components and APIs in 1.8.0 as public/non-experimental

## [1.8.0-alpha.12] - [2025-05-13]

### Added
- Experimental: Added NDMF Viewpoint component
- Experimental: Added Portable Viseme component
- Experimental: Added copy to/from platform UI in NDMF Avatar Root
- Experimental: Added UI to enable/disable experimental features

### Fixed
- [#626] Fixed an issue where an exception would be thrown if the base state corresponding to a synced layer override is destroyed.
- VRChat build hooks did not run certain NDMF passes (in newly added build phases)
- [#625] Null object keyframes break RewriteObjectCurves

## [1.8.0-alpha.11] - [2025-05-01]

### Fixed
- [#607] Fixed an issue where zh-Hans l10n text fallback to zh-Hant.

### Changed
- Improved the UI for Enable-Disable Plugins Window
- [#609] Adjusted the timing of generating portable dynamic bone components, so that optimization passes can delete
  PBs before they are converted.

## [1.8.0-alpha.10] - [2025-04-27]

### Removed
- [#608] Removed some internal shaders that were only used by the Modular Avatar resonite package (they have been moved
  to that package) 

## [1.8.0-alpha.9] - [2025-04-22]

### Fixed
- [#602] Fix issue where, if the hidden "mask" value on the gesture layer in the VRChat avatar controller was missing,
  the avatar would be stuck in "bicycle pose" in gesture manager and Av3Emulator.
- [#604] Fix an issue where certain VirtualClip properties could attempt to modify VRChat proxy animation clips.
- [#604] Fix `VirtualClip.AdditiveReferencePoseTime` setter not working

## [1.8.0-alpha.8] - [2025-04-17]

### Fixed
- [#599] Substate machine transitions were not being enumerated (and thus not processed for MA Parameters renaming)
- [#600] Fix issue where loop time (and other clip settings) were not preserved when there was no additive reference clip.

## [1.8.0-alpha.7] - [2025-04-14]

### Fixed
- [#590] `[RunsOnPlatforms]` and `[RunsOnAllPlatforms]` did not work when applied to a pass class. 
- [#597] Fixed an issue where duplicate layer entries in the VRChat Avatar Descriptor would cause all animator contents
  to be ignored.
- [#591] Fixed a benign `NullReferenceException` at initialization
- [#595] Fixed a NullReferenceException in AnimationIndex
- [#598] Fixed an issue where animation curve paths being rewritten multiple times might be deleted

## [1.8.0-alpha.6] - [2025-04-10]

### Fixed
- [#587] NDMF plugins that modify animators would cause AnimatorLayerControl behaviors to be lost
- [#588] Animator Play Audio paths are not correctly remapped in Merge Animator

## [1.8.0-alpha.5] - [2025-04-08]

### Changed
- Adjusted experimental cross-platform dynamic bone heuristics

## [1.7.5] - [2025-04-08]

### Fixed
- [#585] Build failures when duplicate parameters exist within the same animator controller
  - NDMF will use the last instance of the parameter defined.

### Changed
- [#584] NDMF AnimatorServicesContext will detect when other NDMF plugins have replaced proxy clips with a clone, and
  will revert them back to their original references (provided that they are properly registered in `ObjectRegistry`)

## [1.8.0-alpha.4] - [2025-04-07]

### Added

#### Added (experimental features)
- Added internal APIs and components to support experimental Resonite backend support.
  - These components are very much subject to change in future builds (and are hidden behind the NDMF_EXPERIMENTAL feature flag)

## [1.8.0-alpha.3] - [2025-04-06]

### Added

#### Added (experimental features)
These features are only accessible if you set `NDMF_EXPERIMENTAL` as a script define in your project settings,
and are subject to change in future releases (are not subject to semver).

- Added platform selection UI to the build window. Note that this is displayed only if `NDMF_EXPERIMENTAL` is set as a
  script define.
- Added `NDMF Avatar Root` experimental component.

### Fixed
- [#573] Fixed an issue where additive reference pose clip references in animator override controllers resulted in a
  build failure

### Changed
- [#574] Adjusted the API for platform compatibility declaration
- Disabled platform filtering for the moment. This will be enabled in an experimental package in a later change.

## [1.8.0-alpha.0] - [2025-04-05]

### Added
- [#572] Added `Sequence.OnAllPlatforms()` and `Sequence.OnPlatforms()` methods, and the `WellKnownPlatforms` class.
  - These are in preparation for NDMF's multiplatform support, and configure which platforms passes run on.
  - By default, passes run only on VRChat SDK 3.0.
- [#572] Added `[RunsOnAllPlatforms]` and `[RunsOnPlatform]` attributes as well

## [1.7.4] - [2025-04-05]

### Fixed
- [#573] Fixed an issue where additive reference pose clip references in animator override controllers resulted in a
  build failure

## [1.7.3] - [2025-04-04]

### Fixed
- [#570] Additive reference poses were not preserved in animator cloning

## [1.7.2] - [2025-04-03]

## [1.7.2-rc.0] - [2025-04-03]

### Removed
- The `IVirtualizedMotion` interface was incorrectly exposed in the `API` namespace. It has been moved to `nadena.dev.ndmf.animator` instead.
  **This is a semver breaking change**

### Fixed
- [#567] "Unreachable code reached???" error on some animators
- [#568] VirtualClip did not support discrete curves

## [1.7.1] - [2025-04-02]

### Fixed
- [#564] Some assets assumed that MA/NDMF stripped animation events; this change ensures that they are indeed stripped.

## [1.7.0] - [2025-04-01]

### Fixed
- [#563] Fix issue where `InvalidOperationException`s could be generated continually

## [1.7.0-rc.2] - [2025-03-30]

### Fixed
- [#561] Fixed an issue where `VRC Animator Layer Control` behaviors might be lost on `VirtualControllerContext` reactivation
- [#561] Fixed an issue where layers might be deleted on `VirtualControllerContext` reactivation
- [#561] Fixed an issue where the build might fail with an exception when reactivating `VirtualControllerContext`

## [1.7.0-rc.1] - [2025-03-28]

### Changed
- [#559] Performance improvements

## [1.7.0-rc.0] - [2025-03-22]

### Added
- [#558] Add support for bulk-rewriting object curves in `AnimationIndex`

### Fixed
- [#554] Fix issues where the first layer of an animator controller might not be interpreted as having weight 1

## [1.7.0-beta.0] - [2025-03-17]

### Fixed
- [#553] Path renaming ignored motion overrides on synced layers 

## [1.7.0-alpha.4] - [2025-03-14]

### Added
- [#550] Added setter to `VirtualAnimatorController.Layers`

## [1.7.0-alpha.3] - [2025-03-11]

### Fixed
- [#547] ProcessAvatar ignores enclosing `ErrorReport.CaptureErrors` scopes
- [#549] `VirtualStateMachine.AllStates()` did not properly visit sub-state-machines

## [1.7.0-alpha.2] - [2025-03-10]

Note: This alpha release contains breaking changes to APIs introduced in `alpha.0` and `alpha.1`.
Please upgrade modular avatar at the same time as updating to this release. This does not affect
stable APIs.

### Added
- [#545] `IVirtualizeMotion` interface
- [#545] Additional methods on `IVirtualizeAnimatorController` (breaking change)

### Fixed
- [#543] Exceptions thrown when deactivating extension contexts can result in an infinite loop
- [#544] `INDMFEditorOnly` needed to be in the runtime assembly

### Changed
- [#545] `VirtualControllerContext` now exposes the dictionary of animator controllers via a property
  `VirtualControllerContext.Controllers`

### Removed
- [#545] Removed indexing operator overload on `VirtualControllerContext` (breaking change)

## [1.7.0-alpha.1] - [2025-03-08]

### Added
- [#532] Window to temporarily disable some NDMF plugins
- [#541] Add `INDMFEditorOnlyComponent` interface

### Fixed
- [#529] Don't error out when an animation clip contains multiple bindings for the same property

## [1.7.0-alpha.0] - [2025-02-16]

### Added
- [#467] Added the `AnimatorServicesContext` and lots of supporting APIs for working with animator controllers.

## [1.6.8] - [2025-02-16]

### Fixed
- Avoid NRE that occurs when a null is observed in a PublishedValue (#526)

## [1.6.7] - [2025-01-26]

### Fixed
- Fixed another source of NullReferenceErrors after deleting game objects (#522)

## [1.6.6] - [2025-01-25]

### Fixed
- NullReferenceErrors when there are missing components, breaking scene view movement (#520)

## [1.6.5] - [2025-01-24]

### Fixed
- Performance improvements (#515 #516)
- "Test build" button is disabled after test-building an avatar that is not a scene root (#518)

## [1.6.4] - [2025-01-18]

### Fixed
- [#507] Fixed a potential editor freeze bug

### Removed
- [#507] Removed the `NDMFSyncContext.RunOnMainThread` API which was accidentally addded in 1.6.3.
  This might be re-added in 1.7.0.

## [1.6.3] - [2025-01-15]

### Fixed
- [#500] Fixed a thread-safety issue which could cause various issues, including editor performance degradation.
- [#506] Fixed an issue with the preview preferences dialog

## [1.6.2] - [2024-12-04]

### Added
- [#486] Add Simplified Chinese support
### Fixed
- [#487] Fixed a performance issue where all assets would potentially be loaded on reimport, taking a lot of time and
  memory in the process

## [1.6.1] - [2024-11-28]

**This release does not confirm to semver**. Due to unexpected compatibility issues with `IExtensionContext.Owner`, this
release removes this API. Although this is technically a semver violation, it's better than the accidental violation in
1.6.0.

**Note**: In 1.7.0 NDMF will add sentinel default methods to all interfaces to ensure that downstream packages are not
using outdated versions of C# to build. The NDMF project will not consider this to be a breaking change after 1.7.0.
Please update your projects to build with modern C# before then.

### Fixed
- [#482] Additional performance improvements

### Removed
- [#483] Removed `IExtensionContext.Owner` due to compatibility issues.

## [1.6.0] - [2024-11-28]

### Added
- [#479] Added `IAssetSaver` and `SerializationScope` APIs.
  - NDMF plugins are encouraged to use `IAssetSaver.SaveAsset` instead of directly accessing `AssetContainer`. This will
    split saved assets across multiple files, to avoid performance degradation as the number of assets in a container
    grows.
- [#480] Added `IExtensionContext.Owner` API. Setting this property will allow errors to be correctly attributed to the
  plugin that contains an extension context.
- [#472] [#474] Added the `DependsOnContext` attribute, for declaring dependencies between extension contexts.
- [#473] Added `BuildContext.SetEnableUVDistributionRecalculation` to allow opting out from the automatic call to
  `Mesh.RecalculateUVDistributionMetrics` on generated meshes.
- [#478] Added `ProfilerScope` API
- [#481] Added `NDMFPreview.GetOriginalObjectForProxy` API

### Fixed
- [#479] Unpacking generated asset containers can break inter-asset references

## [1.5.7] - [2024-11-17]

### Changed
- [#468] Performance improvements
- [#471] Improve compatibility with VRChat's streaming mipmaps feature
  - NDMF will now call `Mesh.RecalculateUVDistributionMetrics` on all generated meshes on upload. An API to opt-out will
    be provided at the next major version release.

## [1.5.6] - [2024-10-19]

### Fixed

- [#461] Selection outlines in scene view do not work reliably when preview system is active
- [#459] "Proxy object was destroyed improperly! Resetting pipeline..." error appears frequently
- [#460] Preview system fails to recover when the primary proxy is destroyed
- [#462] NDMF console fails to appear when an ExtensionContext throws an exception

## [1.5.5] - [2024-10-15]

### Fixed

- [#454] Scene view selection outlines flicker in some cases
- [#450] Improved performance when a large number of object change events are generated (e.g. when exiting animation
  mode)
- [#441] Fixed an issue where the preview object pickable status could get out of sync with the original
- [#444] Fixed an issue where the preview system broke drag-and-drop of materials onto the scene view
- [#444] Fixed an issue where the preview system broke drag-to-select in the scene view

### Changed

- [#451] Changed preview system to apply to more cameras. Specifically, we now handle all cameras with no render
  texture set, to ensure that Game Mode behaves as expected.

## [1.5.4] - [2024-10-05]

### Added

- [#439] Added a menu option to disable NDMF processing on build (`Tools -> NDM Framework -> Apply on Build`).
  - This setting will survive domain reloads but not editor restarts.

### Fixed

- [#438] Fixed an issue where exceptions would be thrown when scenes are unloaded

## [1.5.3] - [2024-10-03]

### Fixed
- [#435] Fixed an issue where `File -> Save As` could break due to the internal preview scene becoming selected

## [1.5.2] - [2024-10-02]

### Fixed
- [#434] Pass Harmony ID to `Harmony.UnpatchAll()` to avoid double-unpatching methods
- [#433] Remove stray debug print

## [1.5.1] - [2024-09-30]

### Fixed
- [#431] Compile error when test framework is not present

## [1.5.0] - [2024-09-29]

### Removed
- Unity 2019 is no longer supported.

### Added
- [#244] Added a framework that can be used to override the rendering of an object without modifying the object itself
- [#297] Added UI for turning preview on/off at a plugin or pass level
- [#244] Added a framework for observing scene object changes and reacting to them.
- [#244] Added `SelfDestructComponent` (useful for hidden preview-only components)
- [#312] Added a default value field to ProvidedParameter
- [#312] Added support for invalidating ComputeContext to ParameterInfo
- [#360] Added `AsyncProfiler` to help profile code running in Tasks
- [#365] Added debug tool to profile long editor frames
- [#407] Added `ProvidedParameter.ExpandTypeOnConflict` to resolve parameter type mismatch automatically
- [#410] Added `NDMFSyncContext` API
- [#424] Added tracing system for the preview/invalidation system

### Fixed
- [#260] [ChilloutVR] Fix: Build fails due to CVRAvatar preventing recreation of Animator (contributed by @hai-vr)
- [#261] [ChilloutVR] feat: don't build the avatar when ChilloutVR shows the upload UI (contributed by @hai-vr)
- [#280] Console warnings issued whenever `.unity` (scene) files are saved
- [#341] Font rendering breaks on scene change
- [#385] Fix: parameter introspection used default value from child, not parent
- [#386]
  Workaround [VRCSDK bug](https://feedback.vrchat.com/sdk-bug-reports/p/string-conversion-errors-from-runtimeassemblygetcodebase-with-japanese-locale-an)
  caused by non-ASCII project paths.
- [#399] Fix: Parameter introspection did not skip EditorOnly objects
- [#416] Fixed issues where assets would not properly be tracked due to C# object recreation edge cases
  (removed ObjectIdentityComparer)

### Changed
- [#266] NDMF language defaults to being based on the system language.
- [#408] Unserialized assets will be serialized after the Transforming phase completes (before e.g. VRCFury runs)

## [1.4.1] - [2024-05-12]

### Added
- Manual Bake avatar context menu for projects with Modular Avatar (#234)
- `__Generated` folder is not removed after building avatar (#235)
- Add Traditional Chinese support (#230)

### Fixed
- Workaround VRCSDK bug where stale PhysBones state could be retained over play mode transitions (#231) 
- Show object name when we're unable to find the actual GameObject, in error display UI (#224)
- Rerender error report window when leaving play mode (#237)
- Deduplicate object references in NDMF console error reports (#237)

### Changed
- Renamed `NDMF Error Report` to `NDMF Console` (#222)
- Changed Japanese strings a bit (#222)

### Removed

### Security

### Deprecated

## [1.4.0] - [2024-03-27]

### Added
- Added a new API to allow NDMF plugins to declare and introspect expressions parameter usage (#184)
- Added an API to select a non-broken font for use in UI Elements based on the current locale (#190)
- Added `[NDMFInternal]` attribute (#217)
- Added a debug feature to profile a test build (#214)
- Added `ParameterProvider.SubParameters()` for `ParameterNamespace.PhysBonesPrefix` parameters (#196)
- Added support for declaring ProvidesParametersFor via base classes and interfaces (#198)

### Fixed
- Specify zh-* font to make the font normal (#206)
- Hide certain subassets after manual bake and/or extracting assets (#212)
- Fixed issues with capitalization in language preference (#215)
- Apply on play isn't suppressed when Av3mu is present (#200)
- UIElementLocalizer could fail to find localized strings in some cases (#189)

### Changed
- In ParameterProvider, the parameter type of PhysBone Contact Receiver is now the type corresponding to the receiver type. (#209)

## [1.3.7] - [2024-03-06]

### Fixed
- Fix language code not recognizable due to inconsistent case

## [1.3.6] - [2024-02-27]

### Fixed
- Having multiple language variants that differ in case (e.g. `en-us` vs `en-US`) would break the language switcher (#176)

## [1.3.5] - [2024-02-26]

### Fixed
- Incorrect language switcher behavior when locales were filtered from display (#175)

## [1.3.4] - [2024-02-18]

### Fixed
- Incorrect language display names for some locales (#171)

### Changed
- Uncultured language variants (e.g. `en`) are not displayed in the language switcher when cultured variants (e.g.
  `en-US`) are registered. (#171)

## [1.3.3] - [2024-02-12]

### Fixed
- VRCSDK builds fail due to UnityEditor-only type references (#167)

## [1.3.2] - [2024-02-12]

### Fixed
- Avatar names with leading/trailing whitespace broke builds (#161) 
- Ave3mu's "Run Preprocess Avatar Hook" option was force-enabled even when apply on play was disabled (#160)
- Animator cloning broke "Among Us" follower due to sus processing order (#165)

### Changed
- Changed the hook processing logic to closer match VRCSDK (improves compatibility with VRCF and other external hooks)
  (#162)

### Removed

### Security

### Deprecated

## [1.3.1] - [2024-02-06]

### Fixed
- Apply on Play did not work for non-VRChat avatars or environments (#153)
- Error if some scene is not loaded (#156)
- Outfit sub-animators can cause transforms to move when using GestureManager (#147)

### Changed
- Run all preprocess hooks in Apply On Play processing, to better align with VRCFury handling. (#145)

## [1.3.0] - [2024-01-29]

### Added
- New localization framework
- New error reporting framework
- API to record when one object is replaced by another
- Added a non-component-based check for double execution of hooks (#142)
- Exposed the qualified name variant of `Sequence.BeforePass` (#109)

### Changed
- Adjusted hook processing order to improve compatibility with VRCFury (#122)
- Worked around a hack in VRCFury that broke optimization plugins (#126)

### Fixed
- Play mode processing fails when installed via UPM (#89)

## [1.2.5] - [2023-11-15]

### Fixed
- GUID collisions with packages derived from the VRChat template package (#84)

## [1.2.4] - [2023-11-12]

### Fixed
- Duplicate references error when Lyuma's Av3emulator is installed due to Unity bug (#80)

## [1.2.3] - [2023-11-11]

### Added

### Fixed
- Fixed an issue where apply on play might not work when multiple scenes are open (#61)
- Fixed an issue where Apply on Play would not work properly when Lyuma's Av3Emulator had preprocess hooks disabled
  (bdunderscore/modular-avatar#516) (#64)

### Changed

- Make Apply on Play non-persistent, as users seem to frequently have issues with it left turned off.

### Removed
- Removed a vestigial "Avatar Toolkit -> Apply on Play" menu item, which didn't do anything when selected. (#70)

### Security

### Deprecated
- Deprecated APIs for finding avatar roots defined outside RuntimeUtil (#73)

## [1.2.2] - [2023-10-09]

### Fixed
- Fixed an issue where apply on play might not work when multiple scenes are open (#61)

## [1.2.1] - [2023-10-08]

### Fixed
- Removed .git folder from built packages

## [1.2.0] - [2023-10-07]

### Fixed
- Re-released 1.1.1 as 1.2.0 as it contained new APIs (#34). Please use 1.2.0 for version declarations if these new APIs
  are needed.

## [1.1.1] - [2023-10-07]

### Added
- Exposed APIs for finding avatar roots in RuntimeUtil (#34)
- Support Unity projects without VRCSDK (#50)
- Add warning when an outdated version of Modular Avatar is installed (#54)

## [1.1.0] - [2023-10-05]

### Added
- Added toplevel menu for manual bake avatar, even when MA is also installed (#35)
- Added support for multiple ExportsPlugin declarations (#40)
- Added additional convenience overloads for BeforePlugin and AfterPlugin

### Fixed
- Create ApplyOnPlayGlobalActivator correctly when creating and opening scenes (#31)
- Time resolution on the plugin sequence display is milliseconds, not 0.01 ms (#43)
- BeforePlugin/AfterPlugin declarations don't work (#44)

### Changed
- Position of the Plugin sequence display window is now preserved after restarting the Unity Editor (#42)

### Removed

### Security

## [1.0.2] - [2023-09-26]

### Added
* Added missing license declaration

### Fixed
- Generated assets are not visible on the Project window (#26)
- Apply on play is not disabled if av3emulator is active on other scene
- Avatar names with non-path-safe characters break builds (again) (#18)

### Changed

### Removed

### Security

## [1.0.1] - [2023-09-24]

### Fixed

- Move optimization passes to -1025 to ensure it runs before VRChat destroys IEditorOnly components.

## [1.0.0] - [2023-09-24]

### Added

- Initial public release

### Fixed

- Remove missing script components from the avatar at the start of the build.
- Fixed build failures when avatar names contained non-path-safe characters (#18)
- Suppress apply-on-play when Lyuma's Av3Emulator is active (improves compatibility) (#16)

### Changed

### Removed

- Samples have been removed from the build, as they are not part of the stable NDMF API surface area. They'll probably
  be moved to a separate repository at some point.

## [0.4.0] - [2023-09-20]

0.4.0 and prior releases did not Keep A Changelog.
