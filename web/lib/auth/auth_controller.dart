import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../api/api_client.dart';
import 'auth_state.dart';

final apiClientProvider = Provider<ApiClient>((ref) => ApiClient());

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
    ref.read(apiClientProvider).setToken(null);
    state = const AuthUnauthenticated();
  }
}
