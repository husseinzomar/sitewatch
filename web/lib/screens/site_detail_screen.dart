import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../api/models/check_result_response.dart';
import '../api/models/site_response.dart';
import '../auth/auth_controller.dart';
import '../sites/screenshot_url_provider.dart';
import '../sites/site_results_provider.dart';
import '../theme/app_theme.dart';
import '../utils/relative_time.dart';
import '../widgets/status_badge.dart';
import '../widgets/wordmark.dart';

class SiteDetailScreen extends ConsumerStatefulWidget {
  final SiteResponse site;

  const SiteDetailScreen({super.key, required this.site});

  @override
  ConsumerState<SiteDetailScreen> createState() => _SiteDetailScreenState();
}

class _SiteDetailScreenState extends ConsumerState<SiteDetailScreen> {
  // Per-type, not a single global flag: triggering one check type shouldn't
  // block clicking another's button while it's still running.
  final Set<CheckType> _runningTypes = {};

  Future<void> _runCheck(CheckType type) async {
    setState(() => _runningTypes.add(type));
    try {
      await ref.read(apiClientProvider).runCheck(widget.site.id, type);
      ref.invalidate(siteResultsProvider(widget.site.id));
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Failed to trigger check.')),
        );
      }
    } finally {
      if (mounted) {
        setState(() => _runningTypes.remove(type));
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final site = widget.site;
    final resultsAsync = ref.watch(siteResultsProvider(site.id));

    return Scaffold(
      appBar: AppBar(title: Text(site.name)),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(
              AppSpacing.md,
              AppSpacing.md,
              AppSpacing.md,
              AppSpacing.sm,
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(site.name, style: Theme.of(context).textTheme.headlineSmall),
                const SizedBox(height: AppSpacing.xs),
                Text(
                  site.url,
                  style: Theme.of(context)
                      .textTheme
                      .bodyMedium
                      ?.copyWith(color: Colors.black54),
                ),
                const SizedBox(height: AppSpacing.xs),
                Row(
                  children: [
                    Icon(
                      site.isActive ? Icons.check_circle_rounded : Icons.pause_circle_rounded,
                      size: 16,
                      color: site.isActive ? AppColors.success : Colors.grey,
                    ),
                    const SizedBox(width: AppSpacing.xs),
                    Text(site.isActive ? 'Active' : 'Inactive'),
                  ],
                ),
              ],
            ),
          ),
          resultsAsync.maybeWhen(
            data: (results) => results.isEmpty
                ? const SizedBox.shrink()
                : _RunChecksCard(
                    results: results,
                    runningTypes: _runningTypes,
                    onRun: _runCheck,
                  ),
            orElse: () => const SizedBox.shrink(),
          ),
          const Divider(height: 1),
          Expanded(
            child: resultsAsync.when(
              data: (results) => results.isEmpty
                  ? const _EmptyResults()
                  : _ResultsListView(siteId: site.id, results: results),
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

// Grouped in a titled card rather than a loose row of buttons, so the five
// check types read as one control surface ("here's what you can trigger")
// instead of floating actions scattered on the page.
class _RunChecksCard extends StatelessWidget {
  final List<CheckResultResponse> results;
  final Set<CheckType> runningTypes;
  final void Function(CheckType type) onRun;

  const _RunChecksCard({
    required this.results,
    required this.runningTypes,
    required this.onRun,
  });

  // Below this, a Wrap of 5+ buttons — one of them carrying a "Known issue"
  // tag — doesn't reliably fit even one button's label per line without
  // truncation. Full-width vertical stacking reads better on a phone than a
  // cramped multi-row wrap.
  static const _narrowBreakpoint = 480.0;

  @override
  Widget build(BuildContext context) {
    // Distinct types present in the currently loaded results, in a fixed,
    // sensible order (enum declaration order — page/checkout basics first,
    // then the West Clean admin scenarios) rather than result order, so the
    // row doesn't reshuffle as new results come in.
    final types = CheckType.values.where((t) => results.any((r) => r.checkType == t)).toList();

    return Padding(
      padding: const EdgeInsets.fromLTRB(AppSpacing.md, 0, AppSpacing.md, AppSpacing.md),
      child: Card(
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.md),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('Checks', style: Theme.of(context).textTheme.titleSmall),
              const SizedBox(height: AppSpacing.sm),
              LayoutBuilder(
                builder: (context, constraints) {
                  final buttons = [
                    for (final type in types)
                      _RunCheckButton(
                        type: type,
                        isRunning: runningTypes.contains(type),
                        onPressed: () => onRun(type),
                      ),
                  ];

                  if (constraints.maxWidth < _narrowBreakpoint) {
                    return Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        for (final button in buttons) ...[
                          button,
                          if (button != buttons.last) const SizedBox(height: AppSpacing.sm),
                        ],
                      ],
                    );
                  }

                  return Wrap(
                    spacing: AppSpacing.sm,
                    runSpacing: AppSpacing.sm,
                    children: buttons,
                  );
                },
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _RunCheckButton extends StatelessWidget {
  final CheckType type;
  final bool isRunning;
  final VoidCallback onPressed;

  const _RunCheckButton({
    required this.type,
    required this.isRunning,
    required this.onPressed,
  });

  @override
  Widget build(BuildContext context) {
    // AdminOrderDetailCheck tracks a known, already-confirmed bug — its
    // Failed means "bug still present", the opposite of every other check's
    // Failed. Visually distinct so that isn't misread at a glance.
    final isKnownIssueCheck = type == CheckType.adminOrderDetailCheck;

    // Flexible + ellipsis on the label text: a safety net so a constrained
    // width (narrow stacked layout, or an unexpectedly long future check
    // name) truncates gracefully instead of a RenderFlex pixel overflow.
    final label = Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        if (isRunning)
          const SizedBox(
            width: 14,
            height: 14,
            child: CircularProgressIndicator(strokeWidth: 2),
          )
        else
          const Icon(Icons.play_arrow_rounded, size: 16),
        const SizedBox(width: 6),
        Flexible(
          child: Text('Run ${type.displayLabel}', overflow: TextOverflow.ellipsis),
        ),
        if (isKnownIssueCheck) ...[
          const SizedBox(width: 6),
          const KnownIssueBadge(),
        ],
      ],
    );

    return OutlinedButton(
      onPressed: isRunning ? null : onPressed,
      style: isKnownIssueCheck
          ? OutlinedButton.styleFrom(
              foregroundColor: AppColors.caution,
              side: BorderSide(color: AppColors.caution.withValues(alpha: 0.5)),
            )
          : null,
      child: label,
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
      appBar: AppBar(title: const SiteWatchWordmark()),
      body: Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Text('Site not found.'),
            const SizedBox(height: AppSpacing.md),
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
          const SizedBox(height: AppSpacing.md),
          FilledButton(onPressed: onRetry, child: const Text('Retry')),
        ],
      ),
    );
  }
}

class _ResultsListView extends StatelessWidget {
  final String siteId;
  final List<CheckResultResponse> results;

  const _ResultsListView({required this.siteId, required this.results});

  @override
  Widget build(BuildContext context) {
    return ListView.separated(
      padding: const EdgeInsets.symmetric(vertical: AppSpacing.xs),
      itemCount: results.length,
      separatorBuilder: (context, index) => const Divider(height: 1),
      itemBuilder: (context, index) => _ResultTile(siteId: siteId, result: results[index]),
    );
  }
}

class _ResultTile extends StatelessWidget {
  final String siteId;
  final CheckResultResponse result;

  const _ResultTile({required this.siteId, required this.result});

  @override
  Widget build(BuildContext context) {
    final isKnownIssueCheck = result.checkType == CheckType.adminOrderDetailCheck;

    return ListTile(
      contentPadding: const EdgeInsets.symmetric(horizontal: AppSpacing.md, vertical: AppSpacing.xs),
      leading: StatusBadge(status: result.status),
      title: Row(
        children: [
          Flexible(
            child: Tooltip(
              message: formatAbsoluteUtc(result.ranAt),
              child: Text(
                formatRelativeTime(result.ranAt),
                overflow: TextOverflow.ellipsis,
              ),
            ),
          ),
          if (isKnownIssueCheck) ...[
            const SizedBox(width: AppSpacing.sm),
            const KnownIssueBadge(),
          ],
        ],
      ),
      subtitle: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            '${result.checkType.displayLabel} • ${result.durationMs} ms',
            style: const TextStyle(color: Colors.black54, fontSize: 12),
          ),
          if (result.errorMessage != null)
            Padding(
              padding: const EdgeInsets.only(top: 2),
              child: Text(
                result.errorMessage!,
                style: TextStyle(color: Theme.of(context).colorScheme.error),
              ),
            ),
          if (result.screenshotPath != null)
            Padding(
              padding: const EdgeInsets.only(top: AppSpacing.xs),
              child: _ScreenshotThumbnail(siteId: siteId, resultId: result.id),
            ),
        ],
      ),
      isThreeLine: result.errorMessage != null || result.screenshotPath != null,
    );
  }
}

