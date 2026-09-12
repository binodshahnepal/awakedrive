import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'package:awakedrive_driver/main.dart';

void main() {
  setUp(() {
    SharedPreferences.setMockInitialValues({});
  });

  testWidgets('shows the login screen on a fresh install (no persisted session)', (WidgetTester tester) async {
    await tester.pumpWidget(const AwakeDriveApp());
    await tester.pumpAndSettle();

    expect(find.text('AwakeDrive'), findsOneWidget);
    expect(find.text('Sign in with your driver account.'), findsOneWidget);
    expect(find.widgetWithText(TextField, 'Email'), findsOneWidget);
  });

  testWidgets('email and password fields accept input', (WidgetTester tester) async {
    await tester.pumpWidget(const AwakeDriveApp());
    await tester.pumpAndSettle();

    await tester.enterText(find.widgetWithText(TextField, 'Email'), 'driver@example.com');
    await tester.enterText(find.widgetWithText(TextField, 'Password'), 'a-password');
    await tester.pump();

    expect(find.text('driver@example.com'), findsOneWidget);
  });
}
