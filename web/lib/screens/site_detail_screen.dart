import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../api/models/check_result_response.dart';
import '../api/models/site_response.dart';
import '../sites/site_results_provider.dart';
import '../utils/relative_time.dart';

class SiteDetailScreen extends ConsumerWidget {
  final SiteResponse site;

  const SiteDetailScreen({super.key, required this.site});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final resultsAsync = ref.watch(siteResultsProvider(site.id));

    return Scaffold(
      appBar: AppBar(title: Text(site.name)),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(site.name, style: Theme.of(context).textTheme.headlineSmall),
                const SizedBox(height: 4),
                Text(site.url),
                const SizedBox(height: 4),
                Row(
                  children: [
                    Icon(
                      site.isActive ? Icons.check_circle : Icons.pause_circle,
                      size: 16,
                      color: site.isActive ? Colors.green : Colors.grey,
                    ),
                    const SizedBox(width: 4),
                    Text(site.isActive ? 'Active' : 'Inactive'),
                  ],
                ),
              ],
            ),
          ),
          const Divider(height: 1),
          Expanded(
            child: resultsAsync.when(
              data: (results) => results.isEmpty
                  ? const _EmptyResults()
                  : _ResultsListView(results: results),
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (error, stackTrace) => _ErrorResults(
                onRetry: () => ref.invalidate(siteResultsProvider(site.id)),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

// Defensive fallback for state.extra being null (e.g. a direct/internal
// navigation to /sites/:id without going through the sites list). Not
// backed by a fetch-by-id endpoint — just a clean degrade.
class SiteNotFoundScreen extends StatelessWidget {
  const SiteNotFoundScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('SiteWatch')),
      body: Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Text('Site not found.'),
            const SizedBox(height: 16),
            FilledButton(
              onPressed: () => context.go('/'),
              child: const Text('Back to sites'),
            ),
          ],
        ),
      ),
    );
  }
}

class _EmptyResults extends StatelessWidget {
  const _EmptyResults();

  @override
  Widget build(BuildContext context) {
    return const Center(child: Text('No checks have run yet.'));
  }
}

class _ErrorResults extends StatelessWidget {
  final VoidCallback onRetry;

  const _ErrorResults({required this.onRetry});

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Text('Could not load check results.'),
          const SizedBox(height: 16),
          FilledButton(onPressed: onRetry, child: const Text('Retry')),
        ],
      ),
    );
  }
}

class _ResultsListView extends StatelessWidget {
  final List<CheckResultResponse> results;

  const _ResultsListView({required this.results});

  @override
  Widget build(BuildContext context) {
    return ListView.builder(
      itemCount: results.length,
      itemBuilder: (context, index) => _ResultTile(result: results[index]),
    );
  }
}

class _ResultTile extends StatelessWidget {
  final CheckResultResponse result;

  const _ResultTile({required this.result});

  @override
  Widget build(BuildContext context) {
    return ListTile(
      leading: _StatusBadge(status: result.status),
      title: Tooltip(
        message: formatAbsoluteUtc(result.ranAt),
        child: Text(formatRelativeTime(result.ranAt)),
      ),
      subtitle: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('${result.durationMs} ms'),
          if (result.errorMessage != null)
            Text(
              result.errorMessage!,
              style: TextStyle(color: Theme.of(context).colorScheme.error),
            ),
          if (result.screenshotPath != null) const _ScreenshotIndicator(),
        ],
      ),
      isThreeLine: result.errorMessage != null || result.screenshotPath != null,
    );
  }
}

class _StatusBadge extends StatelessWidget {
  final CheckStatus status;

  const _StatusBadge({required this.status});

  @override
  Widget build(BuildContext context) {
    final (label, color) = switch (status) {
      CheckStatus.passed => ('Passed', Colors.green),
      CheckStatus.failed => ('Failed', Colors.red),
      CheckStatus.error => ('Error', Colors.amber.shade800),
    };

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.15),
        borderRadius: BorderRadius.circular(4),
      ),
      child: Text(
        label,
        style: TextStyle(color: color, fontWeight: FontWeight.bold, fontSize: 12),
      ),
    );
  }
}

class _ScreenshotIndicator extends StatelessWidget {
  const _ScreenshotIndicator();

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(top: 4),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(Icons.image_not_supported_outlined, size: 16, color: Colors.grey.shade600),
          const SizedBox(width: 4),
          Text(
            'Screenshot captured — not viewable yet',
            style: TextStyle(color: Colors.grey.shade600, fontSize: 12),
          ),
        ],
      ),
    );
  }
}
