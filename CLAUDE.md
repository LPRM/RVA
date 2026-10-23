# AR Labyrinth

University AR project (Virtual and Augmented Reality, M.EIC/FEUP). Deadline: **6 November 2026**. Keep the scope small; prefer simple, working solutions over ambitious ones.

## Concept

A single-player tilt-maze game inspired by the ball labyrinth puzzles in *Resident Evil Village*. A printed maze marker carries a 3D labyrinth. Tilting the marker makes a ball roll: lowering the left edge rolls the ball left. Gates block the path, and the player opens them by holding a coloured key marker close to them.

Runs on a **laptop (Windows) with the built-in webcam**, using Unity + Vuforia.

## Gameplay

- **Calibration:** the player holds the maze marker flat and presses Calibrate. This stores the "level" orientation. Gravity is applied relative to that stored orientation, so tilting the marker away from it moves the ball.
- **Ball:** Unity Rigidbody, standard physics.
- **Key markers:** each key has a colour matching a gate. When the key is within a set distance of its gate (measured in the maze marker's local space), the gate opens; when it moves away, the gate closes.
- **Holes:** falling into a hole resets the ball to the last checkpoint.
- **Win:** reaching the exit completes the level and shows the time.

## Markers (Vuforia image targets)

| Marker | Role | What is read from it |
| --- | --- | --- |
| Maze board | Carries the labyrinth | Tilt compared with the calibrated "level" pose |
| Red key | Opens red gates | Distance to each red gate |
| Blue key | Opens blue gates | Distance to each blue gate |

Key positions are always converted into the maze marker's local coordinates (`mazeTransform.InverseTransformPoint(...)`), so behaviour does not depend on where markers are relative to the camera.

## External data: weather

Fetch current weather for Porto from Open-Meteo (free, no API key) when a level starts, using `UnityWebRequest`.

- **Rain:** rain particles and lower floor friction.
- **Clear:** normal friction and bright lighting.
- **Night:** darker scene lighting.
- **No connection / error:** fall back to clear weather.

## Interface

- **Start menu:** Play, level select, Quit.
- **Levels:** three small mazes (one gate → two gates → two gates plus more holes).
- **Modes:** Normal and Time Trial (countdown). Single player only.
- **Undo:** restore ball position and gate states to the previous checkpoint (keep a stack of snapshots).
- **Scene rotation:** slider that rotates the maze around its vertical axis without affecting tilt.
- **HUD:** timer, current weather, Calibrate and Pause buttons.

## Models and animation

- Maze walls and floor, metal ball, gates, goal. From the Unity Asset Store or simple Blender models. Every external asset must be credited in the report, so record source and licence in `Assets/CREDITS.md` when adding one.
- Gates slide open/closed (Animator or simple lerp). Goal plays a particle effect.
- One directional light, adjusted by the weather.

## Technical notes for Claude

- **Vuforia version:** use the Vuforia Engine 10+ API only. Use `ObserverBehaviour` and its `OnTargetStatusChanged` event for tracking state. Do NOT use the old `TrackableBehaviour` / `ITrackableEventHandler` / `DefaultTrackableEventHandler` APIs.
- **Unity version:** 6.6. Vuforia version: 11.4.4.
- **Physics:** set `Physics.gravity` each `FixedUpdate` from the calibrated orientation. Smooth the maze marker's pose over a few frames to reduce jitter. Clamp effective tilt to about 30 degrees. Use reasonably thick walls.
- **Tracking loss:** if the maze marker is not tracked, pause the ball (set Rigidbody to kinematic) instead of letting it fall.
- **Scenes and prefabs:** prefer writing C# scripts and describing the Inspector setup to me, rather than editing `.unity` or `.prefab` YAML files directly. Editor setup (ARCamera, ImageTargets, Vuforia license, target database) is done manually by us.
- **Code style:** one MonoBehaviour per responsibility (e.g. `MazeGravity`, `KeyGate`, `WeatherService`, `CheckpointManager`, `GameManager`, `UIManager`). Expose tunable values with `[SerializeField]`. Keep scripts in `Assets/Scripts/`.

## Risks

| Risk | Mitigation |
| --- | --- |
| Tracking jitter makes the ball bounce | Smooth the marker pose and use thick walls |
| Tracking lost at steep tilts | Limit tilt to about 30 degrees; detailed, high-contrast marker images |
| Low webcam quality | Good room lighting; markers printed around A5 |
| Weather API offline | Fall back to clear weather |

## Deliverables

Source zip, executable zip (including printable markers), a video showing all features, and a short report (student IDs, how to run, features, markers used, external data, asset credits).

# Plan

Here's the plan.

## Where the project is now
- Vuforia 11.4.4 is installed. The only scenes are the Vuforia sample and the default template scene, and there are no game scripts yet.
- Player Settings is set to "Input System package only". My scripts will use the new Input System, and the UI EventSystem will need an `InputSystemUIInputModule`.

## Biggest risk: the Windows build
Vuforia doesn't support normal Windows desktop (.exe) builds. On Windows it only targets 64-bit UWP ([supported versions](https://developer.vuforia.com/library/platform-support/supported-versions), [Vuforia forum](https://developer.vuforia.com/forum/unity-development/does-vuforia-support-standalone-ar-unity-app-mac-or-windows)). That affects the deliverable:
- The "executable zip" will be a UWP package. It has to be installed by sideloading with a certificate, or run through Visual Studio.
- The build needs the WebCam and InternetClient capabilities turned on.
- Vuforia lists Unity 6 LTS as supported, so Unity 6.6 may not be officially supported.

So we should get a UWP build of an empty AR scene running with the webcam in the first days, not the last week.

## Phases
**Phase 0: setup and build check (11–13 Oct)**
- **You:** ARCamera and licence key; three image targets (maze, red key, blue key) printed at A5; Product Name; the empty UWP build test.
- **Me:** the `Assets/Scripts/` structure, `Assets/CREDITS.md`, a scene setup checklist, and a list of sample content that's safe to delete.
**DONE**

**Phase 1: tilt and ball (14–18 Oct).** This is the core of the game; if it doesn't feel good, nothing else matters.
- `MazeTracker` reads tracking state from `OnTargetStatusChanged` and smooths the marker pose.
- `MazeGravity` handles Calibrate, sets gravity from the tilt relative to the calibrated pose, limits it to 30°, freezes the ball while tracking is lost, and provides the keyboard tilt.
- `MazeBuilder` builds the maze, and `MazeMirror` copies positions from the hidden physics copy to the visible maze.
- **Done when:** tilting the printed marker rolls the ball the right way, the walls hold it, and it freezes when the marker leaves view.
**DONE**

**Phase 2: gameplay (19–24 Oct)**
- `KeyGate`: measures key distance in the maze's own coordinates; gates slide open and closed.
- `CheckpointManager`: checkpoints, reset after falling into a hole, undo stack.
- `GameManager`: game states, timer, Normal and Time Trial modes, win.
- Goal particles and the three level grids.
- **Done when:** all three levels can be played start to finish using the keys.
**Done**

**Phase 3: weather and UI (25–29 Oct)**
- `WeatherService`: gets Porto weather from Open-Meteo, with a timeout and a fallback to clear weather.
- `WeatherEffects`: rain particles, lower friction, lighting, and night.
  - Unity barely simulates rolling friction, so "lower friction" will mostly mean changing the ball's damping.
- `UIManager`: start menu, level and mode select, HUD (timer, weather, Calibrate, Pause, Undo, rotation slider), plus pause and win screens.
- **Done when:** every feature in CLAUDE.md can be reached from the UI.

**Phase 4: assets and polish (30 Oct – 2 Nov)**
- Replace the plain shapes with Asset Store or Blender models, and record each one in CREDITS.md.
- Tune with the real markers and room lighting, then make the final UWP build.

**Phase 5: deliverables (3–6 Nov)**
- **Me:** a draft report and a source zip that leaves out Library, Temp and similar folders.
- **You:** the video, student IDs and final checks.
- 5–6 Nov is spare time.

## How we'll work
- For each phase I write the scripts plus a short list of Inspector steps. I won't edit `.unity` or `.prefab` files.
- You set things up in the Editor and test with the webcam, since I can't run Unity or see the camera.
- When something breaks, paste me the console errors or describe what the ball does, and I'll fix it.

If you're happy with the decisions above, I'll start on Phase 1. Thanks to the keyboard tilt, those scripts can be written and tested before the markers are printed. I can also put this plan on a page if you want to share it with your teammates.