import time
from pathlib import Path
from urllib.request import urlretrieve

import cv2
import mediapipe as mp
from mediapipe.tasks import python
from mediapipe.tasks.python import vision


MODEL_PATH = "pose_landmarker_full.task"
CAMERA_INDEX = 0
DEFAULT_MODEL_URL = (
    "https://storage.googleapis.com/mediapipe-models/pose_landmarker/"
    "pose_landmarker_full/float16/latest/pose_landmarker_full.task"
)

JOINT_INDICES = {
    "nose": 0,
    "left_shoulder": 11,
    "right_shoulder": 12,
    "left_elbow": 13,
    "right_elbow": 14,
    "left_wrist": 15,
    "right_wrist": 16,
    "left_hip": 23,
    "right_hip": 24,
    "left_knee": 25,
    "right_knee": 26,
    "left_ankle": 27,
    "right_ankle": 28,
    "left_foot_index": 31,
    "right_foot_index": 32,
}


def landmark_to_dict(landmark, width, height):
    return {
        "x": float(landmark.x * width),
        "y": float(landmark.y * height),
        "z": float(landmark.z),
        "visibility": float(getattr(landmark, "visibility", 0.0)),
    }


def build_packet(result, width, height, timestamp_ms):
    packet = {
        "timestamp_ms": timestamp_ms,
        "frame": {
            "width": width,
            "height": height,
        },
        "players": [],
    }

    if not result.pose_landmarks:
        return packet

    for index, landmarks in enumerate(result.pose_landmarks[:2]):
        packet["players"].append(
            {
                "id": index + 1,
                "joints": {
                    joint_name: landmark_to_dict(landmarks[joint_index], width, height)
                    for joint_name, joint_index in JOINT_INDICES.items()
                },
            }
        )

    return packet


class PoseTracker:
    def __init__(self, model_path=MODEL_PATH):
        self.model_path = Path(model_path)

        if not self.model_path.exists():
            if self.model_path.name == MODEL_PATH:
                self._download_default_model()
            else:
                raise FileNotFoundError(f"Model file not found: {self.model_path.resolve()}")

        base_options = python.BaseOptions(model_asset_path=str(self.model_path))

        options = vision.PoseLandmarkerOptions(
            base_options=base_options,
            running_mode=vision.RunningMode.VIDEO,
            num_poses=2,
            min_pose_detection_confidence=0.5,
            min_pose_presence_confidence=0.5,
            min_tracking_confidence=0.5,
        )

        self._landmarker = vision.PoseLandmarker.create_from_options(options)

    def _download_default_model(self):
        self.model_path.parent.mkdir(parents=True, exist_ok=True)
        print(f"Downloading pose model to {self.model_path.resolve()}...")

        try:
            urlretrieve(DEFAULT_MODEL_URL, self.model_path)
        except Exception as exc:
            raise FileNotFoundError(
                "Default pose model is missing and could not be downloaded automatically. "
                f"Expected path: {self.model_path.resolve()}"
            ) from exc

    def process_frame(self, frame, timestamp_ms=None):
        flipped_frame = cv2.flip(frame, 1)
        height, width = flipped_frame.shape[:2]

        rgb_frame = cv2.cvtColor(flipped_frame, cv2.COLOR_BGR2RGB)
        mp_image = mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb_frame)

        video_timestamp_ms = int(time.time() * 1000) if timestamp_ms is None else timestamp_ms
        result = self._landmarker.detect_for_video(mp_image, video_timestamp_ms)
        packet = build_packet(result, width, height, video_timestamp_ms)

        return flipped_frame, packet

    def close(self):
        self._landmarker.close()