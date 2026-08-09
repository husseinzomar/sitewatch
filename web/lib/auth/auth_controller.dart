import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../api/api_client.dart';
import 'auth_state.dart';

final apiClientProvider = Provider<ApiClient>((ref) {
  final client = ApiClient();
  // Single wiring point for session expiry: any 401, from any screen or
  // provider, routes through here.
  client.onUnauthorized = () => ref.read(authControllerProvider.notifier).logout();
  return client;
});

final authControllerProvider = NotifierProvider<AuthController, AuthState>(AuthController.new);

class AuthController extends Notifier<AuthState> {
  @override
  AuthState build() => const AuthUnauthenticated();

  Future<void> login(String email, String password) async {
    final apiClient = ref.read(apiClientProvider);
    state = const AuthLoading();

    try {
      final loginResponse = await apiClient.login(email, password);
      apiClient.setToken(loginResponse.token);

      final me = await apiClient.getMe();
      state = AuthAuthenticated(me.email);
    } on InvalidCredentialsException {
      apiClient.setToken(null);
      state = const AuthError('Invalid email or password.');
    } on NetworkException {
      apiClient.setToken(null);
      state = const AuthError('Could not reach the server. Check your connection and try again.');
    } catch (_) {
      apiClient.setToken(null);
      state = const AuthError('Something went wrong. Please try again.');
    }
  }

  void logout() {
    // Guards against concurrent 401s (e.g. sites list + a delete in flight
    // when the token expires) both firing onUnauthorized and calling this
    // twice — the second call is a no-op rather than redundant work.
    if (state is AuthUnauthenticated) {
      return;
    }

    ref.read(apiClientProvider).setToken(null);
    state = const AuthUnauthenticated();
  }
}
