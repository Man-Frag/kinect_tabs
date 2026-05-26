import json
from datetime import datetime, timezone
from pathlib import Path


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


def load_recording(path):
    recording_path = Path(path)
    payload = json.loads(recording_path.read_text(encoding="utf-8"))

    if "frames" not in payload or not isinstance(payload["frames"], list):
        raise ValueError(f"Invalid recording file: {recording_path}")

    return payload