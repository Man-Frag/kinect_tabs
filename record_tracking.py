import argparse

from live_tracking import run_live_tracking
from tracking import CAMERA_INDEX, MODEL_PATH


def parse_args():
    parser = argparse.ArgumentParser(description="Track people, visualize, and record packets to JSON.")
    parser.add_argument("output", nargs="?", default="recording.json", help="Path to the output JSON file.")
    parser.add_argument("--camera", type=int, default=CAMERA_INDEX, help="Camera index to open.")
    parser.add_argument("--model", default=MODEL_PATH, help="Path to the MediaPipe pose model.")
    return parser.parse_args()


def main():
    args = parse_args()
    run_live_tracking(
        camera_index=args.camera,
        model_path=args.model,
        recording_path=args.output,
        window_title="MediaPipe Tracker Recorder",
    )


if __name__ == "__main__":
    main()