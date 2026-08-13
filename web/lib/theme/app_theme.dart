import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';

// Central color + spacing tokens. Material's ColorScheme has no slot for a
// 3-way check status system (Passed/Failed/Error) plus a separate "known
// issue" meta-tag, so the two tokens without a native ColorScheme home
// (success, caution) live here rather than as scattered Colors.green/red
// literals in widget files. primary/danger still feed the real ColorScheme
// (see buildAppTheme) so standard Material widgets pick them up too.
class AppColors {
  AppColors._();

  static const primary = Color(0xFF0E7C86);
  static const ink = Color(0xFF16232A);

  // Darker than the first pass (was 0xFFF3F6F7) — that was too close to the
  // white cards to read as a distinct surface at a glance, only visible on
  // close inspection. This gives the card grouping real separation.
  static const surfaceBackground = Color(0xFFE4EAEC);
  static const success = Color(0xFF1D8348);
  static const danger = Color(0xFFC0392B);

  // Shared by the Error status badge and the AdminOrderDetailCheck
  // "known issue" tag — both mean "not a clean pass/fail, read the
  // message," differentiated by shape/placement, not a competing color.
  static const caution = Color(0xFFB7791F);
}

class AppSpacing {
  AppSpacing._();

  static const xs = 4.0;
  static const sm = 8.0;
  static const md = 16.0;
  static const lg = 24.0;
  static const xl = 32.0;
}

ThemeData buildAppTheme() {
  final colorScheme = ColorScheme.fromSeed(
    seedColor: AppColors.primary,
    brightness: Brightness.light,
    primary: AppColors.primary,
    error: AppColors.danger,
    tertiary: AppColors.caution,
    surface: Colors.white,
  );

  // Space Grotesk for headings/wordmark, Inter for body/data — see
  // CLAUDE.md's design-pass notes for the reasoning.
  final headingFont = GoogleFonts.spaceGroteskTextTheme();
  final bodyTextTheme = GoogleFonts.interTextTheme();

  final textTheme = bodyTextTheme.copyWith(
    displayLarge: headingFont.displayLarge?.copyWith(color: AppColors.ink),
    displayMedium: headingFont.displayMedium?.copyWith(color: AppColors.ink),
    displaySmall: headingFont.displaySmall?.copyWith(color: AppColors.ink),
    headlineLarge: headingFont.headlineLarge?.copyWith(color: AppColors.ink),
    headlineMedium: headingFont.headlineMedium?.copyWith(color: AppColors.ink),
    headlineSmall: headingFont.headlineSmall
        ?.copyWith(color: AppColors.ink, fontWeight: FontWeight.w600),
    titleLarge: headingFont.titleLarge
        ?.copyWith(color: AppColors.ink, fontWeight: FontWeight.w600),
    titleMedium: headingFont.titleMedium
        ?.copyWith(color: AppColors.ink, fontWeight: FontWeight.w600),
    titleSmall: headingFont.titleSmall
        ?.copyWith(color: AppColors.ink, fontWeight: FontWeight.w600),
  );

  return ThemeData(
    useMaterial3: true,
    colorScheme: colorScheme,
    scaffoldBackgroundColor: AppColors.surfaceBackground,
    textTheme: textTheme,
    appBarTheme: const AppBarTheme(
      backgroundColor: Colors.white,
      foregroundColor: AppColors.ink,
      elevation: 0,
      scrolledUnderElevation: 1,
      surfaceTintColor: Colors.transparent,
    ),
    cardTheme: CardThemeData(
      // A restrained elevation + darker background together carry the
      // separation now, instead of relying on a near-invisible border alone.
      elevation: 1,
      shadowColor: AppColors.ink.withValues(alpha: 0.16),
      color: Colors.white,
      margin: EdgeInsets.zero,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(12),
        side: BorderSide(color: AppColors.ink.withValues(alpha: 0.1)),
      ),
    ),
    filledButtonTheme: FilledButtonThemeData(
      style: FilledButton.styleFrom(
        backgroundColor: AppColors.primary,
        foregroundColor: Colors.white,
      ),
    ),
    elevatedButtonTheme: ElevatedButtonThemeData(
      style: ElevatedButton.styleFrom(
        backgroundColor: AppColors.primary,
        foregroundColor: Colors.white,
      ),
    ),
    outlinedButtonTheme: OutlinedButtonThemeData(
      style: OutlinedButton.styleFrom(
        foregroundColor: AppColors.primary,
        side: BorderSide(color: AppColors.primary.withValues(alpha: 0.5)),
      ),
    ),
  );
}
