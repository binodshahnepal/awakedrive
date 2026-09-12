import 'dart:async';
import 'dart:convert';
import 'package:path/path.dart';
import 'package:sqflite/sqflite.dart';

import '../models/contracts.dart';
import 'api_client.dart';

/// Offline incident queue: an incident is always written to a local SQLite
/// db first, then a background timer tries to POST each queued row to
/// TelemetryController and deletes it on success. If the phone is in a
/// cellular dead zone, incidents just accumulate locally until the next
/// successful sync — the Dart equivalent of the desktop HUD's
/// IncidentQueueService (Room-queue-equivalent contract from the spec).
class IncidentQueueService {
  final DmsApiClient api;
  Database? _db;
  Timer? _syncTimer;

  final _pendingCountController = StreamController<int>.broadcast();
  Stream<int> get pendingCountStream => _pendingCountController.stream;

  IncidentQueueService({required this.api});

  Future<Database> _database() async {
    if (_db != null) return _db!;
    final path = join(await getDatabasesPath(), 'incident_queue.db');
    _db = await openDatabase(
      path,
      version: 1,
      onCreate: (db, version) => db.execute('''
        CREATE TABLE PendingIncidents (
          id INTEGER PRIMARY KEY AUTOINCREMENT,
          payloadJson TEXT NOT NULL,
          createdAtUtc TEXT NOT NULL
        )
      '''),
    );
    return _db!;
  }

  Future<void> enqueue(IncidentReport report) async {
    final db = await _database();
    await db.insert('PendingIncidents', {
      'payloadJson': jsonEncode(report.toJson()),
      'createdAtUtc': DateTime.now().toUtc().toIso8601String(),
    });
    _pendingCountController.add(await pendingCount());
  }

  Future<int> pendingCount() async {
    final db = await _database();
    final result = await db.rawQuery('SELECT COUNT(*) AS c FROM PendingIncidents');
    return Sqflite.firstIntValue(result) ?? 0;
  }

  void startBackgroundSync({Duration interval = const Duration(seconds: 15)}) {
    _syncTimer?.cancel();
    _syncTimer = Timer.periodic(interval, (_) => flushOnce());
    // Also try once immediately rather than waiting for the first tick.
    flushOnce();
  }

  void stopBackgroundSync() {
    _syncTimer?.cancel();
    _syncTimer = null;
  }

  Future<void> flushOnce() async {
    final db = await _database();
    final rows = await db.query('PendingIncidents', orderBy: 'id ASC', limit: 20);

    for (final row in rows) {
      final report = IncidentReport.fromJson(jsonDecode(row['payloadJson'] as String) as Map<String, dynamic>);
      try {
        await api.postIncident(report);
      } on DmsApiException {
        // Backend unreachable or rejected it — stop this pass, leave
        // remaining rows queued, try again next interval.
        return;
      }
      await db.delete('PendingIncidents', where: 'id = ?', whereArgs: [row['id']]);
      _pendingCountController.add(await pendingCount());
    }
  }

  void dispose() {
    _syncTimer?.cancel();
    _pendingCountController.close();
  }
}
