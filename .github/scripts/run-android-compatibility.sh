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
device_serial="$(printenv ANDROID_SERIAL || true)"
if [[ -z "$device_serial" ]]; then device_serial="emulator-5554"; fi
export ANDROID_SERIAL="$device_serial"
adb_readiness_status="not_executed"
adb_recovery_attempts=0
apk_build_status="not_executed"
apk_install_status="not_executed"
export RUN_ADB_READINESS_STATUS="$adb_readiness_status"
export RUN_ADB_RECOVERY_ATTEMPTS="$adb_recovery_attempts"
export RUN_ADB_DEVICE_SERIAL="$device_serial"
export RUN_APK_BUILD_STATUS="$apk_build_status"
export RUN_APK_INSTALL_STATUS="$apk_install_status"
device_release=""
device_api=""
device_model=""
screen_size=""
screen_density=""
actual_viewport_dp=""
expected_viewport_dp=""

wait_for_android_device() {
  local context="$1"
  local attempt wait_rc state boot observed_devices
  echo "Checking bounded ADB readiness: $context"
  for attempt in $(seq 1 6); do
    {
      echo "attempt=$attempt serial=$device_serial"
      wait_rc=0
      timeout 5s adb -s "$device_serial" wait-for-device >> "$evidence_dir/adb-readiness.log" 2>&1 || wait_rc=$?
      echo "wait_for_device_exit=$wait_rc"
      state="$(timeout 5s adb -s "$device_serial" get-state 2>&1 || true)"
      echo "adb_state=$state"
      if [[ "$state" == "device" ]]; then
        boot="$(timeout 5s adb -s "$device_serial" shell getprop sys.boot_completed 2>&1 | tr -d '\r' || true)"
        echo "sys_boot_completed=$boot"
        if [[ "$boot" == "1" ]]; then
          adb_readiness_status="passed"
          export RUN_ADB_READINESS_STATUS="$adb_readiness_status"
          export RUN_ADB_RECOVERY_ATTEMPTS="$adb_recovery_attempts"
          export RUN_ADB_DEVICE_SERIAL="$device_serial"
          echo "adb_readiness=passed serial=$device_serial recovery_attempts=$adb_recovery_attempts"
          return 0
        fi
      fi
    } >> "$evidence_dir/adb-readiness.log" 2>&1

    if [[ "$attempt" == "2" || "$attempt" == "4" || "$attempt" == "6" ]]; then
      adb_recovery_attempts=$((adb_recovery_attempts + 1))
      {
        echo "bounded_recovery_attempt=$adb_recovery_attempts"
        timeout 8s adb kill-server
        timeout 8s adb start-server
        timeout 8s adb connect 127.0.0.1:5555
        timeout 8s adb devices -l
      } >> "$evidence_dir/adb-readiness.log" 2>&1 || true
      observed_devices="$(timeout 8s adb devices -l 2>&1 || true)"
      printf '%s\n' "$observed_devices" >> "$evidence_dir/adb-readiness.log"
      if grep -Eq '^127\.0\.0\.1:5555[[:space:]]+device([[:space:]]|$)' <<< "$observed_devices"; then
        device_serial="127.0.0.1:5555"
        export ANDROID_SERIAL="$device_serial"
        export RUN_ADB_DEVICE_SERIAL="$device_serial"
        echo "Using recovered ADB TCP transport $device_serial"
      fi
    fi
    if [[ "$attempt" -lt 6 ]]; then sleep 5; fi
  done
  adb_readiness_status="failed"
  export RUN_ADB_READINESS_STATUS="$adb_readiness_status"
  export RUN_ADB_RECOVERY_ATTEMPTS="$adb_recovery_attempts"
  export RUN_ADB_DEVICE_SERIAL="$device_serial"
  echo "ADB did not report a boot-complete Android device after six readiness checks and $adb_recovery_attempts bounded recovery attempts." >&2
  return 1
}
capture_diagnostics() {
  local exit_code="$1"
  trap - EXIT
  set +e
  if [[ "$exit_code" -ne 0 && "$integration_status" == "running" ]]; then
    integration_status="failed"
    export RUN_INTEGRATION_STATUS="$integration_status"
  fi

  echo "Collecting final Android and ADB diagnostics (script exit=$exit_code)"
  timeout 10s adb version > "$evidence_dir/adb-version.txt" 2>&1
  timeout 10s adb server-status > "$evidence_dir/adb-server-status.txt" 2>&1
  timeout 10s adb devices -l > "$evidence_dir/adb-devices.txt" 2>&1
  timeout 15s adb -s "$device_serial" get-state > "$evidence_dir/adb-selected-device-state.txt" 2>&1
  timeout 20s adb -s "$device_serial" shell dumpsys activity activities > "$evidence_dir/activity-activities.txt" 2>&1
  timeout 20s adb -s "$device_serial" shell dumpsys window windows > "$evidence_dir/window-state.txt" 2>&1
  timeout 20s adb -s "$device_serial" shell dumpsys cpuinfo > "$evidence_dir/cpuinfo.txt" 2>&1
  timeout 20s adb -s "$device_serial" shell dumpsys meminfo io.github.lucifron28.unipm > "$evidence_dir/app-meminfo.txt" 2>&1
  timeout 30s adb -s "$device_serial" logcat -b all -d -v threadtime > "$evidence_dir/logcat.txt" 2>&1
  timeout 15s adb -s "$device_serial" shell uiautomator dump /sdcard/unipm-final-window.xml > "$evidence_dir/uiautomator-final.log" 2>&1
  timeout 15s adb -s "$device_serial" pull /sdcard/unipm-final-window.xml "$evidence_dir/ui-final.xml" >> "$evidence_dir/uiautomator-final.log" 2>&1
  timeout 30s adb -s "$device_serial" exec-out screencap -p > "$evidence_dir/final-$api_level.png" 2> "$evidence_dir/final-screenshot.log"
  if [[ "$exit_code" -ne 0 ]]; then
    ADB_TRACE=all timeout 20s adb devices -l > "$evidence_dir/adb-transport-trace.txt" 2>&1
    ps -eo pid,ppid,stat,%cpu,%mem,rss,args | grep -E '[q]emu-system|[e]mulator' > "$evidence_dir/emulator-process-on-failure.txt" || true
    free -h > "$evidence_dir/host-memory-on-failure.txt" 2>&1
    df -h / > "$evidence_dir/host-disk-on-failure.txt" 2>&1
    timeout 20s sudo journalctl --dmesg --no-pager -n 200 > "$evidence_dir/host-kernel-tail.log" 2>&1 || true
  fi

  if [[ -n "$RUNNER_TEMP" && -f "$RUNNER_TEMP/unipm-android-api.log" ]]; then
    cp "$RUNNER_TEMP/unipm-android-api.log" "$evidence_dir/api-server.log"
  fi
  if [[ -f "$evidence_dir/api-server.log" ]]; then
    grep -E 'Request finished HTTP/1\.1 .*\/api\/v1\/' \
      "$evidence_dir/api-server.log" > "$evidence_dir/api-request-status.log" || true
    grep -E 'Request finished HTTP/1\.1 .*\/api\/v1\/assets' \
      "$evidence_dir/api-server.log" > "$evidence_dir/asset-api-status.log" || true
  fi
  if [[ "${RUN_FAILURE_STAGE:-}" == "asset-details" ]]; then
    if [[ ! -s "$evidence_dir/asset-details-failure.png" && -s "$evidence_dir/final-$api_level.png" ]]; then
      cp "$evidence_dir/final-$api_level.png" "$evidence_dir/asset-details-failure.png"
    fi
    if [[ ! -s "$evidence_dir/asset-details-failure-window.xml" && -s "$evidence_dir/ui-final.xml" ]]; then
      cp "$evidence_dir/ui-final.xml" "$evidence_dir/asset-details-failure-window.xml"
    fi
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
    "adb_readiness_outcome": os.environ.get("RUN_ADB_READINESS_STATUS"),
    "adb_recovery_attempts": int(os.environ.get("RUN_ADB_RECOVERY_ATTEMPTS", "0")),
    "adb_device_serial": os.environ.get("RUN_ADB_DEVICE_SERIAL"),
    "apk_build_outcome": os.environ.get("RUN_APK_BUILD_STATUS"),
    "apk_install_outcome": os.environ.get("RUN_APK_INSTALL_STATUS"),
    "am_start_result": os.environ.get("RUN_LAUNCH_STATUS"),
    "launcher_anr_recovery_attempts": int(os.environ.get("RUN_LAUNCHER_ANR_RECOVERY_ATTEMPTS", "0")),
    "visible_unipm_login_ui": os.environ.get("RUN_UI_VISIBLE") == "true",
    "startup_outcome": os.environ.get("RUN_STARTUP_STATUS"),
    "authenticated_smoke_outcome": os.environ.get("RUN_INTEGRATION_STATUS"),
    "failure_stage": os.environ.get("RUN_FAILURE_STAGE"),
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
ps -eo pid,ppid,stat,%cpu,%mem,rss,args | grep -E '[q]emu-system|[e]mulator.*(avd|accel)' > "$evidence_dir/emulator-process.txt" || true

wait_for_android_device "initial emulator boot"
device_release="$(timeout 15s adb -s "$device_serial" shell getprop ro.build.version.release | tr -d '\r')"
device_api="$(timeout 15s adb -s "$device_serial" shell getprop ro.build.version.sdk | tr -d '\r')"
device_model="$(timeout 15s adb -s "$device_serial" shell getprop ro.product.model | tr -d '\r')"
screen_size="$(timeout 15s adb -s "$device_serial" shell wm size | tr -d '\r')"
screen_density="$(timeout 15s adb -s "$device_serial" shell wm density | tr -d '\r')"
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
if [[ ! -s "$apk_path" ]]; then
  apk_build_status="failed"
  export RUN_APK_BUILD_STATUS="$apk_build_status"
  echo "The pre-emulator UniPM APK build did not produce a non-empty APK." >&2
  exit 1
fi
apk_build_status="passed"
export RUN_APK_BUILD_STATUS="$apk_build_status"
echo "APK build passed before emulator startup."

echo "Verifying emulator and ADB readiness before installation."
wait_for_android_device "before APK installation"

echo "Installing UniPM APK"
apk_install_status="running"
export RUN_APK_INSTALL_STATUS="$apk_install_status"
install_succeeded="false"
for install_attempt in 1 2; do
  install_exit_code=0
  timeout 8m adb -s "$device_serial" install -r "$apk_path" > "$evidence_dir/install-attempt-$install_attempt.log" 2>&1 || install_exit_code=$?
  cat "$evidence_dir/install-attempt-$install_attempt.log"
  if [[ "$install_exit_code" -eq 0 ]]; then
    install_succeeded="true"
    apk_install_status="passed"
    export RUN_APK_INSTALL_STATUS="$apk_install_status"
    break
  fi
  if [[ "$install_attempt" == "1" ]]; then
    echo "APK install attempt 1 failed with exit $install_exit_code; trying one bounded ADB recovery." >&2
    if wait_for_android_device "after first install failure"; then continue; fi
  fi
  apk_install_status="failed"
  export RUN_APK_INSTALL_STATUS="$apk_install_status"
  exit "$install_exit_code"
done
if [[ "$install_succeeded" != "true" ]]; then
  apk_install_status="failed"
  export RUN_APK_INSTALL_STATUS="$apk_install_status"
  echo "UniPM APK installation did not succeed." >&2
  exit 1
fi
wait_for_android_device "after APK installation"
timeout 15s adb -s "$device_serial" shell pm path io.github.lucifron28.unipm
application_pid="$(timeout 15s adb -s "$device_serial" shell pidof io.github.lucifron28.unipm 2>/dev/null | tr -d '\r' || true)"
if [[ -z "$application_pid" ]]; then application_pid="not_running"; fi
echo "pre_launch_process_id=$application_pid"

wait_for_android_device "before MainActivity launch"
timeout 15s adb -s "$device_serial" logcat -c
timeout 15s adb -s "$device_serial" shell input keyevent 82 || true
echo "Launching UniPM MainActivity"
set +e
launch_output="$(timeout 120s adb shell am start -W -n io.github.lucifron28.unipm/.MainActivity 2>&1)"
launch_rc=$?
set -e
printf '%s\n' "$launch_output" | tee "$evidence_dir/activity-start.txt"
if printf '%s\n' "$launch_output" | grep -Fq 'Status: ok'; then
  launch_command_status="status_ok"
elif [[ "$launch_rc" -eq 124 ]] || printf '%s\n' "$launch_output" | grep -Fq 'Status: timeout'; then
  launch_command_status="timeout_waiting_for_activity_manager"
else
  launch_command_status="exit_$launch_rc"
fi
export RUN_LAUNCH_STATUS="$launch_command_status"
echo "am_start_result=$launch_command_status"

echo "Waiting for the actual UniPM login interface in the foreground window hierarchy"
ui_visible=false
launcher_anr_recovery_attempts=0
export RUN_LAUNCHER_ANR_RECOVERY_ATTEMPTS="$launcher_anr_recovery_attempts"
for attempt in $(seq 1 20); do
  if ! timeout 8s adb -s "$device_serial" get-state >/dev/null 2>&1; then
    wait_for_android_device "login UI capture attempt $attempt"
  fi
  rm -f "$evidence_dir/ui-login.xml"
  if timeout 12s adb -s "$device_serial" shell uiautomator dump /sdcard/unipm-login-window.xml \
      > "$evidence_dir/uiautomator-startup.log" 2>&1; then
    timeout 15s adb -s "$device_serial" pull /sdcard/unipm-login-window.xml "$evidence_dir/ui-login.xml" \
      >> "$evidence_dir/uiautomator-startup.log" 2>&1 || true
  fi

  timeout 30s adb -s "$device_serial" exec-out screencap -p > "$evidence_dir/startup-login.png" \
    2> "$evidence_dir/startup-screenshot.log" || true

  if [[ -s "$evidence_dir/ui-login.xml" ]] \
      && grep -Fq "Pixel Launcher isn't responding" "$evidence_dir/ui-login.xml" \
      && [[ "$launcher_anr_recovery_attempts" -lt 1 ]]; then
    launcher_anr_recovery_attempts=$((launcher_anr_recovery_attempts + 1))
    export RUN_LAUNCHER_ANR_RECOVERY_ATTEMPTS="$launcher_anr_recovery_attempts"
    launcher_process_id="$(timeout 10s adb -s "$device_serial" shell pidof io.github.lucifron28.unipm 2>/dev/null | tr -d '\r' || true)"
    wait_center="$(python3 - "$evidence_dir/ui-login.xml" <<'PY'
import re
import sys
import xml.etree.ElementTree as ET

root = ET.parse(sys.argv[1]).getroot()
for node in root.iter("node"):
    if node.attrib.get("resource-id") != "android:id/aerr_wait":
        continue
    bounds = re.fullmatch(r"\[(\d+),(\d+)\]\[(\d+),(\d+)\]", node.attrib.get("bounds", ""))
    if bounds:
        x1, y1, x2, y2 = (int(value) for value in bounds.groups())
        print((x1 + x2) // 2, (y1 + y2) // 2)
        break
PY
)"
    if [[ -n "$launcher_process_id" && -n "$wait_center" ]]; then
      cp "$evidence_dir/startup-login.png" "$evidence_dir/launcher-anr-before-wait.png"
      cp "$evidence_dir/ui-login.xml" "$evidence_dir/launcher-anr-before-wait-window.xml"
      read -r wait_x wait_y <<< "$wait_center"
      echo "Pixel Launcher ANR is covering UniPM startup while the app process remains alive; tapping Android Wait at $wait_x,$wait_y."
      timeout 10s adb -s "$device_serial" shell input tap "$wait_x" "$wait_y" \
        > "$evidence_dir/launcher-anr-wait-action.log" 2>&1 || true
      sleep 3
      continue
    fi
    echo "Pixel Launcher ANR was detected, but its Wait control or the UniPM process was unavailable; retaining the startup failure." >&2
  fi

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
  if [[ -s "$evidence_dir/ui-login.xml" ]] && grep -Fq "Pixel Launcher isn't responding" "$evidence_dir/ui-login.xml"; then
    export RUN_FAILURE_STAGE="android-launcher-anr"
    cp "$evidence_dir/startup-login.png" "$evidence_dir/launcher-anr-failure.png"
    cp "$evidence_dir/ui-login.xml" "$evidence_dir/launcher-anr-failure-window.xml"
  fi
  echo "UniPM login controls were not verified in a responsive foreground UI; process presence and screenshot bytes are insufficient." >&2
  exit 1
fi
startup_status="passed"
export RUN_STARTUP_STATUS="$startup_status"
export RUN_UI_VISIBLE="$ui_visible"
application_pid="$(timeout 15s adb -s "$device_serial" shell pidof io.github.lucifron28.unipm | tr -d '\r')"
echo "startup_status=$startup_status"
echo "visible_login_controls=UniPM, Sign in, Use your UniPM field-work account."
echo "app_process_id=$application_pid"

echo "Running supplementary focused Flutter configuration tests on the emulator"
wait_for_android_device "before Flutter configuration tests"
timeout 10m flutter test \
  --device-id "$device_serial" \
  --no-pub \
  --reporter expanded \
  test/app_config_test.dart > "$evidence_dir/config-tests-$api_level.log" 2>&1
configuration_test_status="passed"
export RUN_CONFIG_TEST_STATUS="$configuration_test_status"
cat "$evidence_dir/config-tests-$api_level.log"

echo "Running authenticated, server-backed Flutter integration smoke"
wait_for_android_device "before authenticated integration smoke"
integration_status="running"
export RUN_INTEGRATION_STATUS="$integration_status"
integration_raw_log="$RUNNER_TEMP/unipm-android-integration-api-$api_level.raw.log"
set +e
timeout 30m flutter test \
  --no-pub \
  --reporter expanded \
  --device-id "$device_serial" \
  --dart-define=UNIPM_API_BASE_URL=http://10.0.2.2:5254/ \
  --dart-define=UNIPM_SMOKE_PASSWORD="$UNIPM_DEV_USER_PASSWORD" \
  --dart-define=UNIPM_SMOKE_API="$api_level" \
  integration_test/nonfunctional_smoke_test.dart \
  > "$integration_raw_log" 2>&1 &
integration_test_pid=$!
captured_failure_stage=""
while true; do
  capture_stage=""
  if [[ -s "$integration_raw_log" ]]; then
    if grep -Fq 'SMOKE_CAPTURE_SCREEN=dashboard-search' "$integration_raw_log"; then
      capture_stage="dashboard-search"
    elif grep -Fq 'SMOKE_CAPTURE_SCREEN=asset-details' "$integration_raw_log"; then
      capture_stage="asset-details"
    fi
  fi
  if [[ -n "$capture_stage" ]]; then
    failure_screenshot="$evidence_dir/$capture_stage-failure.png"
    failure_window="$evidence_dir/$capture_stage-failure-window.xml"
    echo "Capturing Android screen and window hierarchy for integration failure stage $capture_stage."
    timeout 30s adb -s "$device_serial" exec-out screencap -p \
      > "$failure_screenshot" 2> "$evidence_dir/$capture_stage-failure-screenshot.log" || true
    if [[ -s "$failure_screenshot" ]]; then
      timeout 15s adb -s "$device_serial" shell uiautomator dump "/sdcard/unipm-$capture_stage-failure.xml" \
        > "$evidence_dir/$capture_stage-failure-uiautomator.log" 2>&1 || true
      timeout 15s adb -s "$device_serial" pull "/sdcard/unipm-$capture_stage-failure.xml" "$failure_window" \
        >> "$evidence_dir/$capture_stage-failure-uiautomator.log" 2>&1 || true
      captured_failure_stage="$capture_stage"
      break
    fi
  fi
  if ! kill -0 "$integration_test_pid" 2>/dev/null; then
    break
  fi
  sleep 0.25
done
wait "$integration_test_pid"
drive_rc=$?
set -e
if [[ -n "$captured_failure_stage" ]]; then
  export RUN_FAILURE_STAGE="$captured_failure_stage"
fi
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
  if grep -Fq 'SMOKE_FAILURE stage=asset-details' "$evidence_dir/integration-$api_level.log"; then
    export RUN_FAILURE_STAGE="asset-details"
  elif grep -Fq 'SMOKE_FAILURE stage=dashboard-search' "$evidence_dir/integration-$api_level.log"; then
    export RUN_FAILURE_STAGE="dashboard-search"
  fi
  exit "$drive_rc"
fi
integration_status="passed"
export RUN_INTEGRATION_STATUS="$integration_status"

echo "Smoke and supplementary checks completed for API $api_level."
