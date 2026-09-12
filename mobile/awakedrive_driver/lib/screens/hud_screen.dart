import 'package:camera/camera.dart';
import 'package:flutter/material.dart';
import 'package:permission_handler/permission_handler.dart';
import 'package:provider/provider.dart';

import '../app_state.dart';
import 'login_screen.dart';

class HudScreen extends StatefulWidget {
  const HudScreen({super.key});

  @override
  State<HudScreen> createState() => _HudScreenState();
}

class _HudScreenState extends State<HudScreen> {
  CameraController? _controller;
  String _cameraStatus = 'Starting camera...';

  @override
  void initState() {
    super.initState();
    _startCamera();
  }

  Future<void> _startCamera() async {
    final status = await Permission.camera.request();
    if (!status.isGranted) {
      setState(() => _cameraStatus = 'Camera permission denied. Grant it in system settings to enable the driver preview.');
      return;
    }

    try {
      final cameras = await availableCameras();
      if (cameras.isEmpty) {
        setState(() => _cameraStatus = 'No camera found on this device.');
        return;
      }

      // Prefer the front (driver-facing) camera, matching the spec's intent
      // of watching the driver, not the road.
      final camera = cameras.firstWhere(
        (c) => c.lensDirection == CameraLensDirection.front,
        orElse: () => cameras.first,
      );

      final controller = CameraController(camera, ResolutionPreset.medium, enableAudio: false);
      await controller.initialize();
      if (!mounted) return;
      setState(() {
        _controller = controller;
        _cameraStatus = 'Camera active';
      });
    } catch (e) {
      setState(() => _cameraStatus = 'Camera error: $e');
    }
  }

  @override
  void dispose() {
    _controller?.dispose();
    super.dispose();
  }

  Future<void> _signOut() async {
    final appState = context.read<AppState>();
    await appState.signOut();
    if (mounted) {
      Navigator.of(context).pushReplacement(MaterialPageRoute(builder: (_) => const LoginScreen()));
    }
  }

  @override
  Widget build(BuildContext context) {
    final appState = context.watch<AppState>();

    return Scaffold(
      backgroundColor: const Color(0xFF111318),
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Row(
                children: [
                  const Expanded(
                    child: Text('Driver HUD', style: TextStyle(color: Colors.white, fontSize: 22, fontWeight: FontWeight.bold)),
                  ),
                  Text('Queued: ${appState.pendingQueueCount}', style: const TextStyle(color: Colors.grey)),
                  const SizedBox(width: 12),
                  TextButton(onPressed: _signOut, child: const Text('Sign out')),
                ],
              ),
              const SizedBox(height: 12),
              Expanded(
                flex: 3,
                child: ClipRRect(
                  borderRadius: BorderRadius.circular(8),
                  child: Container(
                    color: Colors.black,
                    child: Stack(
                      alignment: Alignment.bottomCenter,
                      children: [
                        if (_controller != null && _controller!.value.isInitialized)
                          Positioned.fill(child: CameraPreview(_controller!))
                        else
                          const Center(child: CircularProgressIndicator()),
                        Container(
                          width: double.infinity,
                          color: Colors.black54,
                          padding: const EdgeInsets.all(8),
                          child: Text(_cameraStatus, style: const TextStyle(color: Colors.white, fontSize: 12), textAlign: TextAlign.center),
                        ),
                      ],
                    ),
                  ),
                ),
              ),
              const SizedBox(height: 12),
              // Perception engine note: this app doesn't run on-device fatigue
              // detection yet — perception-engine (Phase 1, see
              // /perception-engine) hasn't exported a model, and its output
              // format isn't decided, so faking EAR/MAR/PERCLOS here would
              // produce numbers that look real but aren't. "Simulate
              // Incident" below exercises the exact same real pipeline
              // (offline queue -> sync -> POST -> SignalR -> fleet
              // dashboards) a real detection would use, without fabricating
              // sensor readings.
              Container(
                padding: const EdgeInsets.all(12),
                decoration: BoxDecoration(color: const Color(0xFF1B1E26), borderRadius: BorderRadius.circular(8)),
                child: const Text(
                  'Perception engine: not yet wired up (no exported model from perception-engine). '
                  'Use "Simulate Incident" to exercise the full alert pipeline.',
                  style: TextStyle(color: Colors.orange, fontSize: 12),
                ),
              ),
              const SizedBox(height: 12),
              FilledButton(
                onPressed: appState.deviceId == null ? null : () => appState.simulateIncident(),
                child: const Text('Simulate Incident'),
              ),
              if (appState.errorMessage != null) ...[
                const SizedBox(height: 8),
                Text(appState.errorMessage!, style: const TextStyle(color: Colors.redAccent, fontSize: 12)),
              ],
            ],
          ),
        ),
      ),
    );
  }
}
