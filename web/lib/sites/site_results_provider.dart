import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../api/models/check_result_response.dart';
import '../auth/auth_controller.dart';

final siteResultsProvider =
    FutureProvider.family<List<CheckResultResponse>, String>((ref, siteId) async {
  final apiClient = ref.watch(apiClientProvider);
  return apiClient.getSiteResults(siteId);
});
