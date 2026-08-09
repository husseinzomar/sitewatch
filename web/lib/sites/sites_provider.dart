import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../api/models/site_response.dart';
import '../auth/auth_controller.dart';

final sitesProvider = FutureProvider<List<SiteResponse>>((ref) async {
  final apiClient = ref.watch(apiClientProvider);
  return apiClient.getSites();
});
