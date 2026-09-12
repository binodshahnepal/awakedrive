"""Perceptual driver-fatigue metrics: EAR, PERCLOS, MAR, and 3D head pose.

Thresholds mirror Dms.Shared.Contracts.Devices.DeviceConfig on the backend so
the edge engine and the API agree on what counts as an incident.
"""
from __future__ import annotations

from collections import deque
from dataclasses import dataclass
from typing import Deque, Sequence

import numpy as np

# --- Default thresholds (kept in sync with backend DeviceConfig defaults) ---
EAR_THRESHOLD = 0.20
EAR_DURATION_SECONDS = 1.5
PERCLOS_WINDOW_SECONDS = 60.0
PERCLOS_THRESHOLD = 0.15
MAR_THRESHOLD = 0.60
MAR_DURATION_SECONDS = 2.5
YAW_THRESHOLD_DEGREES = 25.0
YAW_DURATION_SECONDS = 2.0


def eye_aspect_ratio(eye_landmarks: Sequence[tuple[float, float]]) -> float:
    """Computes EAR from 6 (x, y) eyelid landmarks, ordered as in the
    classic Soukupova & Cech formulation:
    p1..p6 where p2-p6 and p3-p5 are vertical pairs, p1-p4 is horizontal.
    """
    p = np.asarray(eye_landmarks, dtype=float)
    vertical_1 = np.linalg.norm(p[1] - p[5])
    vertical_2 = np.linalg.norm(p[2] - p[4])
    horizontal = np.linalg.norm(p[0] - p[3])
    if horizontal == 0:
        return 0.0
    return (vertical_1 + vertical_2) / (2.0 * horizontal)


def mouth_aspect_ratio(mouth_landmarks: Sequence[tuple[float, float]]) -> float:
    """Computes MAR from 6 (x, y) lip landmarks (outer + inner), same
    vertical/horizontal ratio pattern as EAR.
    """
    p = np.asarray(mouth_landmarks, dtype=float)
    vertical_1 = np.linalg.norm(p[1] - p[5])
    vertical_2 = np.linalg.norm(p[2] - p[4])
    horizontal = np.linalg.norm(p[0] - p[3])
    if horizontal == 0:
        return 0.0
    return (vertical_1 + vertical_2) / (2.0 * horizontal)


@dataclass
class PerclosTracker:
    """Sliding-window PERCLOS: % of time eyes are >= 80% closed."""

    window_seconds: float = PERCLOS_WINDOW_SECONDS
    closed_eye_ear_threshold: float = EAR_THRESHOLD

    def __post_init__(self) -> None:
        self._samples: Deque[tuple[float, bool]] = deque()  # (timestamp, is_closed)

    def update(self, timestamp_seconds: float, ear: float) -> float:
        is_closed = ear < self.closed_eye_ear_threshold
        self._samples.append((timestamp_seconds, is_closed))
        cutoff = timestamp_seconds - self.window_seconds
        while self._samples and self._samples[0][0] < cutoff:
            self._samples.popleft()
        if not self._samples:
            return 0.0
        closed_count = sum(1 for _, closed in self._samples if closed)
        return closed_count / len(self._samples)


@dataclass
class HeadPose:
    pitch_degrees: float
    yaw_degrees: float
    roll_degrees: float


def solve_head_pose(
    image_points: Sequence[tuple[float, float]],
    model_points: Sequence[tuple[float, float, float]],
    camera_matrix: np.ndarray,
    dist_coeffs: np.ndarray | None = None,
) -> HeadPose:
    """Solves 3D head pose (pitch/yaw/roll) via OpenCV's solvePnP.

    Kept as a thin wrapper so the calling code (and its unit tests) don't
    depend on cv2 import order; import is local to avoid a hard dependency
    when this module is used for pure-Python metric math (e.g. in tests).
    """
    import cv2

    if dist_coeffs is None:
        dist_coeffs = np.zeros((4, 1))

    success, rotation_vector, _translation_vector = cv2.solvePnP(
        np.asarray(model_points, dtype=float),
        np.asarray(image_points, dtype=float),
        camera_matrix,
        dist_coeffs,
    )
    if not success:
        raise RuntimeError("solvePnP failed to converge")

    rotation_matrix, _ = cv2.Rodrigues(rotation_vector)
    sy = np.sqrt(rotation_matrix[0, 0] ** 2 + rotation_matrix[1, 0] ** 2)
    singular = sy < 1e-6

    if not singular:
        pitch = np.arctan2(rotation_matrix[2, 1], rotation_matrix[2, 2])
        yaw = np.arctan2(-rotation_matrix[2, 0], sy)
        roll = np.arctan2(rotation_matrix[1, 0], rotation_matrix[0, 0])
    else:
        pitch = np.arctan2(-rotation_matrix[1, 2], rotation_matrix[1, 1])
        yaw = np.arctan2(-rotation_matrix[2, 0], sy)
        roll = 0.0

    return HeadPose(
        pitch_degrees=float(np.degrees(pitch)),
        yaw_degrees=float(np.degrees(yaw)),
        roll_degrees=float(np.degrees(roll)),
    )
