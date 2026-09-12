# AwakeDrive — Driver Drowsiness & Distraction Monitoring System (DMS)

End-to-end commercial DMS product suite: an edge computer-vision perception engine, a C# ASP.NET Core 8 backend with real-time SignalR alerts, and driver-facing + fleet-manager clients across mobile, desktop, and web.

## Architecture

| Tier | App | Stack | Role |
|---|---|---|---|
| Perception | CV Engine | Python 3.11, OpenCV, MediaPipe, PyTorch, ONNX Runtime | Computes EAR / PERCLOS / MAR / 3D head pose; exports ONNX INT8 model |
| Backend | REST API + SignalR Hub | C# ASP.NET Core 8, EF Core, PostgreSQL/SQL Server | Auth (JWT), device registry, telemetry ingestion, real-time alert push |
| Driver client | Mobile App | Android (Kotlin) / iOS (Swift) | In-vehicle HUD + on-device ONNX inference node |
| Driver client | Desktop Driver HUD | WPF, .NET 8, ONNX Runtime (C#), OpenCvSharp | In-cab PC alternative to mobile, same HUD/offline-queue behavior |
| Fleet manager client | Desktop Fleet Console | WPF, .NET 8, SignalR Client | Native admin app: live alerts, driver/device management, reporting |
| Fleet manager client | Web Fleet Portal | Blazor Server, .NET 8 | Browser-based admin dashboard: live alerts, driver/device CRUD, incident history |

Shared C# DTOs/contracts live in `Dms.Shared.Contracts`, referenced by the backend and all three C# clients.

## Repository layout (planned)

```
/docs                          Specifications, architecture notes
/perception-engine              Python CV prototype + ONNX export pipeline
/backend/Dms.Api                 ASP.NET Core 8 Web API + SignalR Hub
/backend/Dms.Shared.Contracts    Shared DTOs/enums used by backend + C# clients
/mobile/android                 Android driver app (Kotlin)
/mobile/ios                     iOS driver app (Swift)
/desktop/Dms.Desktop.DriverHud   WPF in-vehicle driver HUD
/desktop/Dms.Desktop.FleetConsole WPF fleet manager console
/web/Dms.Web.FleetPortal         Blazor Server fleet manager portal
```

## Roadmap

See [DMS_FullStack_Project_Specification.docx](DMS_FullStack_Project_Specification.docx) for the full specification and milestone timeline.
