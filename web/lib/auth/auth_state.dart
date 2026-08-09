sealed class AuthState {
  const AuthState();
}

class AuthUnauthenticated extends AuthState {
  const AuthUnauthenticated();
}

class AuthLoading extends AuthState {
  const AuthLoading();
}

class AuthAuthenticated extends AuthState {
  final String email;

  const AuthAuthenticated(this.email);
}

class AuthError extends AuthState {
  final String message;

  const AuthError(this.message);
}
