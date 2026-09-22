# UniPM Mobile Field-Worker Preventive-Maintenance Workflow Redesign Plan

## 1. Executive Summary & Goals

UniPM is a preventive-maintenance information system for a university General Services Department (GSD). This plan redesigns the Flutter mobile app from an administrative, desktop-style Form CRUD prototype into a field-worker-first preventive-maintenance experience for skilled workers and inspectors.

### Core Goals
1. **Field-Worker-First UX**:
   - Primary journey: Identify Asset $\to$ View PM Task $\to$ Start/Resume Inspection $\to$ Complete Inspection $\to$ View Department + Category + PmCycle Batch Progress $\to$ Submit Batch when all eligible assets are complete $\to$ Capture Department Head Acknowledgement.
   - Eliminate inspector-facing generic draft CRUD, manual draft creation buttons, manual header form filling, and building/period dropdowns.
   - Internal draft forms remain as backend persistence; batches are automatically derived or created using canonical key: `Department + Asset Category + PmCycle`.
2. **Multi-Modal Asset Identification**:
   - Scan QR (fastest path).
   - Enter Asset Code (damaged/unreadable QR fallback).
   - Search Assets (by code, building, department, location).
   - Resolve to asset detail and current PM task without duplicating the full asset registry locally.
3. **UniPM Institutional Branding**:
   - Replace default indigo theme with exact web UniPM palette (Primary `#570000`, Primary active `#7F1D1D`, Background `#F8F9FA`, Muted surface `#F3F4F5`, Input surface `#E1E3E4`, Text `#191C1D` / `#5A413D` / `#57534E`, Border `#E7E5E4`).
   - Bundle web `unipm-logo.png` into mobile assets and configure `pubspec.yaml`.
   - Update Android manifest and app title to `"UniPM"`.
   - Provide centralized `AppColors` and Material 3 `AppTheme`.
4. **Preserved Authoritative Domain & Validation Rules**:
   - Four GSD form specifications (Fire Extinguisher, Fire Alarm, Emergency Light, Water Drinking Station) with exact checklist work items.
   - Lifecycle separation: Field work execution (`Due/Ongoing/Overdue -> Completed`) vs PM Form Batch (`Draft -> Submitted (Awaiting acknowledgement) -> Acknowledged`).
   - Department Head does not have a UniPM account; skilled worker's authenticated session captures signatory details, signature canvas image, and SHA-256 hash.
   - Official maintenance history remains strictly acknowledged-only.
   - Working authorization preserved without privilege broadening.

---

## 2. Backend & API Additions

### 2.1 Asset Code Exact Lookup
- **Endpoint**: `GET /api/v1/assets/by-code/{assetCode}`
- **Behavior**:
  - Normalizes `assetCode` using `AssetCodeValue.Normalize(assetCode)`.
  - Queries `IX_Assets_AssetCode` on `ApplicationDbContext.Assets`.
  - Returns `200 OK` with `AssetResponse` or `404 Not Found` with problem details.
- **Authorization**: Open to all authenticated users (same as `GET /assets/{id}` and `GET /assets/by-qr/{qrCodeValue}`).

### 2.2 Asset Text & Metadata Search
- **Endpoint**: Modify existing `GET /api/v1/assets`
- **Parameters**: Add optional `search` (string) and `limit` (int, default 25, clamped 1..50).
- **Behavior**:
  - If `search` is provided, filters case-insensitively across `AssetCode`, `Building`, `Department`, and `Location`.
  - Orders by `AssetCode` and applies `.Take(maxResults)`.
  - Fully backward compatible with existing category/status/building/department filters.

---

## 3. Mobile Architecture & Reusable Component Plan

### 3.1 Design System & Tokens
- **`mobile/lib/ui/app_colors.dart`**:
  - Static constants for all primary, background, surface, text, border, and status colors.
  - Semantic status pill background/border/text tuples for Draft, Submitted, Acknowledged, Due, and Ongoing.
- **`mobile/lib/ui/app_theme.dart`**:
  - Material 3 `ThemeData.lightTheme` with `ColorScheme.fromSeed(seedColor: AppColors.primary, ...)`.
  - Custom `AppBarTheme`, `CardTheme`, `FilledButtonThemeData`, `OutlinedButtonThemeData`, `InputDecorationTheme`, `ChipThemeData`, and `NavigationBarThemeData`.
- **`mobile/lib/ui/widgets/`**:
  - `unipm_brand_mark.dart`: UniPM wordmark with official shield logo.
  - `status_badge.dart`: Semantic status badges (Draft, Awaiting acknowledgement, Acknowledged, Due, Operational, etc.).
  - `batch_pm_card.dart`: Department + Category + Cycle batch card with progress bar.
  - `pm_progress_indicator.dart`: Visual completion bar and numeric compliance fraction.
  - `asset_summary_card.dart`: Clean asset metadata summary.

### 3.2 Asset Entry & Lookup
- **`mobile/lib/features/assets/asset_repository.dart`**:
  - Add `Future<Asset> getByCode(String assetCode)`.
  - Add `Future<List<Asset>> searchAssets({required String query, String? assetCategory, int limit = 20})`.
- **`mobile/lib/features/assets/asset_lookup_controller.dart`**:
  - Unified controller supporting QR scan value, manual asset code, and debounced text search.
