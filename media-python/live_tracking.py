from pathlib import Path

import cv2

from recording_io import next_recording_path, save_recording
from tracking import CAMERA_INDEX, MODEL_PATH, PoseTracker
from udp_sender import UdpSender
from visualization import draw_packet


# cv2.waitKey returns -1 & 0xFF = 255 when no key is pressed.
_NO_KEY = 0xFF

_SPECIAL_KEY_NAMES = {
    8: "backspace",
    9: "tab",
    13: "return",
    27: "escape",
    32: "space",
}


def _key_name(keycode):
    """Convert a cv2.waitKey keycode to a readable string."""
    if keycode in _SPECIAL_KEY_NAMES:
        return _SPECIAL_KEY_NAMES[keycode]
    if 33 <= keycode <= 126:
        return chr(keycode)
    return f"key{keycode}"


def run_live_tracking(
    *,
    camera_index=CAMERA_INDEX,
    model_path=MODEL_PATH,
    udp_host=None,
    udp_port=None,
    recording_path=None,
    mirror=False,
    window_title="MediaPipe Tracker",
):
    tracker = PoseTracker(model_path=model_path)
    capture = cv2.VideoCapture(camera_index)

    if not capture.isOpened():
        tracker.close()
        raise RuntimeError("Could not open camera")

    sender = UdpSender(udp_host, udp_port) if udp_host and udp_port is not None else None
    recording_target = Path(recording_path) if recording_path else None
    recorded_packets = []
    recorded_events = []
    is_recording = False
    current_recording_path = None
    saved_recordings = []
    # Debounce: track the keycode seen on the previous frame so that holding
    # a key down does not fire repeated events.  Only the frame where the key
    # first appears (transition from _NO_KEY → key) is treated as a press.
    _prev_key = _NO_KEY

    print("Tracking started.")
    if sender:
        print(f"Sending UDP to {sender.host}:{sender.port}")
    if recording_target:
        print("Press R to start or stop recording.")

    try:
        while True:
            ok, raw_frame = capture.read()

            if not ok:
                print("Could not read frame")
                break

            frame, packet, preview_packet = tracker.process_frame(raw_frame, mirror_preview=mirror)

            if sender:
                sender.send_packet(packet)

            if is_recording:
                recorded_packets.append(packet)

            overlay_lines = ["MediaPipe body tracking - press Q to quit"]
            if sender:
                overlay_lines.append(f"UDP -> {sender.host}:{sender.port}")
            overlay_lines.append(f"Preview -> {'mirrored' if mirror else 'not mirrored'}")
            if recording_target:
                if is_recording and current_recording_path is not None:
                    overlay_lines.append(
                        f"REC ON -> {current_recording_path.name} "
                        f"({len(recorded_packets)} frames, {len(recorded_events)} events)"
                    )
                else:
                    next_path = next_recording_path(recording_target)
                    overlay_lines.append(f"REC OFF -> press R to start ({next_path.name})")

            draw_packet(frame, preview_packet, header_lines=overlay_lines)
            cv2.imshow(window_title, frame)

            raw_key = cv2.waitKey(1) & 0xFF

            # Only treat a key as a fresh press on the frame it first appears.
            # If the same code is still set next frame (OS key-repeat) we skip
            # it so one physical press produces exactly one event.
            key = raw_key if raw_key != _prev_key else _NO_KEY
            _prev_key = raw_key

            if key == ord("q"):
                break

            if key == ord("r") and recording_target:
                if is_recording:
                    # Stamp the stop keystroke before saving so the receiver
                    # knows the recording ended intentionally.
                    recorded_events.append({
                        "type": "keydown",
                        "key": "r",
                        "timestamp_ms": packet["timestamp_ms"],
                    })
                    saved_path = save_recording(
                        current_recording_path, recorded_packets, recorded_events
                    )
                    saved_recordings.append(saved_path)
                    print(
                        f"Saved recording to {saved_path.resolve()} "
                        f"({len(recorded_packets)} frames, {len(recorded_events)} events)"
                    )
                    is_recording = False
                    current_recording_path = None
                    recorded_packets = []
                    recorded_events = []
                else:
                    current_recording_path = next_recording_path(recording_target)
                    recorded_packets = []
                    recorded_events = []
                    is_recording = True
                    print(f"Recording started: {current_recording_path.resolve()}")
                continue

            # Any non-null key other than Q and recording-R:
            # forward over UDP (so Unity gets live keystrokes too) and store
            # in the recording if one is active.
            if key != _NO_KEY:
                key_str = _key_name(key)

                if sender:
                    sender.send_key_event(key_str, timestamp_ms=packet["timestamp_ms"])

                if is_recording:
                    recorded_events.append({
                        "type": "keydown",
                        "key": key_str,
                        "timestamp_ms": packet["timestamp_ms"],
                    })
                    print(f"[REC] key={key_str!r}  t={packet['timestamp_ms']}ms")

    finally:
        capture.release()
        tracker.close()

        if sender:
            sender.close()

        cv2.destroyAllWindows()

    if is_recording and current_recording_path is not None:
        saved_path = save_recording(current_recording_path, recorded_packets, recorded_events)
        saved_recordings.append(saved_path)
        print(
            f"Saved recording to {saved_path.resolve()} "
            f"({len(recorded_packets)} frames, {len(recorded_events)} events)"
        )

    return saved_recordings
