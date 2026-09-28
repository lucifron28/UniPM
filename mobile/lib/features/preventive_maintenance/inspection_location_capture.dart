import 'dart:async';

import 'package:geolocator/geolocator.dart';

enum DeviceLocationAccuracyMode {
  precise('Precise'),
  reduced('Reduced'),
  unknown('Unknown');

  const DeviceLocationAccuracyMode(this.apiValue);

  final String apiValue;
}

class DeviceLocationCoordinates {
  const DeviceLocationCoordinates({
    required this.latitude,
    required this.longitude,
    this.accuracyMeters = 0,
    this.hasAccuracy = true,
    this.devicePositionTimestamp,
    this.isMocked = false,
    this.accuracyMode = DeviceLocationAccuracyMode.unknown,
    this.acquisitionDurationMs = 0,
  });

  final double latitude;
  final double longitude;
  final double? accuracyMeters;
  final bool hasAccuracy;
  final DateTime? devicePositionTimestamp;
  final bool isMocked;
  final DeviceLocationAccuracyMode accuracyMode;
  final int acquisitionDurationMs;

  DeviceLocationCoordinates withCaptureMetadata({
    required DeviceLocationAccuracyMode accuracyMode,
    required int acquisitionDurationMs,
  }) => DeviceLocationCoordinates(
    latitude: latitude,
    longitude: longitude,
    accuracyMeters: accuracyMeters,
    hasAccuracy: hasAccuracy,
    devicePositionTimestamp: devicePositionTimestamp,
    isMocked: isMocked,
    accuracyMode: accuracyMode,
    acquisitionDurationMs: acquisitionDurationMs,
  );
}

enum DeviceLocationPermission { granted, denied, permanentlyDenied }

abstract interface class DeviceLocationPlatform {
  Future<bool> isLocationServiceEnabled();
  Future<DeviceLocationPermission> checkPermission();
  Future<DeviceLocationPermission> requestPermission();
  Future<DeviceLocationAccuracyMode> getAccuracyMode();
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

      final accuracyMode = await _platform.getAccuracyMode();
      final stopwatch = Stopwatch()..start();
      final position = await _platform
          .getCurrentPosition(timeout: timeout)
          .timeout(timeout);
      stopwatch.stop();
      return LocationCaptureResult.success(
        position.withCaptureMetadata(
          accuracyMode: accuracyMode,
          acquisitionDurationMs: stopwatch.elapsedMilliseconds,
        ),
      );
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
  Future<DeviceLocationAccuracyMode> getAccuracyMode() async {
    try {
      return switch (await Geolocator.getLocationAccuracy()) {
        LocationAccuracyStatus.precise => DeviceLocationAccuracyMode.precise,
        LocationAccuracyStatus.reduced => DeviceLocationAccuracyMode.reduced,
        LocationAccuracyStatus.unknown => DeviceLocationAccuracyMode.unknown,
      };
    } catch (_) {
      return DeviceLocationAccuracyMode.unknown;
    }
  }

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
      accuracyMeters: position.hasAccuracy ? position.accuracy : null,
      hasAccuracy: position.hasAccuracy,
      devicePositionTimestamp: position.timestamp,
      isMocked: position.isMocked,
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
