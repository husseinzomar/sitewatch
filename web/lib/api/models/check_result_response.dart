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

// Mirrors SiteWatch.Core.Entities.CheckType. Decoded/encoded by name (via
// apiValue / fromJson), never by index — same reasoning as CheckStatus: the
// C# enum is appended-only and stored as int, so its declaration order can
// shift meaning without this Dart enum's *names* ever changing.
enum CheckType {
  pageLoad,
  checkoutFlow,
  adminDashboardCheck,
  adminOverviewCheck,
  adminOrdersCheck,
  adminUsersCheck,
  adminOrderDetailCheck;

  static CheckType fromJson(String value) {
    switch (value) {
      case 'PageLoad':
        return CheckType.pageLoad;
      case 'CheckoutFlow':
        return CheckType.checkoutFlow;
      case 'AdminDashboardCheck':
        return CheckType.adminDashboardCheck;
      case 'AdminOverviewCheck':
        return CheckType.adminOverviewCheck;
      case 'AdminOrdersCheck':
        return CheckType.adminOrdersCheck;
      case 'AdminUsersCheck':
        return CheckType.adminUsersCheck;
      case 'AdminOrderDetailCheck':
        return CheckType.adminOrderDetailCheck;
      default:
        throw FormatException('Unknown CheckType: $value');
    }
  }

  // Used to build the run-check query string — must round-trip back to the
  // exact names above.
  String get apiValue => switch (this) {
        CheckType.pageLoad => 'PageLoad',
        CheckType.checkoutFlow => 'CheckoutFlow',
        CheckType.adminDashboardCheck => 'AdminDashboardCheck',
        CheckType.adminOverviewCheck => 'AdminOverviewCheck',
        CheckType.adminOrdersCheck => 'AdminOrdersCheck',
        CheckType.adminUsersCheck => 'AdminUsersCheck',
        CheckType.adminOrderDetailCheck => 'AdminOrderDetailCheck',
      };

  // Non-technical, human-readable label for the "Run Check Now" buttons.
  String get displayLabel => switch (this) {
        CheckType.pageLoad => 'Page Load',
        CheckType.checkoutFlow => 'Checkout Flow',
        CheckType.adminDashboardCheck => 'Admin Dashboard',
        CheckType.adminOverviewCheck => 'Admin Overview',
        CheckType.adminOrdersCheck => 'Admin Orders',
        CheckType.adminUsersCheck => 'Admin Users',
        CheckType.adminOrderDetailCheck => 'Admin Order Detail',
      };
}

class CheckResultResponse {
  final String id;
  final String checkId;
  final CheckType checkType;
  final CheckStatus status;
  final int durationMs;
  final String? errorMessage;
  final String? screenshotPath;
  final DateTime ranAt;

  CheckResultResponse({
    required this.id,
    required this.checkId,
    required this.checkType,
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
      checkType: CheckType.fromJson(json['checkType'] as String),
      status: CheckStatus.fromJson(json['status'] as String),
      durationMs: json['durationMs'] as int,
      errorMessage: json['errorMessage'] as String?,
      screenshotPath: json['screenshotPath'] as String?,
      ranAt: DateTime.parse(json['ranAt'] as String),
    );
  }
}
