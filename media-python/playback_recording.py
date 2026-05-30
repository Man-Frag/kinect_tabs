import argparse

import cv2

from recording_io import load_recording
from tracking import mirror_packet
from udp_sender import UDP_HOST, UDP_PORT, UdpSender
from visualization import create_canvas, draw_packet


def parse_args():
    parser = argparse.ArgumentParser(description="Playback a JSON body-tracking recording.")
    parser.add_argument("recording", help="Path to the recording JSON file.")
    parser.add_argument("--speed", type=float, default=1.0, help="Playback speed multiplier.")
    parser.add_argument("--loop", action="store_true", help="Loop playback until Q is pressed.")
    parser.add_argument("--mirror", action="store_true", help="Mirror the playback view only. The recording data remains unmirrored.")
    parser.add_argument("--udp-host", default=UDP_HOST, help="UDP host to send replay packets to.")
    parser.add_argument("--udp-port", type=int, default=UDP_PORT, help="UDP port to send replay packets to.")
    return parser.parse_args()


def playback_frames(
    frames,
    events=None,
    speed=1.0,
    loop=False,
    mirror=False,
    udp_host=UDP_HOST,
    udp_port=UDP_PORT,
):
    if not frames:
        raise RuntimeError("Recording does not contain any frames")

    # Sort once; events may originate from out-of-order appends.
    sorted_events = sorted(events or [], key=lambda e: e["timestamp_ms"])
    total_events = len(sorted_events)

    sender = UdpSender(host=udp_host, port=udp_port)
    speed = max(speed, 0.01)

    try:
        while True:
            frame_index = 0
            event_index = 0  # reset at the start of every loop pass

            while True:
                packet = frames[frame_index]
                current_ts = packet["timestamp_ms"]

                # Fire every key event whose timestamp has been reached by
                # this frame.  Events are ordered so we advance a pointer
                # rather than scanning the whole list each frame.
                while event_index < total_events:
                    evt = sorted_events[event_index]
                    if evt["timestamp_ms"] > current_ts:
                        break
                    sender.send_key_event(
                        evt["key"],
                        evt.get("event", "keydown"),
                        evt["timestamp_ms"],
                    )
                    event_index += 1

                sender.send_packet(packet)

                display_packet = mirror_packet(packet) if mirror else packet
                canvas = create_canvas(packet)
                overlay_lines = [
                    "Recording playback - press Q to quit",
                    f"Frame {frame_index + 1}/{len(frames)}",
                    f"Speed x{speed:g}",
                    f"Preview {'mirrored' if mirror else 'not mirrored'}",
                    f"UDP -> {udp_host}:{udp_port}",
                    f"Key events: {event_index}/{total_events} sent",
                ]

                draw_packet(canvas, display_packet, header_lines=overlay_lines)
                is_last_frame = frame_index >= len(frames) - 1

                if is_last_frame:
                    # Flush any events that sit beyond the last frame timestamp.
                    while event_index < total_events:
                        evt = sorted_events[event_index]
                        sender.send_key_event(
                            evt["key"],
                            evt.get("event", "keydown"),
                            evt["timestamp_ms"],
                        )
                        event_index += 1
                    delay_ms = 1
                else:
                    next_timestamp = frames[frame_index + 1]["timestamp_ms"]
                    delta_ms = max(1, next_timestamp - current_ts)
                    delay_ms = max(1, int(delta_ms / speed))

                cv2.imshow("Tracking Playback", canvas)
                key = cv2.waitKey(delay_ms) & 0xFF

                if key == ord("q"):
                    return

                if is_last_frame:
                    if not loop:
                        return
                    break  # restart outer while with reset indices

                frame_index += 1

    finally:
        sender.close()
        cv2.destroyAllWindows()


def main():
    args = parse_args()
    recording = load_recording(args.recording)
    playback_frames(
        recording["frames"],
        events=recording.get("events", []),
        speed=args.speed,
        loop=args.loop,
        mirror=args.mirror,
        udp_host=args.udp_host,
        udp_port=args.udp_port,
    )


if __name__ == "__main__":
    main()
