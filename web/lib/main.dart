import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'router.dart';
import 'theme/app_theme.dart';

void main() {
  runApp(const ProviderScope(child: SiteWatchApp()));
}

class SiteWatchApp extends ConsumerWidget {
  const SiteWatchApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final router = ref.watch(routerProvider);

    return MaterialApp.router(
      title: 'SiteWatch',
      theme: buildAppTheme(),
      routerConfig: router,
    );
  }
}
