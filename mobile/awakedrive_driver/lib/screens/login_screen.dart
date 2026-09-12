import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../app_state.dart';
import 'hud_screen.dart';

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key});

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();

  @override
  void dispose() {
    _emailController.dispose();
    _passwordController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final appState = context.read<AppState>();
    final ok = await appState.login(_emailController.text.trim(), _passwordController.text);
    if (ok && mounted) {
      Navigator.of(context).pushReplacement(MaterialPageRoute(builder: (_) => const HudScreen()));
    }
  }

  @override
  Widget build(BuildContext context) {
    final appState = context.watch<AppState>();

    return Scaffold(
      backgroundColor: const Color(0xFF111318),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 360),
            child: Padding(
              padding: const EdgeInsets.all(24),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text('AwakeDrive', style: TextStyle(color: Colors.white, fontSize: 28, fontWeight: FontWeight.bold)),
                  const SizedBox(height: 4),
                  const Text('Sign in with your driver account.', style: TextStyle(color: Colors.grey)),
                  const SizedBox(height: 24),
                  TextField(
                    controller: _emailController,
                    style: const TextStyle(color: Colors.white),
                    decoration: const InputDecoration(labelText: 'Email', labelStyle: TextStyle(color: Colors.grey)),
                    keyboardType: TextInputType.emailAddress,
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: _passwordController,
                    style: const TextStyle(color: Colors.white),
                    decoration: const InputDecoration(labelText: 'Password', labelStyle: TextStyle(color: Colors.grey)),
                    obscureText: true,
                    onSubmitted: (_) => appState.isBusy ? null : _submit(),
                  ),
                  if (appState.errorMessage != null) ...[
                    const SizedBox(height: 12),
                    Text(appState.errorMessage!, style: const TextStyle(color: Colors.redAccent)),
                  ],
                  const SizedBox(height: 20),
                  SizedBox(
                    width: double.infinity,
                    child: FilledButton(
                      onPressed: appState.isBusy ? null : _submit,
                      child: Text(appState.isBusy ? 'Signing in...' : 'Sign in'),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
