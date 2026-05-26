# Kinect Tabs Tracker (Python)

This folder contains the Python tracking pipeline.

## Entry scripts

There are three runnable scripts:

- `track_people.py`: live camera tracking, on-screen visualization, and UDP packet sending.
- `record_tracking.py`: live camera tracking, on-screen visualization, and JSON recording toggled with `r`.
- `playback_recording.py`: playback of a previously recorded JSON session while re-sending UDP packets frame-by-frame.

By default, all scripts show a non-mirrored view. Pass `--mirror` if you want the preview or playback view mirrored. JSON recordings and UDP packets always stay unmirrored.

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

Install:

```bash
pip install opencv-python mediapipe
```

On first run, the default model file is downloaded automatically to this folder if it is missing.

## Usage

Run commands from this folder:

```bash
cd media-python
```

Run live tracking and UDP sending:

```bash
python track_people.py
```

Optional flags:

```bash
python track_people.py --camera 0 --model pose_landmarker_full.task --udp-host 127.0.0.1 --udp-port 5052
python track_people.py --mirror
```

Record a session to JSON while visualizing:

```bash
python record_tracking.py recording.json
```

Press `r` to start recording and press `r` again to stop. The overlay shows whether recording is on or off. If `recording.json` already exists, the next saved file becomes `recording_2.json`, then `recording_3.json`, and so on.

Optional flags:

```bash
python record_tracking.py recording.json --camera 0 --model pose_landmarker_full.task
python record_tracking.py recording.json --mirror
```

Play back a recording:

```bash
python playback_recording.py recording.json
```

Optional flags:

```bash
python playback_recording.py recording.json --speed 2.0 --loop
python playback_recording.py recording.json --mirror
python playback_recording.py recording.json --udp-host 127.0.0.1 --udp-port 5052
```

## Notes

- Press `q` in any OpenCV window to stop live tracking or playback.
- In `record_tracking.py`, press `r` to start or stop a recording segment.
- The live tracking script remains the original UDP-sending entry point.