- **`mobile/lib/features/assets/asset_search_delegate.dart` or `asset_search_page.dart`**:
  - Search UI with live suggestions, recent searches, category filters, and asset result tiles.
- **Manual Code Entry Dialog**:
  - Accessible from Home and QR Scanner screen ("Can't scan? Enter code manually").

### 3.3 Preventive Maintenance Batch & Field Workflow
- **Models (`mobile/lib/features/preventive_maintenance/preventive_maintenance_models.dart`)**:
  - Add `pmCycle` property to `PreventiveMaintenanceForm` and `ScheduleOption`.
  - Refactor `PreventiveMaintenanceGrouping` to group strictly by `Department + AssetCategory + PmCycle`.
  - Add batch progress calculation (`completedAssetCount / totalAssetCount`).
- **Controller (`mobile/lib/features/preventive_maintenance/preventive_maintenance_controller.dart`)**:
  - Auto-resolve or create matching batch draft for an asset's `Department + AssetCategory + PmCycle`.
  - Manage batch status and progress.
  - Guard batch submission: disable submit until all eligible assets in the batch have completed rows.
- **Screens**:
  - `mobile/lib/features/auth/home_page.dart`:
    - Field-work hub: Quick actions (Scan QR, Enter Code, Search Assets).
    - "My PM Tasks" / Active Batches: progress cards showing completion status.
    - "Awaiting Acknowledgement" section for submitted batches.
  - `mobile/lib/features/assets/asset_qr_lookup_page.dart`:
    - Display asset details, current PM cycle, schedule status.
    - Direct "Start Inspection" / "Resume Inspection" action.
    - Link to maintenance history.
  - `mobile/lib/features/preventive_maintenance/inspection_completion_sheet.dart`:
    - Modal bottom sheet displayed upon saving an inspection row.
    - Shows inspection recorded checkmark, updated batch progress (`X / Y completed`), "Next Asset" shortcut, and "View Batch" button.
  - `mobile/lib/features/preventive_maintenance/preventive_maintenance_acknowledgement_page.dart`:
    - Retained Department Head review and signature capture.

---

## 4. File Ownership & Implementation Strategy

To ensure zero conflicts between parallel execution tasks:
1. **Stream 1: Backend Asset Endpoints & Tests**
   - Files:
     - `server/Features/Assets/AssetsEndpoints.cs`
     - `tests/UniPm.Api.Tests/AssetReadEndpointsTests.cs` (or existing asset endpoint test)
   - Scope: Exact code lookup endpoint, search parameter on asset listing, API tests.
2. **Stream 2: Mobile Branding, Theme, Assets & Widgets**
   - Files:
     - `mobile/pubspec.yaml`
     - `mobile/assets/unipm-logo.png`
     - `mobile/android/app/src/main/AndroidManifest.xml`
     - `mobile/ios/Runner/Info.plist`
     - `mobile/lib/main.dart`
     - `mobile/lib/ui/app_colors.dart`
     - `mobile/lib/ui/app_theme.dart`
     - `mobile/lib/ui/display_labels.dart`
     - `mobile/lib/ui/widgets/` (all new widgets)
   - Scope: Brand tokens, theme configuration, logo asset, app title, reusable badges/cards.
3. **Stream 3: Mobile Field Workflow, Asset Entry & Screens**
   - Files:
     - `mobile/lib/features/assets/asset_repository.dart`
     - `mobile/lib/features/assets/asset_models.dart`
     - `mobile/lib/features/assets/asset_lookup_controller.dart`
     - `mobile/lib/features/assets/asset_search_page.dart`
     - `mobile/lib/features/assets/asset_qr_lookup_page.dart`
     - `mobile/lib/features/qr_scanner/qr_scanner_page.dart`
     - `mobile/lib/features/preventive_maintenance/preventive_maintenance_models.dart`
     - `mobile/lib/features/preventive_maintenance/preventive_maintenance_controller.dart`
     - `mobile/lib/features/preventive_maintenance/scanned_asset_pm_entry.dart`
     - `mobile/lib/features/preventive_maintenance/preventive_maintenance_page.dart`
     - `mobile/lib/features/preventive_maintenance/inspection_completion_sheet.dart`
     - `mobile/lib/features/auth/home_page.dart`
     - `mobile/lib/features/auth/authenticated_shell.dart`
     - `mobile/test/` (updating/adding tests)
   - Scope: Complete field-worker PM workflow, batch derivation, inspection completion sheet, asset search and code entry, and test suites.

---

## 5. Verification Plan

1. **Backend Verification**:
   - Run `dotnet test --filter FullyQualifiedName~Asset` to confirm new endpoints and search functionality.
2. **Mobile Test Verification**:
   - Run focused Flutter unit and widget tests:
     - `asset_qr_lookup_test.dart`
     - `mobile_field_workflow_ux_test.dart`
     - `preventive_maintenance_form_draft_test.dart` (updated for auto-batch and form presentation)
     - `preventive_maintenance_form_acknowledgement_test.dart`
     - New tests: manual asset-code lookup test, asset-search test, batch progress test.
3. **Flutter Static Analysis**:
   - Run `flutter analyze` from `mobile/`. Ensure 0 errors and 0 warnings.
4. **Android Debug Build**:
   - Run `flutter build apk --debug` from `mobile/`. Ensure clean compilation.
5. **Physical Device Smoke-Test Checklist**:
   - Document step-by-step verification flows for both QR scanning and damaged-QR manual entry/search.
