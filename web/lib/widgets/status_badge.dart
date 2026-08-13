import 'package:flutter/material.dart';

import '../api/models/check_result_response.dart';
import '../theme/app_theme.dart';

class StatusBadge extends StatelessWidget {
  final CheckStatus status;

  const StatusBadge({super.key, required this.status});

  @override
  Widget build(BuildContext context) {
    final (label, icon, color) = switch (status) {
      CheckStatus.passed => ('Passed', Icons.check_circle_rounded, AppColors.success),
      CheckStatus.failed => ('Failed', Icons.cancel_rounded, AppColors.danger),
      CheckStatus.error => ('Error', Icons.error_rounded, AppColors.caution),
    };

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: AppSpacing.sm, vertical: 4),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.1),
        borderRadius: BorderRadius.circular(999),
        border: Border.all(color: color.withValues(alpha: 0.35)),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(icon, size: 13, color: color),
          const SizedBox(width: 4),
          Text(
            label,
            style: TextStyle(color: color, fontWeight: FontWeight.w600, fontSize: 12),
          ),
        ],
      ),
    );
  }
}

// Reused in two places, deliberately with identical styling in both: the
// AdminOrderDetailCheck run button, and next to that check's entries in the
// results list. Same widget in both spots is what makes the "this check is
// different, its Failed means something else" signal read as intentional
// design rather than a one-off inconsistency.
class KnownIssueBadge extends StatelessWidget {
  const KnownIssueBadge({super.key});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
      decoration: BoxDecoration(
        color: AppColors.caution.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(4),
        border: Border.all(color: AppColors.caution.withValues(alpha: 0.4)),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Icon(Icons.bug_report_outlined, size: 12, color: AppColors.caution),
          const SizedBox(width: 3),
          const Text(
            'Known issue',
            style: TextStyle(fontSize: 10, fontWeight: FontWeight.bold, color: AppColors.caution),
          ),
        ],
      ),
    );
  }
}
