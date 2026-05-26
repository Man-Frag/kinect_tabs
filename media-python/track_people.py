import argparse

from live_tracking import run_live_tracking
from tracking import CAMERA_INDEX, MODEL_PATH
from udp_sender import UDP_HOST, UDP_PORT

def parse_args():
    parser = argparse.ArgumentParser(description="Track people and send pose packets over UDP.")
    parser.add_argument("--camera", type=int, default=CAMERA_INDEX, help="Camera index to open.")
    parser.add_argument("--model", default=MODEL_PATH, help="Path to the MediaPipe pose model.")
    parser.add_argument("--udp-host", default=UDP_HOST, help="UDP host to send packets to.")
    parser.add_argument("--udp-port", type=int, default=UDP_PORT, help="UDP port to send packets to.")
    parser.add_argument("--mirror", action="store_true", help="Mirror the live preview only. UDP data remains unmirrored.")
    return parser.parse_args()


def main():
    args = parse_args()
    run_live_tracking(
        camera_index=args.camera,
        model_path=args.model,
        udp_host=args.udp_host,
        udp_port=args.udp_port,
        mirror=args.mirror,
        window_title="MediaPipe Tracker",
    )


if __name__ == "__main__":
    main()