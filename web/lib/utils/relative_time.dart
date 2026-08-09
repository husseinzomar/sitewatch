// Hand-written on purpose — no date formatting package for this small a need.

String formatRelativeTime(DateTime utc, {DateTime? now}) {
  final reference = (now ?? DateTime.now()).toUtc();
  final diff = reference.difference(utc);

  if (diff.inSeconds < 60) {
    return 'just now';
  }
  if (diff.inMinutes < 60) {
    return _plural(diff.inMinutes, 'minute');
  }
  if (diff.inHours < 24) {
    return _plural(diff.inHours, 'hour');
  }
  if (diff.inDays < 30) {
    return _plural(diff.inDays, 'day');
  }
  if (diff.inDays < 365) {
    return _plural((diff.inDays / 30).floor(), 'month');
  }
  return _plural((diff.inDays / 365).floor(), 'year');
}

String _plural(int value, String unit) => '$value $unit${value == 1 ? '' : 's'} ago';

String formatAbsoluteUtc(DateTime utc) {
  final u = utc.toUtc();
  return '${u.year}-${_pad(u.month)}-${_pad(u.day)} ${_pad(u.hour)}:${_pad(u.minute)} UTC';
}

String _pad(int value) => value.toString().padLeft(2, '0');
