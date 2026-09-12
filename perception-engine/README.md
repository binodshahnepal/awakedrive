# Perception Engine (Python)

Computer-vision prototype that computes EAR, PERCLOS, MAR, and 3D head pose
from a live camera feed, and exports the trained model to ONNX (INT8) for
on-device inference in the mobile apps and the desktop Driver HUD.

## Layout

```
src/dms_perception/
  metrics.py    EAR / MAR / PERCLOS / head-pose math (pure functions, unit-testable)
  pipeline.py   Camera capture + MediaPipe FaceMesh + incident detection loop
models/          Exported .onnx artifacts (git-ignored except via explicit allowlist)
notebooks/       Exploration / training notebooks
tests/           pytest unit tests for metrics.py
```

## Setup

```bash
python -m venv .venv
.venv\Scripts\activate
pip install -r requirements.txt
pip install -e .   # once pyproject.toml packaging is added, or use PYTHONPATH=src
```

## Run

```bash
python -m dms_perception.pipeline
```

Prints incidents (`MicroSleep`, `Perclos`, `Yawning`) to stdout. Head pose /
off-road distraction detection needs a calibrated camera matrix — see the
`TODO` in `pipeline.py`.

## Test

```bash
pytest tests/
```

## Thresholds

Mirrors `Dms.Shared.Contracts.Devices.DeviceConfig` on the backend so the
edge engine and API agree on what counts as an incident:

| Metric | Threshold | Duration |
|---|---|---|
| EAR | < 0.20 | > 1.5s → micro-sleep |
| PERCLOS | >= 15% | over a 60s window |
| MAR | > 0.60 | > 2.5s → yawning |
| Head yaw | > 25° | > 2.0s → off-road distraction |
