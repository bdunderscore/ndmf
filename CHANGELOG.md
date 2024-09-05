# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

### Fixed
- [#379] Fix CurrentThreadIsMainThread assertion in AsyncProfiler

### Changed

### Removed

### Security

### Deprecated

## [1.5.0-rc.5] - [2024-09-04]

### Fixed

- [#379] Fix CurrentThreadIsMainThread assertion in AsyncProfiler

## [1.5.0-rc.4] - [2024-09-02]

### Fixed
- [#372] Fix an issue where the preview pipeline would be rebuilt every editor frame
- [#369] Fix a `NullReferenceException` generated when previewed renderers are destroyed
- [#371] Performance improvements in change monitoring
- [#374] Suppress processing of renderers deleted during pipeline processing

### Changed
- [#373] Set time limits for preview processing to avoid editor freezes

## [1.5.0-rc.3] - [2024-09-01]

### Added
- [#360] Added `AsyncProfiler` to help profile code running in Tasks
  - Also added profiler scopes for `IRenderFilter.Create`/`IRenderFilterNode.Refresh`.
- [#361] Added `IRenderFilterNode.OnFrameGroup`
- [#365] Added debug tool to profile long editor frames
- [#368] Relaxed generic constraints on `ComputeContext.GetComponent*<>` to allow interfaces to be queried for

### Fixed
- [#362] Fixed unclosed profiler scope in TargetSet
- [#355] Excessive invalidation when scene view visibility states change
- [#363] Reduce GC pressure caused by `ComputeContext.GetComponent`
- [#367] When `IRenderFilter.Create` is invoked when refreshing a filter node, it would get an `ObjectRegistry`
  containing items registered in the prior generation of the node.

### Changed
- [#364] Prevent creation of overlapping render groups in the same `IRenderFilter`

## [1.5.0-rc.2] - [2024-08-28]

### Fixed

- [#351] Fix issue where ObjectRegistry contents are not properly inherited across filter node refreshes.
- [#354] Fix "rendering stopped" errors on Mac editors

## [1.5.0-rc.1] - [2024-08-25]

### Added
- [#350] Added additional ComputeContext-related APIs.
  - `ComputeContext.Invalidate`, the `ComputeContext(string)` constructor, `ComputeContext.Invalidates`, and
    `ComputeContext.InvokeOnInvalidate` are now public.
  - Added the `GetAvatarRoots(this ComputeContext)` and `GetAvatarRoot(this ComputeContext, GameObject)` extension methods.

## [1.5.0-rc.0] - [2024-08-21]

### Fixed
- [#343] [#346] TargetSet is not invalidated when GetTargetGroups context is invalidated
  (new preview components were sometimes ignored)
- [#347] NullReferenceException from SetupRenderingMonitoring on scene change

## [1.5.0-beta.5] - [2024-08-18]

### Fixed
- [#341] Font rendering breaks on scene change

### Changed
- [#340] Render filters operating on disabled renderers are now culled (unless they declare that the might enable those
  renderers) to save frametime.

## [1.5.0-beta.4] - [2024-08-18]

### Added

- [#330] `NDMFPreviewSceneManager` added, allowing other libraries to easily hide temporary objects.

### Fixed
- [#320] Render nodes are not correctly reused across frames
- [#321] Fix GetTargetGroup being called on every pipeline invalidation
- [#327] Z-fighting occurs in prefab isolation view
- [#328] Fix issue where preview system is not reinitialized after a scene change
- [#329] Fix issue where scene root monitoring breaks after a domain reload
- [#334] Fix objects under preview not respecting scene visibility state

### Changed

- [#330] Preview objects are now hidden by placing them in a hidden subscene, instead of harmony patching the hierarchy.
  This should improve stability in general.
- [#335] Skip preview rendering on all cameras except the scene view camera and the VRCSDK thumbnail camera. 
- [#338] Don't disable sub-options when a preview plugin is disabled.

## [1.5.0-beta.3] - [2024-08-04]

### Added
- [#312] Added a default value field to ProvidedParameter
- [#312] Added support for invalidating ComputeContext to ParameterInfo

### Fixed
- [#312] Fix issues preventing preview overrides from changing object enable states

## [1.5.0-beta.2] - [2024-08-03]

### Added

- [#297] Added UI for turning preview on/off at a plugin or pass level
- [#301] [#302] Added API for changing the controls used to manipulate preview enable/disable state

### Fixed

- [#298] Fixed issue where the scene view was sometimes not refreshed when the pipeline build completes
- [#309] NullReferenceException from GetParametersForObject when encountering a missing component
- [#311] Fix issue where MeshRenderers are shown with incorrect scale

## [1.5.0-beta.1] - [2024-07-28]

### Added
- [#297] Added UI for turning preview on/off at a plugin or pass level

### Fixed
- [#298] Fixed issue where the scene view was sometimes not refreshed when the pipeline build completes

## [1.5.0-beta.0] - [2024-07-28]

### Added
- [#287] Added PublishedValue class
- [#288] Added support for passing ObjectRegistry to IRenderFilter
- [#289] Added support for binding multiple render filters to a single pass

### Fixed
- [#283] Cached proxy objects are visible after exiting play mode 
- [#285] Harmony patches break when keyboard is used to open/close objects in the hierarchy in some cases. 
- [#284] Preview objects do not inherit scale when they are previewing a Skinned Mesh Renderer with no root bone.

### Changed
- [#294] Restructured namespace and assembly hierarchy to remove references to "Reactive Query".

### Removed
- [#294] Removed some unimplemented APIs in preparation for 1.5.0 release.

## [1.5.0-alpha.3] - [2024-07-01]

### Added
- [#279] Added an `Observe` overload which checks for changes to an extracted value, to help respond to animation mode
  changes

### Fixed
- [#280] Console warnings issued whenever `.unity` (scene) files are saved

### Changed
- [#273] Preview system now calls `Refresh` to avoid double computation
- [#266] NDMF language defaults to being based on the system language.

### Removed
- [#277] The `ReactiveQuery` API has been removed to reduce the scope and complexity of this upcoming release

## [1.5.0-alpha.2] - [2024-06-16]

### Added

- [#255] Added support for passing context information along with preview target groups.

### Fixed

- Various bugs in preview system
- [#257] Proxy renderers no longer appear in the hierarchy.
- [#260] [ChilloutVR] Fix: Build fails due to CVRAvatar preventing recreation of Animator (contributed by @hai-vr)
- [#261] [ChilloutVR] feat: don't build the avatar when ChilloutVR shows the upload UI (contributed by @hai-vr)

### Changed

- [#256] Certain CommonQueries now ignore hidden and unsaved objects, to avoid infinite update loops.
- [#269] ReactiveValues now invalidate only after their dependencies finish computation
- [#258] The `WhatChanged` and `Reads` flags on `IRenderFilter` are now of an enum type `RenderAspects`

## [1.5.0-alpha.1] - [2024-06-06]

### Changed (since alpha.0)

- Redid `IRenderFilter` API

## [1.5.0-alpha.0] - [2024-06-02]

### Added

- [#244] - Added a framework that can be used to override the rendering of an object without modifying the object itself
  - NOTE: The API for this is still in flux and will likely change in further alpha releases.
- [#244] - Added a framework for observing scene object changes and reacting to them.
- [#244] - Added `SelfDestructComponent` (useful for hidden preview-only components)

### Removed

- Unity 2019 is no longer supported.

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
