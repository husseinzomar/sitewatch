import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../api/api_client.dart';
import '../sites/sites_controller.dart';

const int _maxNameLength = 200;

Future<void> showAddSiteDialog(BuildContext context) {
  return showDialog<void>(
    context: context,
    builder: (context) => const _AddSiteDialog(),
  );
}

class _AddSiteDialog extends ConsumerStatefulWidget {
  const _AddSiteDialog();

  @override
  ConsumerState<_AddSiteDialog> createState() => _AddSiteDialogState();
}

class _AddSiteDialogState extends ConsumerState<_AddSiteDialog> {
  final _formKey = GlobalKey<FormState>();
  final _nameController = TextEditingController();
  final _urlController = TextEditingController();

  bool _submitting = false;
  String? _serverError;

  @override
  void dispose() {
    _nameController.dispose();
    _urlController.dispose();
    super.dispose();
  }

  String? _validateName(String? value) {
    if (value == null || value.isEmpty) {
      return 'Name is required.';
    }
    if (value.length > _maxNameLength) {
      return 'Name must be at most $_maxNameLength characters.';
    }
    return null;
  }

  String? _validateUrl(String? value) {
    if (value == null || value.isEmpty) {
      return 'URL is required.';
    }
    final uri = Uri.tryParse(value);
    if (uri == null || !uri.isAbsolute || (uri.scheme != 'http' && uri.scheme != 'https')) {
      return 'Enter an absolute http:// or https:// URL.';
    }
    return null;
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) {
      return;
    }

    setState(() {
      _submitting = true;
      _serverError = null;
    });

    try {
      await ref.read(sitesControllerProvider).createSite(
            _nameController.text.trim(),
            _urlController.text.trim(),
          );
      if (mounted) {
        Navigator.of(context).pop();
      }
    } on ApiValidationException catch (e) {
      setState(() => _serverError = e.message);
    } catch (_) {
      setState(() => _serverError = 'Could not add the site. Please try again.');
    } finally {
      if (mounted) {
        setState(() => _submitting = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: const Text('Add site'),
      content: Form(
        key: _formKey,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            TextFormField(
              controller: _nameController,
              decoration: const InputDecoration(labelText: 'Name'),
              validator: _validateName,
            ),
            const SizedBox(height: 16),
            TextFormField(
              controller: _urlController,
              decoration: const InputDecoration(labelText: 'URL'),
              keyboardType: TextInputType.url,
              validator: _validateUrl,
            ),
            if (_serverError != null) ...[
              const SizedBox(height: 16),
              Text(
                _serverError!,
                style: TextStyle(color: Theme.of(context).colorScheme.error),
              ),
            ],
          ],
        ),
      ),
      actions: [
        TextButton(
          onPressed: _submitting ? null : () => Navigator.of(context).pop(),
          child: const Text('Cancel'),
        ),
        FilledButton(
          onPressed: _submitting ? null : _submit,
          child: _submitting
              ? const SizedBox(
                  width: 18,
                  height: 18,
                  child: CircularProgressIndicator(strokeWidth: 2),
                )
              : const Text('Add'),
        ),
      ],
    );
  }
}
