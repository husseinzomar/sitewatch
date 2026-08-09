import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../api/models/site_response.dart';
import '../auth/auth_controller.dart';
import 'sites_provider.dart';

final sitesControllerProvider = Provider<SitesController>((ref) => SitesController(ref));

// No local state of its own — create/delete each invalidate sitesProvider
// on success and rethrow on failure, so the calling dialog decides how to
// show the error (mirrors LoginScreen's pattern).
class SitesController {
  final Ref ref;

  SitesController(this.ref);

  Future<SiteResponse> createSite(String name, String url) async {
    final site = await ref.read(apiClientProvider).createSite(name, url);
    ref.invalidate(sitesProvider);
    return site;
  }

  Future<void> deleteSite(String id) async {
    await ref.read(apiClientProvider).deleteSite(id);
    ref.invalidate(sitesProvider);
  }
}
