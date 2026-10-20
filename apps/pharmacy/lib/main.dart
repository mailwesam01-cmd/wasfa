import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;

void main() {
  runApp(const WasfaPharmacyApp());
}

class WasfaPharmacyApp extends StatelessWidget {
  const WasfaPharmacyApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Wasfa Pharmacy · صيدلية وصفة',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(
        useMaterial3: true,
        colorScheme: ColorScheme.fromSeed(
          seedColor: const Color(0xFF1B5E5A),
          primary: const Color(0xFF1B5E5A),
          secondary: const Color(0xFF2E8B84),
          surface: Colors.white,
        ),
        scaffoldBackgroundColor: const Color(0xFFF4F7F6),
        fontFamily: 'Segoe UI',
      ),
      home: const PharmacyHomeScreen(),
    );
  }
}

class PharmacyHomeScreen extends StatefulWidget {
  const PharmacyHomeScreen({super.key});

  @override
  State<PharmacyHomeScreen> createState() => _PharmacyHomeScreenState();
}

class _PharmacyHomeScreenState extends State<PharmacyHomeScreen> {
  final TextEditingController _searchController = TextEditingController(text: 'WASFA-8931');
  final TextEditingController _substituteReasonController = TextEditingController();
  final TextEditingController _substituteBrandController = TextEditingController();
  
  String _apiBaseUrl = 'http://localhost:5000';
  bool _isLoading = false;
  String? _errorMessage;
  Map<String, dynamic>? _prescriptionData;

  bool _isSubstituted = false;
  double _dispenseQuantity = 1.0;
  final bool _isComplete = true;

  @override
  void initState() {
    super.initState();
    _fetchPrescription('WASFA-8931');
  }

  Future<void> _fetchPrescription(String code) async {
    final cleanCode = code.trim().toUpperCase();
    if (cleanCode.isEmpty) return;

    setState(() {
      _isLoading = true;
      _errorMessage = null;
    });

    try {
      final response = await http.get(Uri.parse('$_apiBaseUrl/api/prescription/$cleanCode'));
      if (response.statusCode == 200) {
        final data = json.decode(utf8.decode(response.bodyBytes)) as Map<String, dynamic>;
        setState(() {
          _prescriptionData = data;
          _isLoading = false;
          _dispenseQuantity = 1.0;
          _isSubstituted = false;
          _substituteBrandController.clear();
          _substituteReasonController.clear();
        });
      } else {
        setState(() {
          _prescriptionData = null;
          _isLoading = false;
          _errorMessage = 'الوصفة غير موجودة أو انتهت صلاحيتها (الكود: $cleanCode)';
        });
      }
    } catch (e) {
      setState(() {
        _prescriptionData = null;
        _isLoading = false;
        _errorMessage = 'تعذر الاتصال بخادم وصفة ($e)';
      });
    }
  }

