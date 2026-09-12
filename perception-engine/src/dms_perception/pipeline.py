"""Real-time capture -> MediaPipe FaceMesh -> metrics pipeline entry point.

This is a scaffold: run() opens the default camera, tracks EAR/MAR/PERCLOS/
head pose per frame, and prints incident-worthy events to stdout. Swap the
`on_incident` callback for an HTTP POST to the backend's
TelemetryController (POST /api/v1/telemetry/incidents) once auth is wired up.
"""
from __future__ import annotations

import time
from typing import Callable

from .metrics import (
    EAR_DURATION_SECONDS,
    EAR_THRESHOLD,
    MAR_DURATION_SECONDS,
    MAR_THRESHOLD,
    PERCLOS_THRESHOLD,
    PerclosTracker,
    eye_aspect_ratio,
    mouth_aspect_ratio,
)

# MediaPipe FaceMesh landmark indices for a 6-point EAR/MAR estimate.
LEFT_EYE_IDX = [33, 160, 158, 133, 153, 144]
RIGHT_EYE_IDX = [362, 385, 387, 263, 373, 380]
MOUTH_IDX = [61, 39, 0, 291, 181, 17]


def on_incident_default(incident_type: str, payload: dict) -> None:
    print(f"[INCIDENT] {incident_type}: {payload}")


def run(camera_index: int = 0, on_incident: Callable[[str, dict], None] = on_incident_default) -> None:
    import cv2
    import mediapipe as mp

    mp_face_mesh = mp.solutions.face_mesh
    perclos = PerclosTracker()
    eyes_closed_since: float | None = None
    mouth_open_since: float | None = None

    cap = cv2.VideoCapture(camera_index)
    try:
        with mp_face_mesh.FaceMesh(
            max_num_faces=1, refine_landmarks=True, min_detection_confidence=0.5, min_tracking_confidence=0.5
        ) as face_mesh:
            while cap.isOpened():
                ok, frame = cap.read()
                if not ok:
                    break

                now = time.monotonic()
                h, w = frame.shape[:2]
                rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
                results = face_mesh.process(rgb)

                if results.multi_face_landmarks:
                    lm = results.multi_face_landmarks[0].landmark
                    pts = lambda idxs: [(lm[i].x * w, lm[i].y * h) for i in idxs]

                    left_ear = eye_aspect_ratio(pts(LEFT_EYE_IDX))
                    right_ear = eye_aspect_ratio(pts(RIGHT_EYE_IDX))
                    ear = (left_ear + right_ear) / 2.0
                    mar = mouth_aspect_ratio(pts(MOUTH_IDX))

                    perclos_value = perclos.update(now, ear)
                    if perclos_value >= PERCLOS_THRESHOLD:
                        on_incident("Perclos", {"perclos": perclos_value, "ear": ear})

                    if ear < EAR_THRESHOLD:
                        eyes_closed_since = eyes_closed_since or now
                        if now - eyes_closed_since >= EAR_DURATION_SECONDS:
                            on_incident("MicroSleep", {"ear": ear, "duration_s": now - eyes_closed_since})
                    else:
                        eyes_closed_since = None

                    if mar > MAR_THRESHOLD:
                        mouth_open_since = mouth_open_since or now
                        if now - mouth_open_since >= MAR_DURATION_SECONDS:
                            on_incident("Yawning", {"mar": mar, "duration_s": now - mouth_open_since})
                    else:
                        mouth_open_since = None

                    # TODO: 3D head pose via metrics.solve_head_pose() needs a
                    # calibrated camera matrix + a 3D face model; wire up once
                    # camera intrinsics are known for the target hardware.
    finally:
        cap.release()


if __name__ == "__main__":
    run()