// Not viewable: covers both "old filesystem path" (pre-R2 rows, the API
// 404s for these) and a genuinely missing/expired screenshot.
class _ScreenshotUnavailable extends StatelessWidget {
  const _ScreenshotUnavailable();

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(Icons.image_not_supported_outlined, size: 16, color: Colors.grey.shade600),
        const SizedBox(width: 4),
        Text(
          'Screenshot not available',
          style: TextStyle(color: Colors.grey.shade600, fontSize: 12),
        ),
      ],
    );
  }
}

class _ScreenshotThumbnail extends ConsumerWidget {
  final String siteId;
  final String resultId;

  const _ScreenshotThumbnail({required this.siteId, required this.resultId});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final urlAsync = ref.watch(screenshotUrlProvider((siteId: siteId, resultId: resultId)));

    return urlAsync.when(
      data: (url) => url == null
          ? const _ScreenshotUnavailable()
          : GestureDetector(
              onTap: () => _showFullSize(context, url),
              child: ClipRRect(
                borderRadius: BorderRadius.circular(4),
                child: Image.network(
                  url,
                  width: 64,
                  height: 64,
                  fit: BoxFit.cover,
                  errorBuilder: (context, error, stackTrace) => const _ScreenshotUnavailable(),
                ),
              ),
            ),
      loading: () => const SizedBox(
        width: 24,
        height: 24,
        child: CircularProgressIndicator(strokeWidth: 2),
      ),
      error: (error, stackTrace) => const _ScreenshotUnavailable(),
    );
  }

  void _showFullSize(BuildContext context, String url) {
    showDialog<void>(
      context: context,
      builder: (context) => Dialog(
        child: InteractiveViewer(
          child: Image.network(url),
        ),
      ),
    );
  }
}
