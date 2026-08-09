enum CheckStatus {
  passed,
  failed,
  error;

  // Matches the API's JsonStringEnumConverter output ("Passed"/"Failed"/
  // "Error") — decoded by name, not by index, so a C# enum reordering
  // can't silently break this the way an int would have.
  static CheckStatus fromJson(String value) {
    switch (value) {
      case 'Passed':
        return CheckStatus.passed;
      case 'Failed':
        return CheckStatus.failed;
      case 'Error':
        return CheckStatus.error;
      default:
        throw FormatException('Unknown CheckStatus: $value');
    }
  }
}

class CheckResultResponse {
  final String id;
  final String checkId;
  final CheckStatus status;
  final int durationMs;
  final String? errorMessage;
  final String? screenshotPath;
  final DateTime ranAt;

  CheckResultResponse({
    required this.id,
    required this.checkId,
    required this.status,
    required this.durationMs,
    required this.errorMessage,
    required this.screenshotPath,
    required this.ranAt,
  });

  factory CheckResultResponse.fromJson(Map<String, dynamic> json) {
    return CheckResultResponse(
      id: json['id'] as String,
      checkId: json['checkId'] as String,
      status: CheckStatus.fromJson(json['status'] as String),
      durationMs: json['durationMs'] as int,
      errorMessage: json['errorMessage'] as String?,
      screenshotPath: json['screenshotPath'] as String?,
      ranAt: DateTime.parse(json['ranAt'] as String),
    );
  }
}
