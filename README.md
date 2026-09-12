# AwakeDrive — Driver Drowsiness & Distraction Monitoring System (DMS)

End-to-end commercial DMS product suite: an edge computer-vision perception engine, a C# ASP.NET Core 8 backend with real-time SignalR alerts, and driver-facing + fleet-manager clients across mobile, desktop, and web.

## Architecture

| Tier | App | Stack | Role |
|---|---|---|---|
| Perception | CV Engine | Python 3.11, OpenCV, MediaPipe, PyTorch, ONNX Runtime | Computes EAR / PERCLOS / MAR / 3D head pose; exports ONNX INT8 model |
| Backend | REST API + SignalR Hub | C# ASP.NET Core 8, EF Core, PostgreSQL/SQL Server | Auth (JWT), device registry, telemetry ingestion, real-time alert push |
| Shared client | `Dms.Client.Api` | .NET 8 class library | HTTP + SignalR wrapper shared by every C# client so route strings, auth headers, and reconnection logic live in one place |
| Driver client | Mobile App | Android (Kotlin) / iOS (Swift) | In-vehicle HUD + on-device ONNX inference node |
| Driver client | Desktop Driver HUD | WPF, .NET 8, ONNX Runtime (C#), OpenCvSharp | In-cab PC alternative to mobile: camera preview, offline SQLite queue, siren |
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
/mobile/android                   Android driver app (Kotlin) — see README for setup
/mobile/ios                       iOS driver app (Swift) — see README for setup
/desktop/Dms.Desktop.DriverHud     WPF in-vehicle driver HUD
  Services/CameraService.cs          OpenCvSharp capture loop -> WPF preview + raw frames
  Services/OnnxFaceLandmarkEngine.cs ONNX inference — see "What's real vs. stubbed" below
  Services/IncidentQueueService.cs   SQLite offline queue + background sync
/desktop/Dms.Desktop.FleetConsole  WPF fleet manager console (login + live incident grid)
/web/Dms.Web.FleetPortal           Blazor Server fleet manager portal (login, dashboard, drivers, devices)
```

## Getting started (C# projects)

```bash
dotnet build AwakeDrive.slnx

# Apply migrations to a local Postgres instance (see ConnectionStrings:Default
# in backend/Dms.Api/appsettings.json — override via User Secrets for real use)
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

## What's real vs. stubbed

Everything below **runs end-to-end**: sign in on the Fleet Console or Fleet
Portal → see live incidents; sign in on the Driver HUD → it registers itself
as a device, shows a live camera preview, and its **"Simulate Incident"**
button drives the *entire* real pipeline (SQLite queue → background sync →
`POST /api/v1/telemetry/incidents` → SignalR broadcast → both fleet-manager
dashboards update in real time).

What's **not** implemented, on purpose, and why:

- **Real fatigue detection in the Driver HUD.** `OnnxFaceLandmarkEngine` loads
  a `.onnx` file if one exists at `Perception:ModelPath`, but always returns
  "not ready" — because perception-engine (Phase 1) hasn't exported a model
  yet, and its output tensor layout isn't decided. Faking that mapping would
  produce numbers that *look* like real EAR/MAR/PERCLOS but aren't. See the
  class comment in `OnnxFaceLandmarkEngine.cs` for exactly what to fill in
  once a model exists — the threshold-evaluation and incident-triggering code
  downstream of it is already fully wired and will "just work".
- **Mobile apps.** Android/iOS need their native toolchains (Android
  Studio/Gradle, Xcode on macOS), neither available in this dev environment —
  see the READMEs in `mobile/android` and `mobile/ios` for what to build.

## Roadmap

See [DMS_FullStack_Project_Specification.docx](DMS_FullStack_Project_Specification.docx) for the full specification and milestone timeline.
