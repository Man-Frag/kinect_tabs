from pathlib import Path

import cv2

from recording_io import save_recording
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

    print("Tracking started.")
    if sender:
        print(f"Sending UDP to {sender.host}:{sender.port}")
    if recording_target:
        print(f"Recording to {recording_target.resolve()}")

    try:
        while True:
            ok, raw_frame = capture.read()

            if not ok:
                print("Could not read frame")
                break

            frame, packet = tracker.process_frame(raw_frame)

            if sender:
                sender.send_packet(packet)

            if recording_target:
                recorded_packets.append(packet)

            overlay_lines = ["MediaPipe body tracking - press Q to quit"]
            if sender:
                overlay_lines.append(f"UDP -> {sender.host}:{sender.port}")
            if recording_target:
                overlay_lines.append(f"REC -> {recording_target.name} ({len(recorded_packets)} frames)")

            draw_packet(frame, packet, header_lines=overlay_lines)
            cv2.imshow(window_title, frame)

            if cv2.waitKey(1) & 0xFF == ord("q"):
                break
    finally:
        capture.release()
        tracker.close()

        if sender:
            sender.close()

        cv2.destroyAllWindows()

    if recording_target:
        save_recording(recording_target, recorded_packets)

    return recorded_packets