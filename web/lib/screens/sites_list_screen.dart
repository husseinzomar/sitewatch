import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../api/models/site_response.dart';
import '../auth/auth_controller.dart';
import '../sites/sites_controller.dart';
import '../sites/sites_provider.dart';
import '../theme/app_theme.dart';
import '../widgets/wordmark.dart';
import 'add_site_dialog.dart';

class SitesListScreen extends ConsumerWidget {
  const SitesListScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final sitesAsync = ref.watch(sitesProvider);

    return Scaffold(
      appBar: AppBar(
        title: const SiteWatchWordmark(),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            tooltip: 'Refresh',
            onPressed: () => ref.invalidate(sitesProvider),
          ),
          IconButton(
            icon: const Icon(Icons.logout),
            tooltip: 'Log out',
            onPressed: () => ref.read(authControllerProvider.notifier).logout(),
          ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: () async {
          final _ = await ref.refresh(sitesProvider.future);
        },
        child: sitesAsync.when(
          data: (sites) => sites.isEmpty
              ? _EmptyState(onAdd: () => showAddSiteDialog(context))
              : _SitesListView(sites: sites),
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (error, stackTrace) => _ErrorState(onRetry: () => ref.invalidate(sitesProvider)),
        ),
      ),
      floatingActionButton: FloatingActionButton(
        onPressed: () => showAddSiteDialog(context),
        tooltip: 'Add site',
        child: const Icon(Icons.add),
      ),
    );
  }
}

class _EmptyState extends StatelessWidget {
  final VoidCallback onAdd;

  const _EmptyState({required this.onAdd});

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) => SingleChildScrollView(
        physics: const AlwaysScrollableScrollPhysics(),
        child: ConstrainedBox(
          constraints: BoxConstraints(minHeight: constraints.maxHeight),
          child: Center(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                const Text('No sites yet.'),
                const SizedBox(height: 16),
                FilledButton(onPressed: onAdd, child: const Text('Add your first site')),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _ErrorState extends StatelessWidget {
  final VoidCallback onRetry;

  const _ErrorState({required this.onRetry});

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) => SingleChildScrollView(
        physics: const AlwaysScrollableScrollPhysics(),
        child: ConstrainedBox(
          constraints: BoxConstraints(minHeight: constraints.maxHeight),
          child: Center(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                const Text('Could not load your sites.'),
                const SizedBox(height: 16),
                FilledButton(onPressed: onRetry, child: const Text('Retry')),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _SitesListView extends ConsumerWidget {
  final List<SiteResponse> sites;

  const _SitesListView({required this.sites});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return ListView.builder(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.all(AppSpacing.md),
      itemCount: sites.length,
      itemBuilder: (context, index) {
        final site = sites[index];
        return Padding(
          padding: const EdgeInsets.only(bottom: AppSpacing.sm),
          child: Card(
            child: ListTile(
              title: Text(site.name),
              subtitle: Text('${site.url} • ${site.isActive ? 'Active' : 'Inactive'}'),
              onTap: () => context.push('/sites/${site.id}', extra: site),
              trailing: IconButton(
                icon: const Icon(Icons.delete_outline),
                tooltip: 'Delete',
                onPressed: () => _confirmDelete(context, ref, site),
              ),
            ),
          ),
        );
      },
    );
  }

  Future<void> _confirmDelete(BuildContext context, WidgetRef ref, SiteResponse site) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Delete site?'),
        content: Text(
          'Delete "${site.name}"? This can\'t be undone and its check history will be deleted too.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () => Navigator.of(context).pop(true),
            child: const Text('Delete'),
          ),
        ],
      ),
    );

    if (confirmed != true || !context.mounted) {
      return;
    }

    try {
      await ref.read(sitesControllerProvider).deleteSite(site.id);
    } catch (_) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Could not delete the site. Please try again.')),
        );
      }
    }
  }
}
