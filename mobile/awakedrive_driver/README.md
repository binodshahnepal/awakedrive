# AwakeDrive Driver App (Flutter)

Cross-platform driver app — built with Flutter so the same codebase targets
Android now and iOS later with minimal extra work (the `ios/` folder is
already generated; just needs a macOS/Xcode machine to build).

## What's real vs. stubbed

Mirrors the desktop Driver HUD's honesty split (see the root
[README](../../README.md#whats-real-vs-stubbed)):

- **Real:** login (persisted across app restarts via SharedPreferences),
  device auto-registration on login, a live front-camera preview, and a
  SQLite-backed offline incident queue with background sync — the full
  pipeline from "Simulate Incident" through to both fleet dashboards
  updating live over SignalR.
- **Placeholder:** on-device fatigue detection. No perception model is
  wired into the camera feed — the preview is just a preview. "Simulate
  Incident" exercises the exact same real pipeline a detection would use,
  without fabricating EAR/MAR/PERCLOS numbers.

## Project layout

```
lib/
  main.dart                        App entry point, session-restore gate
  app_state.dart                   Central state: auth, device registration, incident queue
  models/contracts.dart            Dart mirrors of backend/Dms.Shared.Contracts (field/enum-for-field)
  services/
    api_client.dart                  REST calls to Dms.Api
    auth_session.dart                JWT + profile, persisted via SharedPreferences
    device_identity_service.dart     Stable per-install device UUID
    incident_queue_service.dart      sqflite offline queue + background sync timer
  screens/
    login_screen.dart
    hud_screen.dart                  Camera preview, queue status, Simulate Incident
test/
  contracts_test.dart               JSON round-trip + enum-ordinal tests (no platform deps)
  widget_test.dart                  Login screen render test
```

## Setup

Requires the Flutter SDK and Android toolchain (SDK + a JDK) on `PATH`.

```bash
flutter pub get
flutter analyze
flutter test
```

## Run

Point `API_BASE_URL` at your running `Dms.Api` instance. The default,
`http://10.0.2.2:5287`, is the Android emulator's alias for the host
machine's `localhost` — correct out of the box for emulator + local `dotnet
run`. For a physical device on the same network, override it:

```bash
flutter run --dart-define=API_BASE_URL=http://<your-machine-ip>:5287
```

## Build

```bash
flutter build apk --debug     # debug APK, no signing needed
flutter build apk --release   # release APK — needs a signing config first
```

## Dev-only manifest setting

`AndroidManifest.xml` sets `android:usesCleartextTraffic="true"` because
`Dms.Api` runs over plain HTTP in local dev. Remove it (and switch
`API_BASE_URL` to `https://`) before any real deployment — cleartext HTTP
should never ship to production.
