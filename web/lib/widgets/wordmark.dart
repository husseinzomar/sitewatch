import 'package:flutter/material.dart';

import '../theme/app_theme.dart';

// Used in place of a plain Text('SiteWatch') AppBar title, wherever the app
// name (not a site name) is shown — sites list and login only; the site
// detail screen's AppBar correctly shows the site's own name instead.
class SiteWatchWordmark extends StatelessWidget {
  const SiteWatchWordmark({super.key});

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Container(
          width: 28,
          height: 28,
          decoration: BoxDecoration(
            color: AppColors.primary,
            borderRadius: BorderRadius.circular(7),
          ),
          child: const Icon(Icons.monitor_heart_rounded, size: 18, color: Colors.white),
        ),
        const SizedBox(width: AppSpacing.sm),
        Text(
          'SiteWatch',
          style: Theme.of(context).textTheme.titleLarge?.copyWith(
                fontWeight: FontWeight.w700,
                letterSpacing: -0.2,
              ),
        ),
      ],
    );
  }
}
