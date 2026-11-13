#!/usr/bin/env bash
set -Eeuo pipefail

android_version="$1"
api_level="$2"
profile="$3"
screen_class="$4"

cd mobile
evidence_dir="artifacts/nonfunctional-android/api-$api_level"
mkdir -p "$evidence_dir"
run_log="$evidence_dir/run-$api_level.log"
exec > >(tee -a "$run_log") 2>&1

startup_status="not_executed"
integration_status="not_executed"
configuration_test_status="not_executed"
screen_status="not_checked"
launch_command_status="not_executed"
ui_visible="false"
application_pid=""
device_release=""
device_api=""
device_model=""
screen_size=""
screen_density=""
actual_viewport_dp=""
expected_viewport_dp=""

capture_diagnostics() {
  local exit_code="$1"
  trap - EXIT
  set +e
  if [[ "$exit_code" -ne 0 && "$integration_status" == "running" ]]; then
    integration_status="failed"
    export RUN_INTEGRATION_STATUS="$integration_status"
  fi

  echo "Collecting final Android diagnostics (script exit=$exit_code)"
  timeout 30s adb devices -l > "$evidence_dir/adb-devices.txt" 2>&1
  timeout 30s adb shell dumpsys activity activities > "$evidence_dir/activity-activities.txt" 2>&1
  timeout 30s adb shell dumpsys window windows > "$evidence_dir/window-state.txt" 2>&1
  timeout 30s adb shell dumpsys cpuinfo > "$evidence_dir/cpuinfo.txt" 2>&1
  timeout 30s adb shell dumpsys meminfo io.github.lucifron28.unipm > "$evidence_dir/app-meminfo.txt" 2>&1
  timeout 45s adb logcat -b all -d -v threadtime > "$evidence_dir/logcat.txt" 2>&1
  timeout 20s adb shell uiautomator dump /sdcard/unipm-final-window.xml > "$evidence_dir/uiautomator-final.log" 2>&1
  timeout 20s adb pull /sdcard/unipm-final-window.xml "$evidence_dir/ui-final.xml" >> "$evidence_dir/uiautomator-final.log" 2>&1
  timeout 60s adb exec-out screencap -p > "$evidence_dir/final-$api_level.png" 2> "$evidence_dir/final-screenshot.log"

  if [[ -n "$RUNNER_TEMP" && -f "$RUNNER_TEMP/unipm-android-api.log" ]]; then
    cp "$RUNNER_TEMP/unipm-android-api.log" "$evidence_dir/api-server.log"
  fi
  RUN_EXIT_CODE="$exit_code" python3 - "$evidence_dir/summary.json" <<'PY'
import json
import os
import sys

summary = {
    "android_version": os.environ.get("RUN_ANDROID_VERSION"),
    "api_level": int(os.environ.get("RUN_API_LEVEL", "0")),
    "profile": os.environ.get("RUN_PROFILE"),
    "screen_class": os.environ.get("RUN_SCREEN_CLASS"),
    "device_release": os.environ.get("RUN_DEVICE_RELEASE"),
    "device_api": os.environ.get("RUN_DEVICE_API"),
    "device_model": os.environ.get("RUN_DEVICE_MODEL"),
    "screen_size": os.environ.get("RUN_SCREEN_SIZE"),
    "screen_density": os.environ.get("RUN_SCREEN_DENSITY"),
    "logical_viewport_dp": os.environ.get("RUN_VIEWPORT_DP"),
    "expected_viewport_dp": os.environ.get("RUN_EXPECTED_VIEWPORT_DP"),
    "screen_profile_check": os.environ.get("RUN_SCREEN_STATUS"),
    "am_start_result": os.environ.get("RUN_LAUNCH_STATUS"),
    "visible_unipm_login_ui": os.environ.get("RUN_UI_VISIBLE") == "true",
    "startup_outcome": os.environ.get("RUN_STARTUP_STATUS"),
    "authenticated_smoke_outcome": os.environ.get("RUN_INTEGRATION_STATUS"),
    "flutter_configuration_test_outcome": os.environ.get("RUN_CONFIG_TEST_STATUS"),
    "workflow_script_exit_code": int(os.environ.get("RUN_EXIT_CODE", "1")),
    "tested_commit": os.environ.get("GITHUB_SHA"),
    "workflow_run_id": os.environ.get("GITHUB_RUN_ID"),
    "workflow_run_attempt": os.environ.get("GITHUB_RUN_ATTEMPT"),
}
with open(sys.argv[1], "w", encoding="utf-8") as output:
    json.dump(summary, output, indent=2)
    output.write("\n")
PY
  echo "Evidence saved in $evidence_dir"
  exit "$exit_code"
}

export RUN_ANDROID_VERSION="$android_version"
export RUN_API_LEVEL="$api_level"
export RUN_PROFILE="$profile"
export RUN_SCREEN_CLASS="$screen_class"
trap 'capture_diagnostics $?' EXIT

