# Android Driver App (Kotlin)

Not scaffolded from this CLI session — Android projects need Android Studio /
the Android Gradle Plugin toolchain, which isn't installed in this
environment. Create it locally with:

```bash
# In Android Studio: File > New > Project > Empty Views Activity (Kotlin)
# Package name suggestion: com.viotech.awakedrive.driver
```

Or via command line once the Android SDK/Gradle are installed:

```bash
gradle init --type kotlin-application
```

## Planned responsibilities (per DMS_FullStack_Project_Specification.docx)

- On-device ONNX Runtime Mobile inference at 60 FPS (offline-capable)
- Driver HUD: speedometer (GPS), fatigue gauges, high-decibel siren on alert
- Offline incident queue via Room (SQLite), auto-sync to `POST /api/v1/telemetry/incidents`
- Firebase Cloud Messaging for push notifications
- Auth against `POST /api/v1/auth/login`, device registration against `POST /api/v1/devices/register`

Reuse the JSON shapes in [`backend/Dms.Shared.Contracts`](../../backend/Dms.Shared.Contracts)
as the source of truth for request/response fields when writing Kotlin data classes.
