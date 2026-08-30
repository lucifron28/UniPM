import 'package:flutter/foundation.dart';

import '../../api/api_exception.dart';
import '../../auth/auth_models.dart';
import '../assets/asset_models.dart';
import 'preventive_maintenance_models.dart';
import 'preventive_maintenance_repository.dart';

class PreventiveMaintenanceController extends ChangeNotifier {
  PreventiveMaintenanceController({
    required this.repository,
    required this.user,
  });

  final PreventiveMaintenanceRepository repository;
  final AuthUser user;

  List<PreventiveMaintenanceForm> forms = const [];
  PreventiveMaintenanceForm? selectedForm;
  bool isLoading = false;
  bool isSaving = false;
  String? errorMessage;

  bool get isGsd => user.roles.contains('GSD');

  List<PreventiveMaintenanceForm> get visibleDrafts => forms
      .where((form) => form.isDraft)
      .where((form) => isGsd || form.createdByUserId == user.id)
      .toList(growable: false);

  Future<void> loadForms() async {
    isLoading = true;
    errorMessage = null;
    notifyListeners();
    try {
      forms = await repository.listForms();
    } catch (error) {
      errorMessage = friendlyError(error);
    } finally {
      isLoading = false;
      notifyListeners();
    }
  }

  Future<PreventiveMaintenanceForm?> createDraft(
    CreatePreventiveMaintenanceFormInput input,
  ) async {
    return _runSaving(() async {
      final created = await repository.createForm(input);
      forms = [created, ...forms];
      selectedForm = created;
      return created;
    });
  }

  Future<void> loadDraft(String formId) async {
    isLoading = true;
    errorMessage = null;
    selectedForm = null;
    notifyListeners();
    try {
      selectedForm = await repository.getForm(formId);
    } catch (error) {
      errorMessage = friendlyError(error);
    } finally {
      isLoading = false;
      notifyListeners();
    }
  }

  void selectForm(PreventiveMaintenanceForm form) {
    selectedForm = form;
    errorMessage = null;
    notifyListeners();
  }

  PmDraftResolution resolveDraftFor(Asset asset, ScheduleOption schedule) {
    final drafts = visibleDrafts;
    for (final form in drafts) {
      for (final inspection in form.inspections) {
        if (inspection.scheduleId == schedule.id) {
          return PmDraftResolution.resume(
            form: form,
            inspectionId: inspection.id,
          );
        }
      }
    }

    final grouping = PreventiveMaintenanceGrouping.fromAssetAndSchedule(
      asset,
      schedule,
    );
    final compatibleDrafts = drafts
        .where(grouping.matches)
        .toList(growable: false);
    return switch (compatibleDrafts.length) {
      0 => PmDraftResolution.create(grouping),
      1 => PmDraftResolution.reuse(
        form: compatibleDrafts.single,
        grouping: grouping,
      ),
      _ => PmDraftResolution.choose(
        forms: compatibleDrafts,
        grouping: grouping,
      ),
    };
  }

  Future<bool> addInspection(AddInspectionInput input) async {
    final form = selectedForm;
    if (form == null) return false;
    if (form.inspections.any((row) => row.scheduleId == input.scheduleId)) {
      errorMessage = 'This schedule is already included in the draft.';
      notifyListeners();
      return false;
    }
    return (await _runSaving(() async {
          final row = await repository.addInspection(form.id, input);
          _replaceSelected(
            form.copyWith(inspections: [...form.inspections, row]),
          );
          return true;
        })) ??
        false;
  }

  Future<bool> updateInspection(
    String inspectionId,
    UpdateInspectionInput input,
  ) async {
    final form = selectedForm;
    if (form == null) return false;
    return (await _runSaving(() async {
          final row = await repository.updateInspection(
            form.id,
            inspectionId,
            input,
          );
          final rows = form.inspections
              .map(
                (candidate) => candidate.id == inspectionId ? row : candidate,
              )
              .toList(growable: false);
          _replaceSelected(form.copyWith(inspections: rows));
          return true;
        })) ??
        false;
  }

  Future<bool> deleteInspection(String inspectionId) async {
    final form = selectedForm;
    if (form == null) return false;
    return (await _runSaving(() async {
          await repository.deleteInspection(form.id, inspectionId);
          _replaceSelected(
            form.copyWith(
              inspections: form.inspections
                  .where((row) => row.id != inspectionId)
                  .toList(growable: false),
            ),
          );
          return true;
        })) ??
        false;
  }

  void clearSelection() {
    selectedForm = null;
    errorMessage = null;
    notifyListeners();
  }

  Future<T?> _runSaving<T>(Future<T> Function() action) async {
    isSaving = true;
    errorMessage = null;
    notifyListeners();
    try {
      return await action();
    } catch (error) {
      errorMessage = friendlyError(error);
      return null;
    } finally {
      isSaving = false;
      notifyListeners();
    }
  }

  void _replaceSelected(PreventiveMaintenanceForm form) {
    selectedForm = form;
    forms = forms
        .map((candidate) => candidate.id == form.id ? form : candidate)
        .toList(growable: false);
  }
}

enum PmDraftResolutionKind { resume, reuse, create, choose }

class PmDraftResolution {
  const PmDraftResolution._({
    required this.kind,
    required this.forms,
    required this.grouping,
    this.inspectionId,
  });

  factory PmDraftResolution.resume({
    required PreventiveMaintenanceForm form,
    required String inspectionId,
  }) => PmDraftResolution._(
    kind: PmDraftResolutionKind.resume,
    forms: [form],
    grouping: null,
    inspectionId: inspectionId,
  );

  factory PmDraftResolution.reuse({
    required PreventiveMaintenanceForm form,
    required PreventiveMaintenanceGrouping grouping,
  }) => PmDraftResolution._(
    kind: PmDraftResolutionKind.reuse,
    forms: [form],
    grouping: grouping,
  );

  factory PmDraftResolution.create(PreventiveMaintenanceGrouping grouping) =>
      PmDraftResolution._(
        kind: PmDraftResolutionKind.create,
        forms: const [],
        grouping: grouping,
      );

  factory PmDraftResolution.choose({
    required List<PreventiveMaintenanceForm> forms,
    required PreventiveMaintenanceGrouping grouping,
  }) => PmDraftResolution._(
    kind: PmDraftResolutionKind.choose,
    forms: forms,
    grouping: grouping,
  );

  final PmDraftResolutionKind kind;
  final List<PreventiveMaintenanceForm> forms;
  final PreventiveMaintenanceGrouping? grouping;
  final String? inspectionId;

  PreventiveMaintenanceForm? get form =>
      forms.length == 1 ? forms.single : null;
}

String friendlyError(Object error) {
  if (error is ApiException) {
    switch (error.statusCode) {
      case 403:
        return 'This account cannot edit preventive-maintenance drafts.';
      case 404:
        return 'The requested form, schedule, or reference was not found.';
      case 409:
        return 'This draft has a conflict. Refresh it and try again.';
      case 400:
        return 'The server rejected these values. Check the form and try again.';
    }
    return error.message;
  }
  if (error is FormatException) {
    return 'The server returned an invalid response.';
  }
  return 'The mobile service is unavailable. Please try again.';
}
