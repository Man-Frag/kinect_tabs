# pipe-test (Unity UDP Mini Rig)

This is a second Unity project focused on a lightweight MediaPipe-style visualization rig.

It receives the same UDP packets as the main project and renders a small rig made of joints (spheres) and bones (capsules/cylinders), so the result closely matches the Python MediaPipe preview.

## What Is Included

- `Assets/Scripts/UdpPoseReceiver.cs`
  - UDP listener using the same JSON schema (`frame`, `players`, `joints`, optional `bones`).
- `Assets/Scripts/MiniRigVisualizer.cs`
  - Builds and animates a compact rig from MediaPipe joints.
- `Assets/Scripts/PipeTestBootstrap.cs`
  - Auto-creates a receiver, rig, camera, and light when Play starts.
- `Assets/Editor/PipeTestSceneSetup.cs`
  - Adds `Tools > Pipe Test > Create Mini Rig Scene Objects` to create visible model objects in the current scene.
- `Packages/manifest.json` and `ProjectSettings/ProjectVersion.txt`
  - Minimal Unity project metadata so the folder can be opened directly.

## Run

1. Open `pipe-test` in Unity Hub (Editor `6000.4.8f1` recommended).
2. Open or create any scene (empty scene is fine).
3. In Unity menu, run `Tools > Pipe Test > Create Mini Rig Scene Objects`.
4. Press Play.
5. Start Python sender from the workspace root:

```bash
python media-python/track_people.py
```

Or replay a recording:

```bash
python media-python/playback_recording.py right_test.json
```

## Notes

- Default UDP port is `5052`.
- The mini rig uses body-size normalization, so people at different distances stay proportionally stable.
- If left/right appears inverted, set `mirrorX` on `MiniRigVisualizer`.

## 4-Pose Calibration (Play Mode)

`MiniRigVisualizer` includes a guided 4-step calibration:

1. Stand straight with arms and legs closed.
2. T-pose with legs closed.
3. T-pose with legs open (A-stance lower body).
4. Keep legs open and raise both arms all the way up.

Keys (default):

- `C`: start calibration
- `Space` or `Enter`: capture current pose step
- `R`: reset and recalibrate

By default, capture is non-blocking (`requirePoseValidation = false`) so each key press advances steps even if your pose is imperfect. Turn strict validation on in the inspector if you want enforced pose checks.

During play, an on-screen box shows the current required calibration pose and current gain values after completion.
