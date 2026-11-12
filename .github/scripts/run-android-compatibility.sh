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
  echo "device_release=$(adb shell getprop ro.build.version.release)"
  echo "device_api=$(adb shell getprop ro.build.version.sdk)"
  echo "screen_size=$(adb shell wm size)"
  echo "density_dpi=$(adb shell getprop qemu.sf.lcd_density)"
  flutter build apk --debug --dart-define=UNIPM_API_BASE_URL=http://10.0.2.2:5254/
  adb install -r build/app/outputs/flutter-apk/app-debug.apk
  launch_output=$(timeout 90s adb shell am start -W -n io.github.lucifron28.unipm/.MainActivity)
  printf '%s\n' "${launch_output}"
  printf '%s\n' "${launch_output}" | grep -q 'Status: ok'

  sleep 5
  adb shell screencap -p /sdcard/unipm-startup.png
  adb pull /sdcard/unipm-startup.png "artifacts/nonfunctional-android/startup-${api_level}.png"
  test -s "artifacts/nonfunctional-android/startup-${api_level}.png"
  timeout 10m flutter test --device-id emulator-5554 --no-pub --reporter expanded test/app_config_test.dart
} 2>&1 | tee "${log}"
