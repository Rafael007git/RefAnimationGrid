# Reference Animation Grid (RefAnimationGrid)

An advanced 3D rotoscoping and reference projection tool for **She Will Punish Them**, built as a BepInEx plugin. It allows animators and modders to project synchronized reference image sequences (PNG) or videos (MP4) directly onto customizable in-game meshes (walls, easels, frames) during **Free Pose Mode** to easily align bones and recreate precise poses using gizmos.

## Key Features

- **Dual Modes (Images & Video):** 
  - **Image Mode:** Seamlessly cycles through sorted PNG frame sequences from isolated directories.
  - **Video Mode (.mp4):** Automatically detects video files, hooks into Unity's native `VideoPlayer` (with audio), and streams video directly onto the reference wall.
- **Smart Adaptive Scaling:** Automatically reads the aspect ratio of the loaded video or textures and scales the vertical canvas width accordingly, keeping the default world space dimensions completely undistorted.
- **Two-Way Timeline Pipeline:**
  - **Mode 1 (Play and Select):** Play/Pause video on the fly, scrub frame-by-frame, and save key poses into a unified centralized JSON database.
  - **Mode 2 (Reference View):** Filters out the clutter and skips video playback strictly across your curated list of selected keyframes.
- **Ergonomic Controls & Input Routing:**
  - Uses a **front hemisphere dot-product algorithm** to calculate camera orientation and proximity. Input keys affect only the closest focused canvas in front of you.
  - Controls are repositioned into a clean IMGUI panel at the bottom-center of the screen.
  - **Super-Scrubbing:** Press `[ / ]` for single frames, hold `Shift + [ / ]` for 100-frame jumps, or `Ctrl + [ / ]` for 500-frame jumps. Press `H` to toggle UI visibility.
- **Modular Framework:** Clean C# architecture split into three optimized components (`TextureRepository`, `BaseGridController`, `Plugin`) executing native Unity API logic without background search overhead.

## Installation & Folder Structure

1. Compile the solution and place `RefAnimationGrid.dll` into your `...\BepInEx\plugins\` directory.
2. Run the game once to auto-generate the file tree, or manually set up folders as follows:

```text
BepInEx/
└── plugins/
    ├── RefAnimationGrid.dll
    └── RefAnimationGrid/
        ├── Walls/       <-- Put square/grid PNG frame sequences here
        ├── Vertical/    <-- Vertical PNG images for vertical easels/frames
        ├── Horizontal/  <-- Horizontal PNG images for horizontal easels
        └── Video/       <-- Put your '.mp4' reference video here (saves 'keyframes_db.json' here)
```

## Dependencies
- BepInEx 5.x
- `bugerry.ModToolExtension` (for dynamic Steam Mod Tool asset database injections)
