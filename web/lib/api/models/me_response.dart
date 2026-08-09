class MeResponse {
  final String id;
  final String email;

  MeResponse({required this.id, required this.email});

  factory MeResponse.fromJson(Map<String, dynamic> json) {
    return MeResponse(
      id: json['id'] as String,
      email: json['email'] as String,
    );
  }
}
