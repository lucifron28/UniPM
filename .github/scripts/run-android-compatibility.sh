#!/usr/bin/env bash
set -euo pipefail

android_version="$1"
api_level="$2"
profile="$3"
screen_class="$4"

cd mobile
mkdir -p artifacts/nonfunctional-android
log="artifacts/nonfunctional-android/run-${api_level}.log"

{
  echo "android_version=${android_version}"
  echo "api_level=${api_level}"
  echo "profile=${profile}"
  echo "screen_class=${screen_class}"
  echo "Collecting emulator display metadata"
  echo "device_release=$(timeout 30s adb shell getprop ro.build.version.release)"
  echo "device_api=$(timeout 30s adb shell getprop ro.build.version.sdk)"
  echo "screen_size=$(timeout 30s adb shell wm size)"
  echo "density_dpi=$(timeout 30s adb shell getprop qemu.sf.lcd_density)"
  echo "Building the debug APK"
  timeout 15m flutter build apk --debug --target-platform android-x64 --dart-define=UNIPM_API_BASE_URL=http://10.0.2.2:5254/
  echo "Installing the APK with bounded ADB timeout"
  timeout 8m adb install -r build/app/outputs/flutter-apk/app-debug.apk
  echo "Launching MainActivity"
  launch_output=$(timeout 120s adb shell am start -W -n io.github.lucifron28.unipm/.MainActivity)
  printf '%s\n' "${launch_output}"
  if printf '%s\n' "${launch_output}" | grep -q 'Status: ok'; then
    echo "activity_start_status=ok"
  elif printf '%s\n' "${launch_output}" | grep -Fq 'Activity: io.github.lucifron28.unipm/.MainActivity'; then
    echo "activity_start_status=timeout; expected activity reported"
  else
    echo "Expected MainActivity to be reported by am start" >&2
    exit 1
  fi

  echo "Verifying application process"
  app_pid=""
  for attempt in $(seq 1 10); do
    app_pid=$(timeout 10s adb shell pidof io.github.lucifron28.unipm) || true
    app_pid="${app_pid//$'\r'/}"
    if [[ -n "${app_pid}" ]]; then
      break
    fi
    sleep 2
  done
  test -n "${app_pid}"
  echo "app_process_id=${app_pid}"

  echo "Waiting for cold-start rendering"
  sleep 20
  echo "Capturing startup screenshot"
  timeout 30s adb shell screencap -p /sdcard/unipm-startup.png
  timeout 60s adb pull /sdcard/unipm-startup.png "artifacts/nonfunctional-android/startup-${api_level}.png"
  test -s "artifacts/nonfunctional-android/startup-${api_level}.png"
  echo "Running focused app configuration tests"
  timeout 10m flutter test --device-id emulator-5554 --no-pub --reporter expanded test/app_config_test.dart
} 2>&1 | tee "${log}"
