// Verifies the "Checks" button row doesn't overflow at a narrow (phone)
// width — the browser automation used earlier in this styling pass could
// not force a real narrow browser-window resize (Windows kept reporting a
// maximized 2560px viewport regardless), so this test is the actual source
// of truth for the responsive fix, not a screenshot. It also renders both
// widths out to PNG (see captureDir below) purely so there's something
// visual to review — the pass/fail assertion is the real check.
import 'dart:io';
import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:google_fonts/google_fonts.dart';
import 'package:sitewatch_web/api/models/check_result_response.dart';
import 'package:sitewatch_web/api/models/site_response.dart';
import 'package:sitewatch_web/screens/site_detail_screen.dart';
import 'package:sitewatch_web/sites/site_results_provider.dart';
import 'package:sitewatch_web/theme/app_theme.dart';

const captureDir =
    r'C:\Users\husse\AppData\Local\Temp\claude\C--Users-husse-SiteWatch\b6c62e1a-6919-47a3-8930-a48a65557933\scratchpad';

void main() {
  // Without this, GoogleFonts tries to fetch font files over the network on
  // first use, which hangs pumpAndSettle indefinitely in this sandbox (no
  // outbound network from the test runner). Layout/overflow is what this
  // test verifies, not font rendering, so the platform fallback is fine.
  GoogleFonts.config.allowRuntimeFetching = false;

  final site = SiteResponse(
    id: 'site-1',
    name: 'West Clean Admin',
    url: 'https://westcleanapp.com',
    isActive: true,
    createdAt: DateTime(2026, 1, 1),
  );

  // One result per CheckType, so all five "Run Check Now" buttons render —
  // the worst case for the button row, including the longest label
  // (Admin Order Detail) paired with the "Known issue" tag.
  final results = [
    for (final type in CheckType.values)
      CheckResultResponse(
        id: 'result-${type.name}',
        checkId: 'check-${type.name}',
        checkType: type,
        status: type == CheckType.adminOrderDetailCheck ? CheckStatus.failed : CheckStatus.passed,
        durationMs: 1000,
        errorMessage: type == CheckType.adminOrderDetailCheck
            ? "KNOWN ISSUE: order detail page still shows a server error (PHP ParseError: "
                "unexpected 'endforeach' in view.blade.php) instead of order details. "
                "This is a tracked bug, not a new failure."
            : null,
        screenshotPath: null,
        ranAt: DateTime(2026, 8, 13, 12),
      ),
  ];

  final boundaryKey = GlobalKey();

  Future<void> pumpAt(WidgetTester tester, Size size) async {
    tester.view.physicalSize = size;
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          siteResultsProvider(site.id).overrideWith((ref) async => results),
        ],
        child: MaterialApp(
          theme: buildAppTheme(),
          home: RepaintBoundary(
            key: boundaryKey,
            child: SiteDetailScreen(site: site),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
  }

  // toImage() is a real async/GPU operation — it never completes inside
  // testWidgets' default FakeAsync zone, which is what hung pumpAndSettle
  // (nothing to do with fonts or network). runAsync escapes to a real zone.
  Future<void> capturePng(WidgetTester tester, String filename) async {
    await tester.runAsync(() async {
      final boundary =
          tester.renderObject(find.byKey(boundaryKey)) as RenderRepaintBoundary;
      final image = await boundary.toImage(pixelRatio: 2.0);
      final byteData = await image.toByteData(format: ui.ImageByteFormat.png);
      Directory(captureDir).createSync(recursive: true);
      File('$captureDir/$filename').writeAsBytesSync(byteData!.buffer.asUint8List());
    });
  }

  testWidgets('renders without overflow at a narrow (iPhone SE, 375px) width',
      (tester) async {
    // testWidgets resets FlutterError.onError per-test at setup, so this
    // must be installed inside the test body, not in main() — an earlier
    // attempt in main() was silently overwritten and printed nothing.
    final originalOnError = FlutterError.onError;
    FlutterError.onError = (details) {
      // ignore: avoid_print
      print('FULL OVERFLOW DETAILS:\n${details.toString()}');
      originalOnError?.call(details);
    };

    await pumpAt(tester, const Size(375, 1600));
    final exception = tester.takeException();
    if (exception != null) {
      // ignore: avoid_print
      print('OVERFLOW DETAIL:\n$exception');
    }
    expect(exception, isNull);
    await capturePng(tester, 'site_detail_375px.png');
  });

  testWidgets('renders without overflow at a typical desktop width', (tester) async {
    await pumpAt(tester, const Size(1440, 900));
    expect(tester.takeException(), isNull);
    await capturePng(tester, 'site_detail_1440px.png');
  });
}
