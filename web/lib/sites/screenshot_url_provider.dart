import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../auth/auth_controller.dart';

typedef ScreenshotKey = ({String siteId, String resultId});

// autoDispose: presigned URLs are valid for 1 hour. Discarding the cached
// value once nothing is watching it means navigating back to a result
// later re-fetches a fresh URL rather than reusing a stale one. This does
// NOT cover staying on the same screen past an hour with the tab open —
// see IDEAS.md.
final screenshotUrlProvider =
    FutureProvider.family.autoDispose<String?, ScreenshotKey>((ref, key) async {
  final apiClient = ref.watch(apiClientProvider);
  return apiClient.getScreenshotUrl(key.siteId, key.resultId);
});
