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

  screen_ready=0
  for attempt in $(seq 1 60); do
    adb shell uiautomator dump /sdcard/unipm-window.xml >/dev/null 2>&1 || true
    if adb shell cat /sdcard/unipm-window.xml | grep -q 'Sign in'; then
      screen_ready=1
      break
    fi
    sleep 1
  done
  test "${screen_ready}" = "1"

  adb shell dumpsys activity activities | grep -E 'mResumedActivity|topResumedActivity'
  adb pull /sdcard/unipm-window.xml "artifacts/nonfunctional-android/startup-${api_level}.xml"
  adb shell screencap -p /sdcard/unipm-startup.png
  adb pull /sdcard/unipm-startup.png "artifacts/nonfunctional-android/startup-${api_level}.png"
  flutter test --device-id emulator-5554 --no-pub --reporter expanded
} 2>&1 | tee "${log}"
