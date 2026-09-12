# AwakeDrive — Driver Drowsiness & Distraction Monitoring System (DMS)

End-to-end commercial DMS product suite: an edge computer-vision perception engine, a C# ASP.NET Core 8 backend with real-time SignalR alerts, and driver-facing + fleet-manager clients across mobile, desktop, and web.

## Architecture

| Tier | App | Stack | Role |
|---|---|---|---|
| Perception | CV Engine | Python 3.11, OpenCV, MediaPipe, PyTorch, ONNX Runtime | Computes EAR / PERCLOS / MAR / 3D head pose; exports ONNX INT8 model |
| Backend | REST API + SignalR Hub | C# ASP.NET Core 8, EF Core, PostgreSQL/SQL Server | Auth (JWT), device registry, telemetry ingestion, real-time alert push |
| Shared client | `Dms.Client.Api` | .NET 8 class library | HTTP + SignalR wrapper shared by every C# client so route strings, auth headers, and reconnection logic live in one place |
| Driver client | Mobile App | Flutter (Android now, iOS-ready) | In-vehicle HUD: camera preview, offline queue, device registration |
| Driver client | Desktop Driver HUD | WPF, .NET 8, ONNX Runtime (C#), OpenCvSharp | In-cab PC alternative to mobile: camera preview + real OpenCV Haar-cascade fatigue detection, offline SQLite queue, synthesized siren |
| Fleet manager client | Desktop Fleet Console | WPF, .NET 8 | Native admin app: live alerts (SignalR), driver/device management |
| Fleet manager client | Web Fleet Portal | Blazor Server, .NET 8 | Browser-based dashboard: live alerts, driver/device CRUD, incident history |

Shared C# DTOs/contracts live in `Dms.Shared.Contracts`; the HTTP/SignalR client wrapper lives in `Dms.Client.Api`. Both are referenced by the backend and/or all three fleet-facing C# clients.

## Repository layout

```
AwakeDrive.slnx                  .NET solution (all 6 C# projects)
/docs                            Specifications, architecture notes
/perception-engine                Python CV prototype + ONNX export pipeline
  src/dms_perception/metrics.py    EAR / MAR / PERCLOS / head-pose math
  src/dms_perception/pipeline.py   Camera capture + MediaPipe + incident loop
  tests/                           pytest unit tests
/backend/Dms.Api                   ASP.NET Core 8 Web API + SignalR Hub
  Controllers/                       Authentication, Devices, Telemetry, Users (GET list endpoints too)
  Hubs/DrowsinessHub.cs               WebSocket endpoint: /hubs/drowsiness
  Data/DmsDbContext.cs                EF Core model: Fleet, User, Device, Incident (Npgsql)
  Data/Entities/                      Entity classes (kept independent of the wire-contract DTOs)
  Migrations/                         EF Core migrations (InitialCreate, SeedDefaultAdmin)
  Services/JwtTokenService.cs         Issues JWTs consistent with the bearer-auth validation setup
/backend/Dms.Shared.Contracts      Shared DTOs/enums used by backend + C# clients
/backend/Dms.Client.Api            Shared HTTP/SignalR client (DmsApiClient, DrowsinessHubClient, AuthSession)
/mobile/awakedrive_driver         Flutter driver app (Android + iOS) — see its own README
/desktop/Dms.Desktop.DriverHud     WPF in-vehicle driver HUD
  Services/CameraService.cs          OpenCvSharp capture loop -> WPF preview + raw frames
  Services/OnnxFaceLandmarkEngine.cs ONNX model if present, else OpenCV Haar cascades — see "What's real vs. stubbed"
  Services/AudioAlertService.cs      Synthesized siren/chime/beep tones per incident severity
  Services/IncidentQueueService.cs   SQLite offline queue + background sync
  cascades/                          Bundled OpenCV Haar cascade XML files (face, eye)
/desktop/Dms.Desktop.FleetConsole  WPF fleet manager console (login + live incident grid)
/web/Dms.Web.FleetPortal           Blazor Server fleet manager portal (login, dashboard, drivers, devices)
```

## Getting started (C# projects)

```bash
dotnet build AwakeDrive.slnx

# A local Postgres on the default port 5432 is common enough that
# ConnectionStrings:Default targets port 5433 instead, so it won't collide
# with one you already have running. Spin up an isolated dev instance there:
docker run -d --name awakedrive-postgres -p 5433:5432 \
  -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=awakedrive \
  postgres:17-alpine

# Apply migrations (override ConnectionStrings:Default via User Secrets for real use)
dotnet ef database update --project backend/Dms.Api

dotnet run --project backend/Dms.Api
```

Then run any client — `dotnet run --project desktop/Dms.Desktop.FleetConsole`,
`dotnet run --project web/Dms.Web.FleetPortal`, or
`dotnet run --project desktop/Dms.Desktop.DriverHud`. Each reads the API's
base URL from its own `appsettings.json` (`Api:BaseUrl`, defaults to
`http://localhost:5287` to match `Dms.Api`'s dev launch profile).

Auth, device registration, and incident ingestion are backed by a real
Postgres schema (`Fleet`, `User`, `Device`, `Incident` — see
`backend/Dms.Api/Data/DmsDbContext.cs`). A migration seeds one local-dev Admin:

```
email:    admin@awakedrive.local
password: ChangeMe123!
```

Log in as that admin (Fleet Console or Fleet Portal, or `POST
/api/v1/auth/login` directly) to get a token, then use `POST
/api/v1/auth/register` (Admin-only — there's no public self-signup) to onboard
drivers and fleet managers. Change or remove the seeded admin before anything
beyond local dev.

## Getting started (Flutter mobile app)

```bash
cd mobile/awakedrive_driver
flutter pub get
flutter test
flutter run --dart-define=API_BASE_URL=http://<your-machine-ip>:5287
```

`API_BASE_URL` defaults to `http://10.0.2.2:5287` (the Android emulator's
alias for the host's `localhost`) — correct out of the box for
emulator + local `dotnet run`. See `mobile/awakedrive_driver/README.md`.

## What's real vs. stubbed

Verified live, not just built: backend + Postgres running, seeded admin
logged in via curl and through the Fleet Portal and Fleet Console in a real
browser/desktop session, a driver registered, a device registered, incidents
posted and confirmed to arrive on both fleet dashboards **in real time over
SignalR** (posted via curl while the dashboard was open — no refresh). The
Flutter mobile app has a genuine debug APK build (`flutter build apk
--debug` succeeds; installable `app-debug.apk` produced), `flutter analyze`
clean, and `flutter test` passing.

Every client's **"Simulate Incident"** button drives the *entire* real
pipeline: offline queue → background sync → `POST
/api/v1/telemetry/incidents` → SignalR broadcast → both fleet-manager
dashboards update live.

**Fatigue detection — read this before trusting any of the numbers it shows:**

- **Desktop Driver HUD:** `OnnxFaceLandmarkEngine` runs a real ONNX model if
  one exists at `Perception:ModelPath`; otherwise it falls back to OpenCV
  Haar cascades (bundled in `cascades/`) — genuine, camera-responsive face
  and eye detection, not fabricated numbers. But it's a coarse heuristic: EAR
  is a 3-valued estimate from eye *count* within the detected face box, not a
  continuous ratio from real landmarks, and "no face detected" is treated as
  a micro-sleep signal — which will false-positive whenever the cascade
  simply fails to find a face (poor lighting, an extreme angle, briefly
  looking down at the radio) rather than the driver actually being asleep.
  Treat its output as a rough proof-of-concept, not a validated safety
  signal, until this is tightened.
- **The ONNX path specifically needs a trained model, not just an exported
  one.** `perception-engine/export_onnx.py` exports a `FaceLandmarkModel`'s
  *architecture* — the weights are randomly initialized, never trained. If
  that output is dropped into `Perception:ModelPath`, the HUD will
  confidently report "ONNX Perception Engine active" and run real inference
  through an untrained network: numbers that look like a working model but
  are noise. Train it on labeled data (e.g. a public drowsiness/landmarks
  dataset) before relying on that path over the Haar fallback.
- **Mobile app:** no perception model is wired into the camera feed at all —
  the preview is just a preview, deliberately, for the same reason: a
  believable-looking fake number is worse than an honest "not implemented."

Everything downstream of a metric (threshold evaluation, incident creation,
queuing, syncing) is fully wired regardless of *which* metric source feeds
it, so improving detection accuracy is a drop-in change to one engine, not a
rearchitecture.

## Roadmap

See [DMS_FullStack_Project_Specification.docx](DMS_FullStack_Project_Specification.docx) for the full specification and milestone timeline.
