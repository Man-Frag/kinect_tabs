from pathlib import Path

import cv2

from recording_io import next_recording_path, save_recording
from tracking import CAMERA_INDEX, MODEL_PATH, PoseTracker
from udp_sender import UdpSender
from visualization import draw_packet


def run_live_tracking(
    *,
    camera_index=CAMERA_INDEX,
    model_path=MODEL_PATH,
    udp_host=None,
    udp_port=None,
    recording_path=None,
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
    is_recording = False
    current_recording_path = None
    saved_recordings = []

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

            frame, packet = tracker.process_frame(raw_frame)

            if sender:
                sender.send_packet(packet)

            if is_recording:
                recorded_packets.append(packet)

            overlay_lines = ["MediaPipe body tracking - press Q to quit"]
            if sender:
                overlay_lines.append(f"UDP -> {sender.host}:{sender.port}")
            if recording_target:
                if is_recording and current_recording_path is not None:
                    overlay_lines.append(
                        f"REC ON -> {current_recording_path.name} ({len(recorded_packets)} frames)"
                    )
                else:
                    next_path = next_recording_path(recording_target)
                    overlay_lines.append(f"REC OFF -> press R to start ({next_path.name})")

            draw_packet(frame, packet, header_lines=overlay_lines)
            cv2.imshow(window_title, frame)

            key = cv2.waitKey(1) & 0xFF

            if key == ord("r") and recording_target:
                if is_recording:
                    saved_path = save_recording(current_recording_path, recorded_packets)
                    saved_recordings.append(saved_path)
                    print(f"Saved recording to {saved_path.resolve()} ({len(recorded_packets)} frames)")
                    is_recording = False
                    current_recording_path = None
                    recorded_packets = []
                else:
                    current_recording_path = next_recording_path(recording_target)
                    recorded_packets = []
                    is_recording = True
                    print(f"Recording started: {current_recording_path.resolve()}")

            if key == ord("q"):
                break
    finally:
        capture.release()
        tracker.close()

        if sender:
            sender.close()

        cv2.destroyAllWindows()

    if is_recording and current_recording_path is not None:
        saved_path = save_recording(current_recording_path, recorded_packets)
        saved_recordings.append(saved_path)
        print(f"Saved recording to {saved_path.resolve()} ({len(recorded_packets)} frames)")

    return saved_recordings