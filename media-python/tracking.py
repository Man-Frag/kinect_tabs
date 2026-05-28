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

BONE_ENDPOINTS = {
    "left_upper_arm": ("left_shoulder", "left_elbow"),
    "left_lower_arm": ("left_elbow", "left_wrist"),
    "right_upper_arm": ("right_shoulder", "right_elbow"),
    "right_lower_arm": ("right_elbow", "right_wrist"),
    "left_upper_leg": ("left_hip", "left_knee"),
    "left_lower_leg": ("left_knee", "left_ankle"),
    "right_upper_leg": ("right_hip", "right_knee"),
    "right_lower_leg": ("right_knee", "right_ankle"),
}


def landmark_to_dict(landmark, width, height):
    return {
        "x": float(landmark.x * width),
        "y": float(landmark.y * height),
        "z": float(landmark.z),
        "visibility": float(getattr(landmark, "visibility", 0.0)),
    }


def normalize_vector(dx, dy, dz):
    length = (dx * dx + dy * dy + dz * dz) ** 0.5

    if length < 1e-6:
        return None

    return {
        "x": float(dx / length),
        "y": float(dy / length),
        "z": float(dz / length),
    }


def vector_between_points(start_xyz, end_xyz, start_visibility=0.0, end_visibility=0.0):
    vector = normalize_vector(
        end_xyz[0] - start_xyz[0],
        end_xyz[1] - start_xyz[1],
        end_xyz[2] - start_xyz[2],
    )

    if vector is None:
        return None

    confidence = min(
        float(start_visibility),
        float(end_visibility),
    )

    vector["confidence"] = confidence
    return vector


def build_bones(landmarks):
    joints = {
        name: landmarks[index]
        for name, index in JOINT_INDICES.items()
    }

    bones = {}

    for bone_name, (start_name, end_name) in BONE_ENDPOINTS.items():
        start = joints[start_name]
        end = joints[end_name]
        vector = vector_between_points(
            (start.x, start.y, start.z),
            (end.x, end.y, end.z),
            getattr(start, "visibility", 0.0),
            getattr(end, "visibility", 0.0),
        )

        if vector is not None:
            bones[bone_name] = vector

    left_shoulder = joints["left_shoulder"]
    right_shoulder = joints["right_shoulder"]
    nose = joints["nose"]

    shoulder_center = (
        (left_shoulder.x + right_shoulder.x) * 0.5,
        (left_shoulder.y + right_shoulder.y) * 0.5,
        (left_shoulder.z + right_shoulder.z) * 0.5,
    )
    shoulder_visibility = min(
        float(getattr(left_shoulder, "visibility", 0.0)),
        float(getattr(right_shoulder, "visibility", 0.0)),
    )

    head_vector = vector_between_points(
        shoulder_center,
        (nose.x, nose.y, nose.z),
        shoulder_visibility,
        getattr(nose, "visibility", 0.0),
    )

    if head_vector is not None:
        bones["head"] = head_vector

    return bones


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
        joints = {
            joint_name: landmark_to_dict(landmarks[joint_index], width, height)
            for joint_name, joint_index in JOINT_INDICES.items()
        }

        packet["players"].append(
            {
                "id": index + 1,
                "joints": joints,
                "bones": build_bones(landmarks),
            }
        )

    return packet


def mirror_packet(packet):
    mirrored_packet = {
        "timestamp_ms": packet["timestamp_ms"],
        "frame": dict(packet["frame"]),
        "players": [],
    }

    frame_width = mirrored_packet["frame"]["width"]

    for player in packet.get("players", []):
        mirrored_joints = {}
        for joint_name, joint in player["joints"].items():
            mirrored_joint = dict(joint)
            mirrored_joint["x"] = float(frame_width - joint["x"])
            mirrored_joints[joint_name] = mirrored_joint

        mirrored_bones = {}
        for bone_name, bone in player.get("bones", {}).items():
            mirrored_bone = dict(bone)
            mirrored_bone["x"] = float(-bone.get("x", 0.0))
            mirrored_bones[bone_name] = mirrored_bone

        mirrored_packet["players"].append(
            {
                "id": player["id"],
                "joints": mirrored_joints,
                "bones": mirrored_bones,
            }
        )

    return mirrored_packet


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

    def process_frame(self, frame, timestamp_ms=None, mirror_preview=False):
        height, width = frame.shape[:2]

        rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        mp_image = mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb_frame)

        video_timestamp_ms = int(time.time() * 1000) if timestamp_ms is None else timestamp_ms
        result = self._landmarker.detect_for_video(mp_image, video_timestamp_ms)
        packet = build_packet(result, width, height, video_timestamp_ms)
        preview_frame = cv2.flip(frame, 1) if mirror_preview else frame.copy()
        preview_packet = mirror_packet(packet) if mirror_preview else packet

        return preview_frame, packet, preview_packet

    def close(self):
        self._landmarker.close()