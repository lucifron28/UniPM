import 'dart:async';

import 'package:geolocator/geolocator.dart';

class DeviceLocationCoordinates {
  const DeviceLocationCoordinates({
    required this.latitude,
    required this.longitude,
    required this.accuracyMeters,
  });

  final double latitude;
  final double longitude;
  final double accuracyMeters;
}

enum DeviceLocationPermission { granted, denied, permanentlyDenied }

abstract interface class DeviceLocationPlatform {
  Future<bool> isLocationServiceEnabled();
  Future<DeviceLocationPermission> checkPermission();
  Future<DeviceLocationPermission> requestPermission();
  Future<DeviceLocationCoordinates> getCurrentPosition({
    required Duration timeout,
  });
}

enum LocationCaptureFailure {
  permissionDenied,
  permissionPermanentlyDenied,
  servicesDisabled,
  timedOut,
  unavailable,
}

class LocationCaptureResult {
  const LocationCaptureResult.success(this.coordinates) : failure = null;
  const LocationCaptureResult.failure(this.failure) : coordinates = null;

  final DeviceLocationCoordinates? coordinates;
  final LocationCaptureFailure? failure;
}

class InspectionLocationCapture {
  InspectionLocationCapture({
    DeviceLocationPlatform? platform,
    this.timeout = const Duration(seconds: 10),
  }) : _platform = platform ?? GeolocatorDeviceLocationPlatform();

  final DeviceLocationPlatform _platform;
  final Duration timeout;

  Future<LocationCaptureResult> capture() async {
    try {
      if (!await _platform.isLocationServiceEnabled()) {
        return const LocationCaptureResult.failure(
          LocationCaptureFailure.servicesDisabled,
        );
      }

      var permission = await _platform.checkPermission();
      if (permission == DeviceLocationPermission.denied) {
        permission = await _platform.requestPermission();
      }
      if (permission == DeviceLocationPermission.denied) {
        return const LocationCaptureResult.failure(
          LocationCaptureFailure.permissionDenied,
        );
      }
      if (permission == DeviceLocationPermission.permanentlyDenied) {
        return const LocationCaptureResult.failure(
          LocationCaptureFailure.permissionPermanentlyDenied,
        );
      }

      final coordinates = await _platform
          .getCurrentPosition(timeout: timeout)
          .timeout(timeout);
      return LocationCaptureResult.success(coordinates);
    } on TimeoutException {
      return const LocationCaptureResult.failure(
        LocationCaptureFailure.timedOut,
      );
    } on LocationServiceDisabledException {
      return const LocationCaptureResult.failure(
        LocationCaptureFailure.servicesDisabled,
      );
    } on PermissionDeniedException {
      return const LocationCaptureResult.failure(
        LocationCaptureFailure.permissionDenied,
      );
    } catch (_) {
      return const LocationCaptureResult.failure(
        LocationCaptureFailure.unavailable,
      );
    }
  }
}

class GeolocatorDeviceLocationPlatform implements DeviceLocationPlatform {
  @override
  Future<bool> isLocationServiceEnabled() =>
      Geolocator.isLocationServiceEnabled();

  @override
  Future<DeviceLocationPermission> checkPermission() async =>
      _permission(await Geolocator.checkPermission());

  @override
  Future<DeviceLocationPermission> requestPermission() async =>
      _permission(await Geolocator.requestPermission());

  @override
  Future<DeviceLocationCoordinates> getCurrentPosition({
    required Duration timeout,
  }) async {
    final position = await Geolocator.getCurrentPosition(
      locationSettings: LocationSettings(
        accuracy: LocationAccuracy.high,
        timeLimit: timeout,
      ),
    ).timeout(timeout);
    return DeviceLocationCoordinates(
      latitude: position.latitude,
      longitude: position.longitude,
      accuracyMeters: position.accuracy,
    );
  }

  DeviceLocationPermission _permission(LocationPermission permission) =>
      switch (permission) {
        LocationPermission.denied => DeviceLocationPermission.denied,
        LocationPermission.deniedForever =>
          DeviceLocationPermission.permanentlyDenied,
        LocationPermission.whileInUse ||
        LocationPermission.always => DeviceLocationPermission.granted,
        LocationPermission.unableToDetermine => DeviceLocationPermission.denied,
      };
}