echo "Android compatibility run"
echo "android_version=$android_version"
echo "api_level=$api_level"
echo "profile=$profile"
echo "screen_class=$screen_class"
echo "tested_commit=$GITHUB_SHA"
echo "workflow_run_id=$GITHUB_RUN_ID"
echo "flutter_version=$(flutter --version | head -n 1)"
echo "host_kernel=$(uname -a)"
echo "host_cpu_count=$(nproc)"
free -h > "$evidence_dir/host-memory.txt"
df -h / > "$evidence_dir/host-disk.txt"
if [[ -e /dev/kvm ]]; then
  echo "host_kvm_device=$(stat -c '%A %U:%G %n' /dev/kvm)"
  echo "host_kvm_read_write=$([[ -r /dev/kvm && -w /dev/kvm ]] && echo true || echo false)"
else
  echo "host_kvm_device=not_available"
fi
sdk="$(printenv ANDROID_SDK_ROOT || true)"
if [[ -z "$sdk" ]]; then sdk="$(printenv ANDROID_HOME || true)"; fi
if [[ -n "$sdk" && -x "$sdk/emulator/emulator" ]]; then
  "$sdk/emulator/emulator" -accel-check > "$evidence_dir/emulator-accel-check.txt" 2>&1 || true
  cat "$evidence_dir/emulator-accel-check.txt"
fi
ps -eo pid,args | grep -E '[e]mulator.*(avd|accel)' > "$evidence_dir/emulator-process.txt" || true

device_release="$(timeout 30s adb shell getprop ro.build.version.release | tr -d '\r')"
device_api="$(timeout 30s adb shell getprop ro.build.version.sdk | tr -d '\r')"
device_model="$(timeout 30s adb shell getprop ro.product.model | tr -d '\r')"
screen_size="$(timeout 30s adb shell wm size | tr -d '\r')"
screen_density="$(timeout 30s adb shell wm density | tr -d '\r')"
export RUN_DEVICE_RELEASE="$device_release"
export RUN_DEVICE_API="$device_api"
export RUN_DEVICE_MODEL="$device_model"
export RUN_SCREEN_SIZE="$screen_size"
export RUN_SCREEN_DENSITY="$screen_density"
printf 'device_release=%s\ndevice_api=%s\ndevice_model=%s\nscreen_size=%s\nscreen_density=%s\n' \
  "$device_release" "$device_api" "$device_model" "$screen_size" "$screen_density"

if [[ "$api_level" == "29" ]]; then
  expected_viewport_dp="360x640"
elif [[ "$api_level" == "34" ]]; then
  expected_viewport_dp="411x891"
else
  echo "Unexpected API level $api_level" >&2
  exit 1
fi
viewport_values="$(python3 - "$screen_size" "$screen_density" <<'PY'
import re
import sys

size_match = re.findall(r"(?:Physical|Override) size:\s*(\d+)x(\d+)", sys.argv[1])
density_match = re.findall(r"(?:Physical|Override) density:\s*(\d+)", sys.argv[2])
if not size_match or not density_match:
    raise SystemExit("Could not parse emulator pixel dimensions and density.")
width_px, height_px = map(int, size_match[-1])
density_dpi = int(density_match[-1])
print(f"{round(width_px * 160 / density_dpi)}x{round(height_px * 160 / density_dpi)}")
PY
)"
actual_viewport_dp="$viewport_values"
export RUN_VIEWPORT_DP="$actual_viewport_dp"
export RUN_EXPECTED_VIEWPORT_DP="$expected_viewport_dp"
if [[ "$actual_viewport_dp" != "$expected_viewport_dp" ]]; then
  screen_status="failed"
  export RUN_SCREEN_STATUS="$screen_status"
  echo "Expected $expected_viewport_dp dp viewport, observed $actual_viewport_dp dp." >&2
  exit 1
fi
screen_status="passed"
export RUN_SCREEN_STATUS="$screen_status"
echo "logical_viewport_dp=$actual_viewport_dp"

if [[ "$device_api" != "$api_level" || "$device_release" != "$android_version" ]]; then
  echo "Emulator Android release/API did not match the requested matrix row." >&2
  exit 1
fi

apk_path="build/app/outputs/flutter-apk/app-debug.apk"
echo "Building the actual UniPM debug APK with the local synthetic API base address"
timeout 15m flutter build apk \
  --debug \
  --target-platform android-x64 \
  --dart-define=UNIPM_API_BASE_URL=http://10.0.2.2:5254/
test -s "$apk_path"

echo "Installing UniPM APK"
timeout 8m adb install -r "$apk_path"
timeout 30s adb shell pm path io.github.lucifron28.unipm
application_pid="$(timeout 30s adb shell pidof io.github.lucifron28.unipm 2>/dev/null | tr -d '\r' || true)"
if [[ -z "$application_pid" ]]; then application_pid="not_running"; fi
echo "pre_launch_process_id=$application_pid"

