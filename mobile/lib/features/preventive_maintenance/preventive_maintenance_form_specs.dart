class PreventiveMaintenanceFormSpec {
  const PreventiveMaintenanceFormSpec({
    required this.assetCategory,
    required this.documentTitle,
    required this.revision,
    required this.effectivityDate,
    required this.assetNumberLabel,
    this.waterWorkItems = const <String>[],
  });

  final String assetCategory;
  final String documentTitle;
  final String revision;
  final String effectivityDate;
  final String assetNumberLabel;
  final List<String> waterWorkItems;

  bool get isWaterDrinkingStation => waterWorkItems.isNotEmpty;

  String get revisionLabel => 'Revision $revision · Effective $effectivityDate';

  static const fireExtinguisher = PreventiveMaintenanceFormSpec(
    assetCategory: 'fire-extinguisher',
    documentTitle: 'Fire Extinguisher Monitoring Form',
    revision: '2',
    effectivityDate: 'November 2023',
    assetNumberLabel: 'Fire extinguisher number',
  );

  static const fireAlarm = PreventiveMaintenanceFormSpec(
    assetCategory: 'fire-alarm',
    documentTitle: 'Fire Alarm Preventive Maintenance Form',
    revision: '1',
    effectivityDate: 'May 2022',
    assetNumberLabel: 'Device particulars (number)',
  );

  static const emergencyLight = PreventiveMaintenanceFormSpec(
    assetCategory: 'emergency-light',
    documentTitle: 'Emergency Lights Preventive Maintenance Form',
    revision: '1',
    effectivityDate: 'May 2022',
    assetNumberLabel: 'Emergency lights number',
  );

  static const waterDrinkingStation = PreventiveMaintenanceFormSpec(
    assetCategory: 'water-drinking-station',
    documentTitle: 'Water Drinking Station Preventive Maintenance Form',
    revision: '1',
    effectivityDate: 'November 2023',
    assetNumberLabel: 'Water drinking station number',
    waterWorkItems: <String>[
      'Replace carbon filter',
      'Replace sediment filter',
      'Checking of UV Light',
    ],
  );

  static const unknown = PreventiveMaintenanceFormSpec(
    assetCategory: '',
    documentTitle: 'Preventive Maintenance Form',
    revision: '—',
    effectivityDate: '—',
    assetNumberLabel: 'Asset/device number',
  );

  static PreventiveMaintenanceFormSpec forCategory(String category) {
    switch (category.trim().toLowerCase()) {
      case 'fire-extinguisher':
        return fireExtinguisher;
      case 'fire-alarm':
        return fireAlarm;
      case 'emergency-light':
        return emergencyLight;
      case 'water-drinking-station':
        return waterDrinkingStation;
      default:
        return unknown;
    }
  }
}
