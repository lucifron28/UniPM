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
  adb shell getprop ro.build.version.release
  adb shell getprop ro.build.version.sdk
  adb shell wm size
  adb shell wm density
  flutter build apk --debug --dart-define=UNIPM_API_BASE_URL=http://10.0.2.2:5254/
  adb install -r build/app/outputs/flutter-apk/app-debug.apk
  adb shell monkey -p io.github.lucifron28.unipm 1

  activity_ready=0
  for attempt in $(seq 1 30); do
    activity=$(adb shell dumpsys activity activities | grep -E 'mResumedActivity|topResumedActivity' || true)
    if printf '%s\n' "${activity}" | grep -q 'io.github.lucifron28.unipm/.MainActivity'; then
      activity_ready=1
      break
    fi
    sleep 1
  done
  test "${activity_ready}" = "1"
  printf '%s\n' "${activity}"

  sleep 5
  adb shell screencap -p /sdcard/unipm-startup.png
  adb pull /sdcard/unipm-startup.png "artifacts/nonfunctional-android/startup-${api_level}.png"
  test -s "artifacts/nonfunctional-android/startup-${api_level}.png"
  timeout 10m flutter test --device-id emulator-5554 --no-pub --reporter expanded test/app_config_test.dart
} 2>&1 | tee "${log}"