timeout 30s adb logcat -c
timeout 30s adb shell input keyevent 82 || true
echo "Launching UniPM MainActivity"
set +e
launch_output="$(timeout 120s adb shell am start -W -n io.github.lucifron28.unipm/.MainActivity 2>&1)"
launch_rc=$?
set -e
printf '%s\n' "$launch_output" | tee "$evidence_dir/activity-start.txt"
if [[ "$launch_rc" -eq 0 ]] && printf '%s\n' "$launch_output" | grep -Fq 'Status: ok'; then
  launch_command_status="status_ok"
elif [[ "$launch_rc" -eq 124 ]]; then
  launch_command_status="timeout_waiting_for_activity_manager"
else
  launch_command_status="exit_$launch_rc"
fi
export RUN_LAUNCH_STATUS="$launch_command_status"
echo "am_start_result=$launch_command_status"

echo "Waiting for the actual UniPM login interface in the foreground window hierarchy"
ui_visible=false
for attempt in $(seq 1 20); do
  rm -f "$evidence_dir/ui-login.xml"
  if timeout 12s adb shell uiautomator dump /sdcard/unipm-login-window.xml \
      > "$evidence_dir/uiautomator-startup.log" 2>&1; then
    timeout 15s adb pull /sdcard/unipm-login-window.xml "$evidence_dir/ui-login.xml" \
      >> "$evidence_dir/uiautomator-startup.log" 2>&1 || true
  fi

  timeout 30s adb exec-out screencap -p > "$evidence_dir/startup-login.png" \
    2> "$evidence_dir/startup-screenshot.log" || true

  if [[ -s "$evidence_dir/ui-login.xml" ]] \
      && grep -Fq 'Sign in' "$evidence_dir/ui-login.xml" \
      && grep -Fq 'Use your UniPM field-work account.' "$evidence_dir/ui-login.xml" \
      && grep -Fq 'UniPM' "$evidence_dir/ui-login.xml" \
      && ! grep -Eiq "isn.t responding|is not responding|Close app|Wait" "$evidence_dir/ui-login.xml" \
      && [[ -s "$evidence_dir/startup-login.png" ]]; then
    ui_visible=true
    break
  fi
  sleep 3
done
if [[ "$ui_visible" != true ]]; then
  startup_status="failed"
  export RUN_STARTUP_STATUS="$startup_status"
  export RUN_UI_VISIBLE="$ui_visible"
  echo "UniPM login controls were not verified in a responsive foreground UI; process presence and screenshot bytes are insufficient." >&2
  exit 1
fi
startup_status="passed"
export RUN_STARTUP_STATUS="$startup_status"
export RUN_UI_VISIBLE="$ui_visible"
application_pid="$(timeout 30s adb shell pidof io.github.lucifron28.unipm | tr -d '\r')"
echo "startup_status=$startup_status"
echo "visible_login_controls=UniPM, Sign in, Use your UniPM field-work account."
echo "app_process_id=$application_pid"

echo "Running supplementary focused Flutter configuration tests on the emulator"
timeout 10m flutter test \
  --device-id emulator-5554 \
  --no-pub \
  --reporter expanded \
  test/app_config_test.dart > "$evidence_dir/config-tests-$api_level.log" 2>&1
configuration_test_status="passed"
export RUN_CONFIG_TEST_STATUS="$configuration_test_status"
cat "$evidence_dir/config-tests-$api_level.log"

echo "Running authenticated, server-backed Flutter integration smoke"
integration_status="running"
export RUN_INTEGRATION_STATUS="$integration_status"
integration_raw_log="$RUNNER_TEMP/unipm-android-integration-api-$api_level.raw.log"
set +e
UNIPM_ANDROID_ARTIFACTS_DIR="$PWD/$evidence_dir" timeout 30m flutter drive \
  --driver=test_driver/nonfunctional_smoke_driver.dart \
  --target=integration_test/nonfunctional_smoke_test.dart \
  --device-id emulator-5554 \
  --dart-define=UNIPM_API_BASE_URL=http://10.0.2.2:5254/ \
  --dart-define=UNIPM_SMOKE_PASSWORD="$UNIPM_DEV_USER_PASSWORD" \
  --dart-define=UNIPM_SMOKE_API="$api_level" \
  > "$integration_raw_log" 2>&1
drive_rc=$?
set -e
python3 - "$integration_raw_log" "$evidence_dir/integration-$api_level.log" "$UNIPM_DEV_USER_PASSWORD" <<'PY'
from pathlib import Path
import sys

raw_path, safe_path, password = sys.argv[1:]
text = Path(raw_path).read_text(encoding="utf-8", errors="replace")
Path(safe_path).write_text(text.replace(password, "[REDACTED]"), encoding="utf-8")
PY
cat "$evidence_dir/integration-$api_level.log"
if [[ "$drive_rc" -ne 0 ]]; then
  integration_status="failed"
  export RUN_INTEGRATION_STATUS="$integration_status"
  exit "$drive_rc"
fi
integration_status="passed"
export RUN_INTEGRATION_STATUS="$integration_status"

echo "Smoke and supplementary checks completed for API $api_level."
