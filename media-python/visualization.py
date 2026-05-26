import cv2
import numpy as np


PLAYER_COLORS = [
    (255, 180, 40),
    (80, 120, 255),
]

SKELETON_CONNECTIONS = [
    ("left_shoulder", "right_shoulder"),
    ("left_shoulder", "left_hip"),
    ("right_shoulder", "right_hip"),
    ("left_hip", "right_hip"),
    ("left_shoulder", "left_elbow"),
    ("left_elbow", "left_wrist"),
    ("right_shoulder", "right_elbow"),
    ("right_elbow", "right_wrist"),
    ("left_hip", "left_knee"),
    ("left_knee", "left_ankle"),
    ("right_hip", "right_knee"),
    ("right_knee", "right_ankle"),
]

SPECIAL_POINTS = [
    ("left_wrist", (0, 255, 255), "L HAND", 13),
    ("right_wrist", (0, 255, 255), "R HAND", 13),
    ("left_foot_index", (0, 255, 0), "L FOOT", 13),
    ("right_foot_index", (0, 255, 0), "R FOOT", 13),
]


def create_canvas(packet, background_color=(24, 24, 24)):
    width = max(1, int(packet["frame"].get("width", 1280)))
    height = max(1, int(packet["frame"].get("height", 720)))
    return np.full((height, width, 3), background_color, dtype=np.uint8)


def _joint_point(joint):
    return int(joint["x"]), int(joint["y"])


def _draw_point(frame, joint, color, label=None, radius=8):
    x, y = _joint_point(joint)
    cv2.circle(frame, (x, y), radius, color, -1)

    if label:
        cv2.putText(
            frame,
            label,
            (x + 8, y - 8),
            cv2.FONT_HERSHEY_SIMPLEX,
            0.45,
            color,
            2,
        )


def _draw_line(frame, first_joint, second_joint, color, thickness=4):
    ax, ay = _joint_point(first_joint)
    bx, by = _joint_point(second_joint)
    cv2.line(frame, (ax, ay), (bx, by), color, thickness)


def draw_player(frame, player, color):
    joints = player["joints"]

    for first_name, second_name in SKELETON_CONNECTIONS:
        _draw_line(frame, joints[first_name], joints[second_name], color)

    _draw_point(frame, joints["nose"], color, f"Player {player['id']}")

    for joint_name, special_color, label, radius in SPECIAL_POINTS:
        _draw_point(frame, joints[joint_name], special_color, label, radius)


def draw_packet(frame, packet, header_lines=None):
    for index, player in enumerate(packet.get("players", [])[:2]):
        color = PLAYER_COLORS[index % len(PLAYER_COLORS)]
        draw_player(frame, player, color)

    y = 40
    for line in header_lines or []:
        cv2.putText(
            frame,
            line,
            (20, y),
            cv2.FONT_HERSHEY_SIMPLEX,
            0.8,
            (255, 255, 255),
            2,
        )
        y += 34

    return frame