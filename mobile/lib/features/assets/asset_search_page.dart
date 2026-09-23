import 'dart:async';
import 'package:flutter/material.dart';

import '../../auth/auth_models.dart';
import '../../ui/app_colors.dart';
import '../../ui/widgets/asset_summary_card.dart';
import '../maintenance_history/asset_maintenance_history_repository.dart';
import '../preventive_maintenance/preventive_maintenance_repository.dart';
import 'asset_models.dart';
import 'asset_qr_lookup_page.dart';
import 'asset_repository.dart';

class AssetSearchPage extends StatefulWidget {
  const AssetSearchPage({
    super.key,
    required this.repository,
    this.preventiveMaintenanceRepository,
    this.assetMaintenanceHistoryRepository,
    this.user,
    this.initialQuery,
  });

  final AssetRepository repository;
  final PreventiveMaintenanceRepository? preventiveMaintenanceRepository;
  final AssetMaintenanceHistoryRepository? assetMaintenanceHistoryRepository;
  final AuthUser? user;
  final String? initialQuery;

  @override
  State<AssetSearchPage> createState() => _AssetSearchPageState();
}

class _AssetSearchPageState extends State<AssetSearchPage> {
  final _searchController = TextEditingController();
  Timer? _debounceTimer;
  int _searchToken = 0;

  bool _isLoading = false;
  String? _errorMessage;
  List<Asset> _results = [];
  String? _selectedCategory;

  static const _categories = [
    ('All', null),
    ('Fire Extinguisher', 'fire-extinguisher'),
    ('Fire Alarm', 'fire-alarm'),
    ('Emergency Light', 'emergency-light'),
    ('Water Station', 'water-drinking-station'),
  ];

  @override
  void initState() {
    super.initState();
    if (widget.initialQuery != null && widget.initialQuery!.isNotEmpty) {
      _searchController.text = widget.initialQuery!;
      _performSearch(widget.initialQuery!);
    } else {
      _performSearch('');
    }
  }

  @override
  void dispose() {
    _debounceTimer?.cancel();
    _searchController.dispose();
    super.dispose();
  }

  void _onQueryChanged(String query) {
    _debounceTimer?.cancel();
    _debounceTimer = Timer(const Duration(milliseconds: 300), () {
      _performSearch(query);
    });
  }

  Future<void> _performSearch(String query) async {
    final token = ++_searchToken;
    setState(() {
      _isLoading = true;
      _errorMessage = null;
    });

    try {
      final assets = await widget.repository.searchAssets(
        search: query.trim().isEmpty ? null : query.trim(),
        assetCategory: _selectedCategory,
        limit: 30,
      );
      if (!mounted || token != _searchToken) return;
      setState(() {
        _results = assets;
        _isLoading = false;
      });
    } catch (_) {
      if (!mounted || token != _searchToken) return;
      setState(() {
        _isLoading = false;
        _errorMessage = 'Could not search assets. Please try again.';
      });
    }
  }

  void _onAssetSelected(Asset asset) {
    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => AssetQrLookupPage(
          repository: widget.repository,
          scannedValue: asset.qrCodeValue,
          preventiveMaintenanceRepository:
              widget.preventiveMaintenanceRepository,
          assetMaintenanceHistoryRepository:
              widget.assetMaintenanceHistoryRepository,
          user: widget.user,
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Search Assets'),
      ),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 8, 16, 8),
            child: TextField(
              key: const Key('asset-search-input'),
              controller: _searchController,
              onChanged: _onQueryChanged,
              autofocus: false,
              decoration: InputDecoration(
                hintText: 'Search code, building, department, location...',
                prefixIcon: const Icon(Icons.search),
                suffixIcon: _searchController.text.isNotEmpty
                    ? IconButton(
                        icon: const Icon(Icons.clear),
                        onPressed: () {
                          _searchController.clear();
                          _performSearch('');
                        },
                      )
                    : null,
              ),
            ),
          ),
          SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
            child: Row(
              children: _categories.map((cat) {
                final isSelected = _selectedCategory == cat.$2;
                return Padding(
                  padding: const EdgeInsets.only(right: 8),
                  child: FilterChip(
                    label: Text(cat.$1),
                    selected: isSelected,
                    onSelected: (selected) {
                      setState(() {
                        _selectedCategory = selected ? cat.$2 : null;
                      });
                      _performSearch(_searchController.text);
                    },
                  ),
                );
              }).toList(),
            ),
          ),
          if (_isLoading)
            const LinearProgressIndicator(
              minHeight: 2,
              backgroundColor: Colors.transparent,
              valueColor: AlwaysStoppedAnimation<Color>(AppColors.primary),
            ),
          Expanded(
            child: _buildResultsList(),
          ),
        ],
      ),
    );
  }

  Widget _buildResultsList() {
    if (_errorMessage != null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(Icons.error_outline, size: 48, color: AppColors.error),
              const SizedBox(height: 12),
              Text(
                _errorMessage!,
                textAlign: TextAlign.center,
                style: const TextStyle(color: AppColors.error),
              ),
              const SizedBox(height: 16),
              FilledButton(
                onPressed: () => _performSearch(_searchController.text),
                child: const Text('Retry'),
              ),
            ],
          ),
        ),
      );
    }

    if (!_isLoading && _results.isEmpty) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(32),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(
                Icons.inventory_2_outlined,
                size: 56,
                color: AppColors.textNeutral,
              ),
              const SizedBox(height: 16),
              const Text(
                'No assets match your search',
                style: TextStyle(
                  fontSize: 16,
                  fontWeight: FontWeight.w700,
                  color: AppColors.textPrimary,
                ),
              ),
              const SizedBox(height: 8),
              const Text(
                'Try searching by asset code, building, department, or specific room.',
                textAlign: TextAlign.center,
                style: TextStyle(
                  fontSize: 14,
                  color: AppColors.textNeutral,
                ),
              ),
            ],
          ),
        ),
      );
    }

    return ListView.separated(
      padding: const EdgeInsets.all(16),
      itemCount: _results.length,
      separatorBuilder: (context, index) => const SizedBox(height: 12),
      itemBuilder: (context, index) {
        final asset = _results[index];
        return AssetSummaryCard(
          assetCode: asset.assetCode,
          assetCategory: asset.assetCategory,
          building: asset.building,
          department: asset.department,
          location: asset.location,
          status: asset.status,
          onTap: () => _onAssetSelected(asset),
        );
      },
    );
  }
}
