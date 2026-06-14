# Changelog

## 2026.06 - 2026-06-14

Fezzing Through!

#### Features

- `ChrisEditor`: Symmetry modes for geometry and paint tools
- `ChrisEditor`: Bucket tool and reworked paint tools with common/last-picked color history
- `ChrisEditor`: Emission painting mode with trixel edge line display
- `ChrisEditor`: Texture export and import
- `ChrisEditor`: Multi-orientation trixel face selection
- `ChrisEditor`: Reworked tools modeled after Felt / MagicaVoxel workflows
- `JadeEditor`: Overhauled MapTree visualization and UX with separate properties window
- `JadeEditor`: World map generation algorithm based on level data
- `EddyEditor`: Overlapping triles support with layer selector
- `EddyEditor`: Camera speed control for first-person view
- `EddyEditor`: Black hole volume rendering
- `EddyEditor`: Bulk property editing for multi-trile selection
- `EddyEditor`: Movement path workflow improvements and trile group path editing
- `EddyEditor`: Trile group creation from a single trile
- `EddyEditor`: Level previewer with faraway thumb and map screen export
- `EddyEditor`: Rain preview mesh
- `LukeEditor`: Asset picker support
- `DiezEditor`: Asset picker support
- `ZuEditor`: Font atlas texture edit tracking and refactor into focused context components
- Asset picker for asset selection across editors
- Trile set selection in level creation dialog
- FEZ-specific asset type icons via `@icons` font in File Browser
- Manual display scaling alongside automatic HiDPI scaling
- Dynamic window title
- Clock pause button
- `GitInfo` package with commit hash surfaced in menu bar and About window
- Assembly metadata and authors credit in About window
- CI/CD workflows added

#### Fixes

- `ChrisEditor`: Fixed trixel geometry for non-unit object sizes
- `ChrisEditor`: Fixed parallel face selection bleeding through depth layers
- `ChrisEditor`: Fixed performance issue when accessing visible trixels
- `ChrisEditor`: Fixed geometry serialization, trile storage, and UV offset on Right/Back faces
- `ChrisEditor`: Fixed undo crashes in TrileSet and ArtObject contexts
- `ChrisEditor`: Fixed selection issues in paint tool
- `EddyEditor`: Fixed stale visuals after undo/redo
- `EddyEditor`: Fixed volume mesh rendering on top of liquid mesh
- `EddyEditor`: Fixed water and rain rendering on top of level geometry
- `EddyEditor`: Fixed offset trile bounds and displacement detection
- `EddyEditor`: Fixed rotation widget cross-contamination for AOs and BPs
- `EddyEditor`: Fixed syncing between Background Plane instance and its mesh
- `EddyEditor`: Fixed trile cursor overlay leaking into placement tools
- `EddyEditor`: Fixed stale trile paint hover/raycast usage
- `EddyEditor`: Fixed null refs on overlapped triles and editable arrays
- `EddyEditor`: Fixed camera panning conflict with active gizmo drag
- `EddyEditor`: Fixed sky partial revisualization on level changes
- `JadeEditor`: Fixed map tree generator root selection modal
- `LukeEditor`: Fixed sky layer properties header and shadow preview quad rendering
- Fixed UI scaling on high-DPI displays
- Fixed editable collection change tracking
- Fixed `Editable*` methods not registering undo history
- Fixed resource extractor modal visibility from welcome splash
- Fixed editor crashing when resolving relative path on root
- Fixed music not loading from PAK archives
- Fixed opening some textures from PAK archives
- Fixed ImGui font atlas native data lifetimes on Release builds
- Fixed re-entrant crash when resolving relative paths on root

#### Build

- Updated target framework to .NET 10 and C# 14
- Updated `FNA` to version `26.05` and `FNALibs`; dropped x86 binary support
- Replaced `FEZRepacker` submodule with NuGet package (updated to 1.3.1)
- Bundled editor assets as embedded assembly resource in Release builds
- Added script to speed up shader compilation on Wine for CI
- Added `fxc` from DirectX SDK (June 2010) for `fx_2_0` shader compilation

## 2026.04 - 2026-04-11

The editor is major features complete!

#### Showcase

https://youtu.be/bksXuaovHVk

#### Features

- `EddyEditor`: Level editor
- `MuEditor`: NPC metadata
- `TexViewer`: Static and animated textures
- `LukeEditor`: Sky assets
- `RickViewer`: Sound effect preview with OGG and WAV playback
- GLTF diorama export for levels (available inside `EddyEditor`)
- Asset browser with thumbnail cache
- Persistent app state and recent files per provider
- Status bar with input hints
- Fog support for materials and Art Objects
- Hardware-instanced star field for `JadeEditor`
- View menu with File Browser toggle

#### Fixes

- Numerous EddyEditor fixes (camera, gizmo, trile deselection, context menus, scaling)
- File Browser improvements (context menu, path display, deselect on open)
- Ctrl key penalty for proper shortcut handling
- Rendering switched to DFS; pre-cached BasicEffect instances
- Fixed re-entrant DestroyActor crash, OGG stream leak, editor tab stability

## 2026.03 - 2026-03-05

First public release (**non-production ready!**).

#### Features

- Open PAK files and folders with extracted assets (XNB and FEZRepacker formats)
- Extract assets from PAK
- `ChrisEditor`: ArtObjects and TrileSets
- `DiezEditor`: Tracked Songs
- `JadeEditor`: World Map editor (live and interactive)
- `PoEditor`: Localization files
- `SallyEditor`: Save files (PC format only)
- `ZuEditor`: SpriteFonts

#### Supported platforms

- Windows x64
- Linux x64
- macOS ARM64
