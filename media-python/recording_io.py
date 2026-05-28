import json
from datetime import datetime, timezone
from pathlib import Path


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


def _normalize_vector(dx, dy, dz):
    length = (dx * dx + dy * dy + dz * dz) ** 0.5

    if length < 1e-6:
        return None

    return {
        "x": float(dx / length),
        "y": float(dy / length),
        "z": float(dz / length),
    }


def _joint_vector(joints, start_name, end_name):
    start = joints.get(start_name)
    end = joints.get(end_name)

    if not isinstance(start, dict) or not isinstance(end, dict):
        return None

    direction = _normalize_vector(
        float(end.get("x", 0.0)) - float(start.get("x", 0.0)),
        float(end.get("y", 0.0)) - float(start.get("y", 0.0)),
        float(end.get("z", 0.0)) - float(start.get("z", 0.0)),
    )

    if direction is None:
        return None

    start_visibility = float(start.get("visibility", 0.0))
    end_visibility = float(end.get("visibility", 0.0))
    direction["confidence"] = min(start_visibility, end_visibility)
    return direction


def _build_bones_from_joints(joints):
    bones = {}

    for bone_name, (start_name, end_name) in BONE_ENDPOINTS.items():
        vector = _joint_vector(joints, start_name, end_name)
        if vector is not None:
            bones[bone_name] = vector

    left_shoulder = joints.get("left_shoulder")
    right_shoulder = joints.get("right_shoulder")
    nose = joints.get("nose")

    if isinstance(left_shoulder, dict) and isinstance(right_shoulder, dict) and isinstance(nose, dict):
        shoulder_center = {
            "x": (float(left_shoulder.get("x", 0.0)) + float(right_shoulder.get("x", 0.0))) * 0.5,
            "y": (float(left_shoulder.get("y", 0.0)) + float(right_shoulder.get("y", 0.0))) * 0.5,
            "z": (float(left_shoulder.get("z", 0.0)) + float(right_shoulder.get("z", 0.0))) * 0.5,
            "visibility": min(
                float(left_shoulder.get("visibility", 0.0)),
                float(right_shoulder.get("visibility", 0.0)),
            ),
        }

        head_vector = _joint_vector({"start": shoulder_center, "end": nose}, "start", "end")
        if head_vector is not None:
            bones["head"] = head_vector

    return bones


def _ensure_bones(payload):
    frames = payload.get("frames", [])

    for frame in frames:
        players = frame.get("players", [])

        for player in players:
            if not isinstance(player, dict):
                continue

            bones = player.get("bones")

            if isinstance(bones, dict) and bones:
                continue

            joints = player.get("joints")

            if isinstance(joints, dict):
                player["bones"] = _build_bones_from_joints(joints)


def save_recording(path, packets):
    recording_path = Path(path)
    recording_path.parent.mkdir(parents=True, exist_ok=True)

    payload = {
        "version": 1,
        "created_at": datetime.now(timezone.utc).isoformat(),
        "frame_count": len(packets),
        "frames": packets,
    }

    recording_path.write_text(json.dumps(payload, indent=2), encoding="utf-8")
    return recording_path


def next_recording_path(path):
    recording_path = Path(path)

    if not recording_path.exists():
        return recording_path

    suffix = recording_path.suffix
    stem = recording_path.stem
    parent = recording_path.parent
    index = 2

    while True:
        candidate = parent / f"{stem}_{index}{suffix}"
        if not candidate.exists():
            return candidate
        index += 1


def load_recording(path):
    recording_path = Path(path)
    payload = json.loads(recording_path.read_text(encoding="utf-8"))

    if "frames" not in payload or not isinstance(payload["frames"], list):
        raise ValueError(f"Invalid recording file: {recording_path}")

    _ensure_bones(payload)

    return payload