  Future<void> _submitDispense() async {
    if (_prescriptionData == null) return;

    final requestFhirId = _prescriptionData!['prescriptionId'] as String;

    setState(() {
      _isLoading = true;
    });

    try {
      final payload = {
        'requestFhirId': requestFhirId,
        'pharmacyId': 'pharm-amman-center',
        'quantity': _dispenseQuantity,
        'isCompleteDispense': _isComplete,
        'substitutedBrand': _isSubstituted ? _substituteBrandController.text.trim() : null,
        'substitutionReason': _isSubstituted ? _substituteReasonController.text.trim() : null,
      };

      final response = await http.post(
        Uri.parse('$_apiBaseUrl/fhir/MedicationDispense'),
        headers: {'Content-Type': 'application/json'},
        body: json.encode(payload),
      );

      if (response.statusCode == 200 || response.statusCode == 201) {
        if (!mounted) return;
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            backgroundColor: Color(0xFF2A9D8F),
            content: Text('✓ تم صرف الدواء بنجاح وتسجيل عملية الصرف وفق معيار FHIR'),
          ),
        );
        await _fetchPrescription(_prescriptionData!['shortCode'] ?? requestFhirId);
      } else {
        throw Exception('فشل الخادم في حفظ سجل الصرف (${response.statusCode})');
      }
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          backgroundColor: const Color(0xFFD62828),
          content: Text('خطأ أثناء الصرف: $e'),
        ),
      );
      setState(() {
        _isLoading = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        backgroundColor: const Color(0xFF1B5E5A),
        foregroundColor: Colors.white,
        elevation: 1,
        title: Row(
          children: [
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
              decoration: BoxDecoration(
                color: Colors.white.withValues(alpha: 0.15),
                borderRadius: BorderRadius.circular(6),
              ),
              child: const Text(
                'وصفة · Wasfa',
                style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16),
              ),
            ),
            const SizedBox(width: 8),
            const Flexible(
              child: Text(
                'بوابة الصيدلية',
                overflow: TextOverflow.ellipsis,
                style: TextStyle(fontSize: 14, fontWeight: FontWeight.w400),
              ),
            ),
          ],
        ),
        actions: [
          IconButton(
            tooltip: 'إعدادات عنوان الخادم',
            icon: const Icon(Icons.settings),
            onPressed: () => _showSettingsDialog(),
          ),
        ],
      ),
      body: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Sidebar: Search / Scan
          Container(
            width: 320,
            color: Colors.white,
            padding: const EdgeInsets.all(16),
            child: SingleChildScrollView(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text(
                    'مسح أو إدخال كود الوصفة',
                    style: TextStyle(fontSize: 15, fontWeight: FontWeight.bold, color: Color(0xFF0D1F1E)),
                  ),
                  const SizedBox(height: 6),
                  const Text(
                    'أدخل كود الاستلام السريع للوصفة',
                    style: TextStyle(fontSize: 12, color: Color(0xFF5F7370)),
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: _searchController,
                    textCapitalization: TextCapitalization.characters,
                    decoration: InputDecoration(
                      hintText: 'مثال: WASFA-8931',
                      prefixIcon: const Icon(Icons.qr_code_scanner, color: Color(0xFF1B5E5A)),
                      border: OutlineInputBorder(borderRadius: BorderRadius.circular(8)),
                      contentPadding: const EdgeInsets.symmetric(horizontal: 10, vertical: 10),
                    ),
                    onSubmitted: (val) => _fetchPrescription(val),
                  ),
                  const SizedBox(height: 10),
                  SizedBox(
                    width: double.infinity,
                    child: ElevatedButton.icon(
                      style: ElevatedButton.styleFrom(
                        backgroundColor: const Color(0xFF1B5E5A),
                        foregroundColor: Colors.white,
                        padding: const EdgeInsets.symmetric(vertical: 10),
                        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
                      ),
                      onPressed: _isLoading ? null : () => _fetchPrescription(_searchController.text),
                      icon: const Icon(Icons.search, size: 18),
                      label: const Text('جلب الوصفة', style: TextStyle(fontWeight: FontWeight.bold)),
                    ),
                  ),
                  const SizedBox(height: 16),
                  const Divider(),
                  const SizedBox(height: 8),
                  const Text(
                    'أمثلة تجريبية:',
                    style: TextStyle(fontSize: 12, fontWeight: FontWeight.bold, color: Color(0xFF5F7370)),
                  ),
                  const SizedBox(height: 6),
                  Wrap(
                    spacing: 6,
                    runSpacing: 6,
                    children: [
                      ActionChip(
                        label: const Text('WASFA-8931', style: TextStyle(fontSize: 11)),
                        onPressed: () {
                          _searchController.text = 'WASFA-8931';
                          _fetchPrescription('WASFA-8931');
                        },
                      ),
                      ActionChip(
                        label: const Text('WASFA-1042', style: TextStyle(fontSize: 11)),
                        onPressed: () {
                          _searchController.text = 'WASFA-1042';
                          _fetchPrescription('WASFA-1042');
                        },
                      ),
                    ],
                  ),
                  const SizedBox(height: 20),
                  Container(
                    padding: const EdgeInsets.all(10),
                    decoration: BoxDecoration(
                      color: const Color(0xFFF4F7F6),
                      borderRadius: BorderRadius.circular(8),
                      border: Border.all(color: const Color(0xFFDCE5E3)),
                    ),
                    child: const Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          children: [
                            Icon(Icons.shield_outlined, size: 14, color: Color(0xFF1B5E5A)),
                            SizedBox(width: 4),
                            Text('HL7 FHIR R4', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 11)),
                          ],
                        ),
                        SizedBox(height: 4),
                        Text(
                          'تسجيل الصرف فوري لمنع تكرار أو تعارض الأدوية للمريض.',
                          style: TextStyle(fontSize: 11, color: Color(0xFF5F7370)),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ),
          const VerticalDivider(width: 1, thickness: 1, color: Color(0xFFDCE5E3)),

          // Main Detail Area
          Expanded(
            child: _isLoading
                ? const Center(child: CircularProgressIndicator(color: Color(0xFF1B5E5A)))
                : _errorMessage != null
                    ? Center(
                        child: Column(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            const Icon(Icons.error_outline, size: 48, color: Color(0xFFD62828)),
                            const SizedBox(height: 16),
                            Text(_errorMessage!, style: const TextStyle(fontSize: 14, fontWeight: FontWeight.bold)),
                          ],
                        ),
                      )
                    : _prescriptionData == null
                        ? const Center(
                            child: Text('الرجاء إدخال كود الوصفة لعرض تفاصيلها', style: TextStyle(color: Color(0xFF5F7370))),
                          )
                        : _buildPrescriptionView(),
          ),
        ],
      ),
    );
  }

  Widget _buildPrescriptionView() {
    final patient = _prescriptionData!['patient'] as Map<String, dynamic>? ?? {};
    final medication = _prescriptionData!['medication'] as Map<String, dynamic>? ?? {};
    final overrides = (_prescriptionData!['safetyOverrides'] as List<dynamic>?) ?? [];
    final dispenses = (_prescriptionData!['dispensingHistory'] as List<dynamic>?) ?? [];
    final isDispensed = _prescriptionData!['isDispensed'] == true;

    return SingleChildScrollView(
      padding: const EdgeInsets.all(20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Header Status Card
          Container(
            padding: const EdgeInsets.all(16),
            decoration: BoxDecoration(
              color: Colors.white,
              borderRadius: BorderRadius.circular(12),
              border: Border.all(color: const Color(0xFFDCE5E3)),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Expanded(
                      child: Text(
                        patient['nameAr'] ?? patient['nameEn'] ?? 'مريض غير مسمى',
                        style: const TextStyle(fontSize: 18, fontWeight: FontWeight.bold, color: Color(0xFF0D1F1E)),
                      ),
                    ),
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                      decoration: BoxDecoration(
                        color: isDispensed ? const Color(0xFF2A9D8F).withValues(alpha: 0.12) : const Color(0xFF3A7CA5).withValues(alpha: 0.12),
                        borderRadius: BorderRadius.circular(20),
                        border: Border.all(
                          color: isDispensed ? const Color(0xFF2A9D8F) : const Color(0xFF3A7CA5),
                        ),
                      ),
                      child: Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Icon(
                            isDispensed ? Icons.check_circle : Icons.pending_actions,
                            size: 14,
                            color: isDispensed ? const Color(0xFF2A9D8F) : const Color(0xFF3A7CA5),
                          ),
                          const SizedBox(width: 4),
                          Text(
                            isDispensed ? 'تم الصرف (Dispensed)' : 'جاهزة للصرف (Active)',
                            style: TextStyle(
                              fontWeight: FontWeight.bold,
                              fontSize: 12,
                              color: isDispensed ? const Color(0xFF2A9D8F) : const Color(0xFF3A7CA5),
                            ),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 8),
                Wrap(
                  spacing: 12,
                  runSpacing: 6,
                  children: [
                    _buildMetaChip(Icons.cake_outlined, 'الميلاد: ${patient['birthDate'] ?? 'غير محدد'}'),
                    _buildMetaChip(Icons.fitness_center_outlined, 'الوزن: ${patient['weightKg'] ?? '-'} كغ'),
                    _buildMetaChip(Icons.transgender, 'الجنس: ${patient['gender'] ?? '-'}'),
                    if (patient['isPregnant'] == true)
                      _buildAlertBadge('حامل (Pregnant)', const Color(0xFF8B0000)),
                    if (patient['renalImpairment'] != null && patient['renalImpairment'] != 'none')
                      _buildAlertBadge('قصور كلوي (${patient['renalImpairment']})', const Color(0xFFF77F00)),
                  ],
                ),
              ],
            ),
          ),
          const SizedBox(height: 16),

          // Safety Overrides Alert
          if (overrides.isNotEmpty) ...[
            Container(
              padding: const EdgeInsets.all(14),
              decoration: BoxDecoration(
                color: const Color(0xFFFFF3CD),
                borderRadius: BorderRadius.circular(8),
                border: const Border(
                  left: BorderSide(color: Color(0xFFF77F00), width: 4),
                  top: BorderSide(color: Color(0xFFF77F00), width: 0.5),
                  right: BorderSide(color: Color(0xFFF77F00), width: 0.5),
                  bottom: BorderSide(color: Color(0xFFF77F00), width: 0.5),
                ),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Row(
                    children: [
                      Icon(Icons.warning_amber_rounded, color: Color(0xFFF77F00), size: 18),
                      SizedBox(width: 6),
                      Text(
                        'تنبيه: تم تجاوز تحذيرات سريرية من الطبيب مع ذكر السبب',
                        style: TextStyle(fontWeight: FontWeight.bold, fontSize: 13, color: Color(0xFF7A4100)),
                      ),
                    ],
                  ),
                  const SizedBox(height: 6),
                  ...overrides.map((o) => Padding(
                    padding: const EdgeInsets.only(top: 4),
                    child: Text(
                      '• [${o['severity']}] - ${o['code']}: "${o['reason']}"',
                      style: const TextStyle(fontSize: 12, color: Color(0xFF553300)),
                    ),
                  )),
                ],
              ),
            ),
            const SizedBox(height: 16),
          ],

          // Medication Prescription Details
          Container(
            padding: const EdgeInsets.all(16),
            decoration: BoxDecoration(
              color: Colors.white,
              borderRadius: BorderRadius.circular(12),
              border: Border.all(color: const Color(0xFFDCE5E3)),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text(
                  'تفاصيل الدواء الموصوف',
                  style: TextStyle(fontSize: 15, fontWeight: FontWeight.bold, color: Color(0xFF0D1F1E)),
                ),
                const SizedBox(height: 12),
                Container(
                  padding: const EdgeInsets.all(12),
                  decoration: BoxDecoration(
                    color: const Color(0xFFF4F7F6),
                    borderRadius: BorderRadius.circular(8),
                    border: Border.all(color: const Color(0xFFDCE5E3)),
                  ),
                  child: Row(
                    children: [
                      Container(
                        padding: const EdgeInsets.all(8),
                        decoration: BoxDecoration(
                          color: const Color(0xFF1B5E5A).withValues(alpha: 0.1),
                          shape: BoxShape.circle,
                        ),
                        child: const Icon(Icons.medication, color: Color(0xFF1B5E5A), size: 24),
                      ),
                      const SizedBox(width: 12),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              medication['brandName'] ?? medication['moleculeCode'] ?? '',
                              style: const TextStyle(fontSize: 16, fontWeight: FontWeight.bold, fontFamily: 'Consolas, monospace'),
                            ),
                            const SizedBox(height: 2),
                            Text(
                              'المادة الفعالة: ${medication['moleculeCode'] ?? ''} · التركيز: ${medication['strength'] ?? ''}',
                              style: const TextStyle(fontSize: 13, color: Color(0xFF5F7370)),
                            ),
                            const SizedBox(height: 2),
                            Text(
                              'الجرعة والتكرار: ${medication['doseText'] ?? medication['frequency'] ?? ''} · المدة: ${medication['durationDays'] ?? '-'} أيام',
                              style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: Color(0xFF1B5E5A)),
                            ),
                          ],
                        ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 16),

                // Dispense Action Form
                CheckboxListTile(
                  contentPadding: EdgeInsets.zero,
                  title: const Text('استبدال بديل تجاري مكافئ (Brand Substitution)', style: TextStyle(fontSize: 13)),
                  value: _isSubstituted,
                  activeColor: const Color(0xFF1B5E5A),
                  onChanged: (val) {
                    setState(() {
                      _isSubstituted = val ?? false;
                    });
                  },
                ),
                if (_isSubstituted) ...[
                  const SizedBox(height: 8),
                  Row(
                    children: [
                      Expanded(
                        child: TextField(
                          controller: _substituteBrandController,
                          decoration: const InputDecoration(
                            labelText: 'اسم الدواء البديل المصروف',
                            hintText: 'مثال: Amoxil 500mg',
                            border: OutlineInputBorder(),
                            isDense: true,
                          ),
                        ),
                      ),
                      const SizedBox(width: 12),
                      Expanded(
                        child: TextField(
                          controller: _substituteReasonController,
                          decoration: const InputDecoration(
                            labelText: 'سبب الاستبدال الموثق',
                            hintText: 'عدم توفر الصنف التجاري',
                            border: OutlineInputBorder(),
                            isDense: true,
                          ),
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 12),
                ],
                const SizedBox(height: 8),
                ElevatedButton.icon(
                  style: ElevatedButton.styleFrom(
                    backgroundColor: const Color(0xFF1B5E5A),
                    foregroundColor: Colors.white,
                    padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 12),
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
                  ),
                  onPressed: _isLoading ? null : _submitDispense,
                  icon: const Icon(Icons.check_circle_outline, size: 18),
                  label: Text(
                    isDispensed ? 'تسجيل صرف إضافي' : 'تأكيد وإتمام صرف الوصفة',
                    style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 14),
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 16),

          // Dispense History Log
          if (dispenses.isNotEmpty) ...[
            Container(
              padding: const EdgeInsets.all(16),
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(12),
                border: Border.all(color: const Color(0xFFDCE5E3)),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text(
                    'سجل عمليات الصرف المسبقة',
                    style: TextStyle(fontSize: 14, fontWeight: FontWeight.bold, color: Color(0xFF0D1F1E)),
                  ),
                  const SizedBox(height: 8),
                  ...dispenses.map((d) => Container(
                    margin: const EdgeInsets.only(bottom: 6),
                    padding: const EdgeInsets.all(8),
                    decoration: BoxDecoration(
                      color: const Color(0xFFF4F7F6),
                      borderRadius: BorderRadius.circular(6),
                    ),
                    child: Row(
                      children: [
                        const Icon(Icons.history, color: Color(0xFF1B5E5A), size: 16),
                        const SizedBox(width: 8),
                        Text('تم الصرف: ${d['dispensedAt'] ?? ''}', style: const TextStyle(fontSize: 12)),
                        const SizedBox(width: 8),
                        Text('الكمية: ${d['quantity'] ?? 1}', style: const TextStyle(fontSize: 12)),
                      ],
                    ),
                  )),
                ],
              ),
            ),
          ],
        ],
      ),
    );
  }

  Widget _buildMetaChip(IconData icon, String text) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(icon, size: 14, color: const Color(0xFF5F7370)),
        const SizedBox(width: 4),
        Text(text, style: const TextStyle(fontSize: 12, color: Color(0xFF5F7370))),
      ],
    );
  }

  Widget _buildAlertBadge(String text, Color color) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.1),
        borderRadius: BorderRadius.circular(4),
        border: Border.all(color: color, width: 1),
      ),
      child: Text(text, style: TextStyle(fontSize: 11, fontWeight: FontWeight.bold, color: color)),
    );
  }

  void _showSettingsDialog() {
    final controller = TextEditingController(text: _apiBaseUrl);
    showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('إعدادات الاتصال بالخادم'),
        content: TextField(
          controller: controller,
          decoration: const InputDecoration(
            labelText: 'عنوان خادم Wasfa.Api',
            hintText: 'http://localhost:5000',
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx),
            child: const Text('إلغاء'),
          ),
          ElevatedButton(
            onPressed: () {
              setState(() {
                _apiBaseUrl = controller.text.trim();
              });
              Navigator.pop(ctx);
            },
            child: const Text('حفظ'),
          ),
        ],
      ),
    );
  }
}
