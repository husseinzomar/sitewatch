class SiteResponse {
  final String id;
  final String name;
  final String url;
  final bool isActive;
  final DateTime createdAt;

  SiteResponse({
    required this.id,
    required this.name,
    required this.url,
    required this.isActive,
    required this.createdAt,
  });

  factory SiteResponse.fromJson(Map<String, dynamic> json) {
    return SiteResponse(
      id: json['id'] as String,
      name: json['name'] as String,
      url: json['url'] as String,
      isActive: json['isActive'] as bool,
      createdAt: DateTime.parse(json['createdAt'] as String),
    );
  }
}
