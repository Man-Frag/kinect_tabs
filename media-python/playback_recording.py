import argparse

import cv2

from recording_io import load_recording
from tracking import mirror_packet
from visualization import create_canvas, draw_packet


def parse_args():
    parser = argparse.ArgumentParser(description="Playback a JSON body-tracking recording.")
    parser.add_argument("recording", help="Path to the recording JSON file.")
    parser.add_argument("--speed", type=float, default=1.0, help="Playback speed multiplier.")
    parser.add_argument("--loop", action="store_true", help="Loop playback until Q is pressed.")
    parser.add_argument("--mirror", action="store_true", help="Mirror the playback view only. The recording data remains unmirrored.")
    return parser.parse_args()


def playback_frames(frames, speed=1.0, loop=False, mirror=False):
    if not frames:
        raise RuntimeError("Recording does not contain any frames")

    frame_index = 0
    speed = max(speed, 0.01)

    while True:
        packet = frames[frame_index]
        display_packet = mirror_packet(packet) if mirror else packet
        canvas = create_canvas(packet)
        overlay_lines = [
            "Recording playback - press Q to quit",
            f"Frame {frame_index + 1}/{len(frames)}",
            f"Speed x{speed:g}",
            f"Preview {'mirrored' if mirror else 'not mirrored'}",
        ]

        draw_packet(canvas, display_packet, header_lines=overlay_lines)
        is_last_frame = frame_index >= len(frames) - 1

        if is_last_frame:
            delay_ms = 1
        else:
            next_timestamp = frames[frame_index + 1]["timestamp_ms"]
            current_timestamp = packet["timestamp_ms"]
            delta_ms = max(1, next_timestamp - current_timestamp)
            delay_ms = max(1, int(delta_ms / speed))

        cv2.imshow("Tracking Playback", canvas)
        key = cv2.waitKey(delay_ms) & 0xFF

        if key == ord("q"):
            break

        if is_last_frame:
            if not loop:
                break
            frame_index = 0
            continue

        frame_index += 1

    cv2.destroyAllWindows()


def main():
    args = parse_args()
    recording = load_recording(args.recording)
    playback_frames(recording["frames"], speed=args.speed, loop=args.loop, mirror=args.mirror)


if __name__ == "__main__":
    main()