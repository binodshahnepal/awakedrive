# iOS Driver App (Swift)

Not scaffolded from this CLI session — iOS projects need Xcode, which requires
macOS and isn't available in this (Windows) environment. Create it locally
with:

```
Xcode > File > New > Project > App
Interface: SwiftUI, Language: Swift
Bundle ID suggestion: com.viotech.awakedrive.driver
```

## Planned responsibilities (per DMS_FullStack_Project_Specification.docx)

- On-device ONNX Runtime Mobile inference at 60 FPS (offline-capable)
- Driver HUD: speedometer (GPS), fatigue gauges, high-decibel siren on alert
- Offline incident queue via Core Data (SQLite-backed), auto-sync to `POST /api/v1/telemetry/incidents`
- Apple APNs for push notifications
- Auth against `POST /api/v1/auth/login`, device registration against `POST /api/v1/devices/register`

Reuse the JSON shapes in [`backend/Dms.Shared.Contracts`](../../backend/Dms.Shared.Contracts)
as the source of truth for request/response fields when writing Swift `Codable` models.
