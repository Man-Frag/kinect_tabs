# Kinect Tabs Tracker

This project tracks up to two people with MediaPipe pose estimation, visualizes the detected skeletons, sends pose data over UDP, records sessions to JSON, and plays recordings back.

## Entry scripts

There are three runnable scripts:

- `track_people.py`: live camera tracking, on-screen visualization, and UDP packet sending.
- `record_tracking.py`: live camera tracking, on-screen visualization, and JSON recording toggled with `r`.
- `playback_recording.py`: playback of a previously recorded JSON session.

## Helper modules

- `tracking.py`: camera frame processing and MediaPipe pose packet creation.
- `udp_sender.py`: UDP transport.
- `visualization.py`: skeleton and overlay drawing.
- `live_tracking.py`: shared live camera loop used by the live and record scripts.
- `recording_io.py`: JSON save/load helpers.

## Requirements

- Python 3.10+
- OpenCV (`cv2`)
- MediaPipe
- Internet access on first run to auto-download the default MediaPipe model, or a local `pose_landmarker_full.task` passed in with `--model`

Example install:

```bash
pip install opencv-python mediapipe
```

On first run, the default model file is downloaded automatically to the project directory if it is missing.

## Usage

Run live tracking and UDP sending:

```bash
python track_people.py
```

Optional flags:

```bash
python track_people.py --camera 0 --model pose_landmarker_full.task --udp-host 127.0.0.1 --udp-port 5052
```

Record a session to JSON while visualizing:

```bash
python record_tracking.py recording.json
```

Press `r` to start recording and press `r` again to stop. The overlay shows whether recording is on or off. If `recording.json` already exists, the next saved file becomes `recording_2.json`, then `recording_3.json`, and so on.

Optional flags:

```bash
python record_tracking.py recording.json --camera 0 --model pose_landmarker_full.task
```

Play back a recording:

```bash
python playback_recording.py recording.json
```

Optional flags:

```bash
python playback_recording.py recording.json --speed 2.0 --loop
```

## Output format

Recordings are stored as JSON with:

- top-level metadata (`version`, `created_at`, `frame_count`)
- a `frames` array
- each frame containing `timestamp_ms`, frame size, and up to two tracked players with joint coordinates

## Notes

- Press `q` in any OpenCV window to stop live tracking or playback.
- In `record_tracking.py`, press `r` to start or stop a recording segment.
- The live tracking script remains the original UDP-sending entry point.