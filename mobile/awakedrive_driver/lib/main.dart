import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'app_state.dart';
import 'screens/hud_screen.dart';
import 'screens/login_screen.dart';

void main() {
  runApp(const AwakeDriveApp());
}

class AwakeDriveApp extends StatelessWidget {
  const AwakeDriveApp({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (_) => AppState(),
      child: MaterialApp(
        title: 'AwakeDrive Driver',
        debugShowCheckedModeBanner: false,
        theme: ThemeData(
          brightness: Brightness.dark,
          colorScheme: ColorScheme.fromSeed(seedColor: const Color(0xFF4C6FFF), brightness: Brightness.dark),
          useMaterial3: true,
        ),
        home: const _StartupGate(),
      ),
    );
  }
}

/// Restores a persisted session (if any) before deciding whether to show the
/// login screen or jump straight to the HUD.
class _StartupGate extends StatefulWidget {
  const _StartupGate();

  @override
  State<_StartupGate> createState() => _StartupGateState();
}

class _StartupGateState extends State<_StartupGate> {
  bool _ready = false;

  @override
  void initState() {
    super.initState();
    context.read<AppState>().restoreSession().then((_) {
      if (mounted) setState(() => _ready = true);
    });
  }

  @override
  Widget build(BuildContext context) {
    if (!_ready) {
      return const Scaffold(
        backgroundColor: Color(0xFF111318),
        body: Center(child: CircularProgressIndicator()),
      );
    }

    final isAuthenticated = context.watch<AppState>().authSession.isAuthenticated;
    return isAuthenticated ? const HudScreen() : const LoginScreen();
  }
}
