import 'dart:typed_data';

abstract interface class InspectionPhotoEvidenceRepository {
  Future<Uint8List> getInspectionPhoto(String inspectionId);
  Future<void> saveInspectionPhoto(String inspectionId, Uint8List jpegBytes);
  Future<void> deleteInspectionPhoto(String inspectionId);
}
