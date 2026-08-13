import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import 'api/models/site_response.dart';
import 'auth/auth_controller.dart';
import 'auth/auth_state.dart';
import 'screens/login_screen.dart';
import 'screens/register_screen.dart';
import 'screens/site_detail_screen.dart';
import 'screens/sites_list_screen.dart';

const _unauthenticatedRoutes = {'/login', '/register'};

class _RouterRefreshNotifier extends ChangeNotifier {
  void notify() => notifyListeners();
}

final routerProvider = Provider<GoRouter>((ref) {
  final refresh = _RouterRefreshNotifier();
  ref.listen(authControllerProvider, (_, _) => refresh.notify());

  return GoRouter(
    initialLocation: '/login',
    refreshListenable: refresh,
    redirect: (context, state) {
      final isAuthenticated = ref.read(authControllerProvider) is AuthAuthenticated;
      final isUnauthenticatedRoute = _unauthenticatedRoutes.contains(state.matchedLocation);

      if (!isAuthenticated && !isUnauthenticatedRoute) {
        return '/login';
      }
      if (isAuthenticated && isUnauthenticatedRoute) {
        return '/';
      }
      return null;
    },
    routes: [
      GoRoute(path: '/login', builder: (context, state) => const LoginScreen()),
      GoRoute(path: '/register', builder: (context, state) => const RegisterScreen()),
      GoRoute(path: '/', builder: (context, state) => const SitesListScreen()),
      GoRoute(
        path: '/sites/:id',
        builder: (context, state) {
          final site = state.extra;
          if (site is! SiteResponse) {
            return const SiteNotFoundScreen();
          }
          return SiteDetailScreen(site: site);
        },
      ),
    ],
  );
});
