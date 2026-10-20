import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;

void main() {
  runApp(const WasfaPatientApp());
}

class WasfaPatientApp extends StatelessWidget {
  const WasfaPatientApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Wasfa Patient · بطاقة وصفتي',
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
      home: const PatientHomeScreen(),
    );
  }
}

class PatientHomeScreen extends StatefulWidget {
  const PatientHomeScreen({super.key});

  @override
  State<PatientHomeScreen> createState() => _PatientHomeScreenState();
}

class _PatientHomeScreenState extends State<PatientHomeScreen> {
  int _selectedIndex = 0;
  String _apiBaseUrl = 'http://localhost:5000';
  String _currentPatientId = 'pat-1001';
  bool _isLoading = false;
  String? _errorMessage;
  Map<String, dynamic>? _cardData;

  final Map<String, bool> _takenDoses = {};

  final List<Map<String, dynamic>> _demoPatients = [
    {'id': 'pat-1001', 'name': 'أحمد المجالي (Ahmad Al-Majali)'},
    {'id': 'pat-1002', 'name': 'فاطمة الزعبي (Fatima Al-Zoubi)'},
    {'id': 'pat-1003', 'name': 'طارق الخطيب (Tareq Al-Khatib)'},
    {'id': 'pat-1007', 'name': 'أم يوسف القاضي (Um Youssef)'},
    {'id': 'pat-1009', 'name': 'خليل العبادي (Khalil Al-Abbadi)'},
  ];

  @override
  void initState() {
    super.initState();
    _fetchMedicationCard(_currentPatientId);
  }

  Future<void> _fetchMedicationCard(String patientId) async {
    setState(() {
      _isLoading = true;
      _errorMessage = null;
    });

    try {
      final response = await http.get(Uri.parse('$_apiBaseUrl/api/prescription/card/$patientId'));
      if (response.statusCode == 200) {
        final data = json.decode(utf8.decode(response.bodyBytes)) as Map<String, dynamic>;
        setState(() {
          _cardData = data;
          _isLoading = false;
        });
      } else {
        setState(() {
          _cardData = null;
          _isLoading = false;
          _errorMessage = 'تعذر العثور على سجل المريض أو بطاقة الأدوية (رمز: ${response.statusCode})';
        });
      }
    } catch (e) {
      setState(() {
        _cardData = null;
        _isLoading = false;
        _errorMessage = 'تعذر الاتصال بخادم وصفة ($e)';
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        backgroundColor: const Color(0xFF1B5E5A),
        foregroundColor: Colors.white,
        elevation: 0,
        title: Row(
          children: [
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
              decoration: BoxDecoration(
                color: Colors.white.withOpacity(0.15),
                borderRadius: BorderRadius.circular(6),
              ),
              child: const Text('وصفة للمريض', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 17)),
            ),
            const SizedBox(width: 12),
            const Text('سجل أدويتي الموحد · Patient Med Record', style: TextStyle(fontSize: 14)),
          ],
        ),
        actions: [
          PopupMenuButton<String>(
            tooltip: 'تبديل المريض للتجربة',
            icon: const Icon(Icons.person_pin, color: Colors.white),
            onSelected: (val) {
              setState(() {
                _currentPatientId = val;
              });
              _fetchMedicationCard(val);
            },
            itemBuilder: (ctx) => _demoPatients.map((p) => PopupMenuItem<String>(
              value: p['id'] as String,
              child: Text(p['name'] as String),
            )).toList(),
          ),
          IconButton(
            tooltip: 'تحديث البيانات',
            icon: const Icon(Icons.refresh),
            onPressed: () => _fetchMedicationCard(_currentPatientId),
          ),
        ],
      ),
      bottomNavigationBar: NavigationBar(
        selectedIndex: _selectedIndex,
        onDestinationSelected: (idx) {
          setState(() {
            _selectedIndex = idx;
          });
        },
        destinations: const [
          NavigationDestination(icon: Icon(Icons.badge_outlined), selectedIcon: Icon(Icons.badge), label: 'البطاقة الموحدة'),
          NavigationDestination(icon: Icon(Icons.schedule_outlined), selectedIcon: Icon(Icons.schedule), label: 'جدول جرعات اليوم'),
          NavigationDestination(icon: Icon(Icons.medication_liquid), selectedIcon: Icon(Icons.medication), label: 'أدويتي الحالية'),
          NavigationDestination(icon: Icon(Icons.lock_outline), selectedIcon: Icon(Icons.lock), label: 'الخصوصية والأذونات'),
        ],
      ),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator(color: Color(0xFF1B5E5A)))
          : _errorMessage != null
              ? Center(
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      const Icon(Icons.error_outline, size: 48, color: Color(0xFFD62828)),
                      const SizedBox(height: 16),
                      Text(_errorMessage!, style: const TextStyle(fontWeight: FontWeight.bold)),
                      const SizedBox(height: 12),
                      ElevatedButton(
                        onPressed: () => _fetchMedicationCard(_currentPatientId),
                        child: const Text('إعادة المحاولة'),
                      ),
                    ],
                  ),
                )
              : _buildCurrentTab(),
    );
  }

  Widget _buildCurrentTab() {
    switch (_selectedIndex) {
      case 0:
        return _buildEmergencyMedCardTab();
      case 1:
        return _buildDailyDosesTab();
      case 2:
        return _buildMedicationsListTab();
      case 3:
        return _buildConsentTab();
      default:
        return const SizedBox.shrink();
    }
  }

  // TAB 1: Shareable Emergency Medication Card
  Widget _buildEmergencyMedCardTab() {
    if (_cardData == null) return const SizedBox.shrink();
    final patient = _cardData!['patient'] as Map<String, dynamic>? ?? {};
    final meds = (_cardData!['medications'] as List<dynamic>?) ?? [];

    return SingleChildScrollView(
      padding: const EdgeInsets.all(24),
      child: Center(
        child: Container(
          constraints: const BoxConstraints(maxWidth: 800),
          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.circular(16),
            boxShadow: [
              BoxShadow(
                color: Colors.black.withOpacity(0.06),
                blurRadius: 16,
                offset: const Offset(0, 4),
              ),
            ],
            border: Border.all(color: const Color(0xFFDCE5E3)),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              // Card Top Banner
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 16),
                decoration: const BoxDecoration(
                  color: Color(0xFF1B5E5A),
                  borderRadius: BorderRadius.only(
                    topLeft: Radius.circular(15),
                    topRight: Radius.circular(15),
                  ),
                ),
                child: Row(
                  children: [
                    const Icon(Icons.health_and_safety, color: Colors.white, size: 28),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            _cardData!['titleAr'] ?? 'بطاقة الأدوية الموحدة للمريض',
                            style: const TextStyle(fontSize: 18, fontWeight: FontWeight.bold, color: Colors.white),
                          ),
                          Text(
                            _cardData!['title'] ?? 'Consolidated Medication Card',
                            style: const TextStyle(fontSize: 12, color: Colors.white70),
                          ),
                        ],
                      ),
                    ),
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                      decoration: BoxDecoration(
                        color: Colors.white.withOpacity(0.2),
                        borderRadius: BorderRadius.circular(12),
                      ),
                      child: const Text('FHIR R4 Verified', style: TextStyle(color: Colors.white, fontSize: 11, fontWeight: FontWeight.bold)),
                    ),
                    const SizedBox(width: 8),
                    IconButton(
                      tooltip: 'طباعة البطاقة الدوائية الرسمية',
                      icon: const Icon(Icons.print, color: Colors.white, size: 20),
                      onPressed: () {
                        ScaffoldMessenger.of(context).showSnackBar(
                          const SnackBar(
                            backgroundColor: Color(0xFF1B5E5A),
                            content: Text('✓ جاري تجهيز وطباعة بطاقة المريض الموحدة للطوارئ (PDF)'),
                          ),
                        );
                      },
                    ),
                  ],
                ),
              ),

              // Patient Info Banner
              Padding(
                padding: const EdgeInsets.all(24),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        CircleAvatar(
                          radius: 30,
                          backgroundColor: const Color(0xFF1B5E5A).withOpacity(0.1),
                          child: const Icon(Icons.person, size: 36, color: Color(0xFF1B5E5A)),
                        ),
                        const SizedBox(width: 16),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                patient['nameAr'] ?? patient['nameEn'] ?? 'مريض',
                                style: const TextStyle(fontSize: 22, fontWeight: FontWeight.bold, color: Color(0xFF0D1F1E)),
                              ),
                              Text(
                                patient['nameEn'] ?? '',
                                style: const TextStyle(fontSize: 14, color: Color(0xFF5F7370)),
                              ),
                              const SizedBox(height: 8),
                              Wrap(
                                spacing: 16,
                                runSpacing: 6,
                                children: [
                                  Text('تاريخ الميلاد: ${patient['birthDate'] ?? '-'}', style: const TextStyle(fontSize: 13, color: Color(0xFF5F7370))),
                                  Text('الجنس: ${patient['gender'] ?? '-'}', style: const TextStyle(fontSize: 13, color: Color(0xFF5F7370))),
                                  Text('الوزن: ${patient['weightKg'] ?? '-'} كغ', style: const TextStyle(fontSize: 13, color: Color(0xFF5F7370))),
                                ],
                              ),
                            ],
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 20),
                    const Divider(),
                    const SizedBox(height: 12),
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        Text(
                          'الأدوية الفعالة الحالية (${meds.length})',
                          style: const TextStyle(fontSize: 16, fontWeight: FontWeight.bold, color: Color(0xFF0D1F1E)),
                        ),
                        const Text(
                          'تحديث لحظي لحماية المريض من التضارب الدوائي',
                          style: TextStyle(fontSize: 12, color: Color(0xFF5F7370)),
                        ),
                      ],
                    ),
                    const SizedBox(height: 12),
                    if (meds.isEmpty)
                      const Padding(
                        padding: EdgeInsets.symmetric(vertical: 24),
                        child: Center(child: Text('لا توجد أدوية مسجلة حالياً لهذا المريض')),
                      )
                    else
                      ...meds.map((m) => Container(
                        margin: const EdgeInsets.only(bottom: 12),
                        padding: const EdgeInsets.all(16),
                        decoration: BoxDecoration(
                          color: const Color(0xFFF4F7F6),
                          borderRadius: BorderRadius.circular(10),
                          border: Border.all(color: const Color(0xFFDCE5E3)),
                        ),
                        child: Row(
                          children: [
                            Container(
                              padding: const EdgeInsets.all(8),
                              decoration: BoxDecoration(
                                color: Colors.white,
                                borderRadius: BorderRadius.circular(8),
                              ),
                              child: const Icon(Icons.medication, color: Color(0xFF1B5E5A)),
                            ),
                            const SizedBox(width: 14),
                            Expanded(
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Text(
                                    m['brandName'] ?? m['moleculeCode'] ?? '',
                                    style: const TextStyle(fontSize: 16, fontWeight: FontWeight.bold, fontFamily: 'Consolas, monospace'),
                                  ),
                                  const SizedBox(height: 4),
                                  Text(
                                    'المادة: ${m['moleculeCode'] ?? ''} · التركيز: ${m['strength'] ?? ''}',
                                    style: const TextStyle(fontSize: 13, color: Color(0xFF5F7370)),
                                  ),
                                  Text(
                                    'الجرعة والتكرار: ${m['frequency'] ?? ''}',
                                    style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: Color(0xFF1B5E5A)),
                                  ),
                                ],
                              ),
                            ),
                            Container(
                              padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                              decoration: BoxDecoration(
                                color: Colors.white,
                                borderRadius: BorderRadius.circular(6),
                                border: Border.all(color: const Color(0xFFDCE5E3)),
                              ),
                              child: Text(
                                m['shortCode'] ?? '',
                                style: const TextStyle(fontFamily: 'Consolas, monospace', fontWeight: FontWeight.bold, fontSize: 13, color: Color(0xFF1B5E5A)),
                              ),
                            ),
                          ],
                        ),
                      )),
                    const SizedBox(height: 16),
                    Container(
                      padding: const EdgeInsets.all(12),
                      decoration: BoxDecoration(
                        color: const Color(0xFFE8F5E9),
                        borderRadius: BorderRadius.circular(8),
                        border: Border.all(color: const Color(0xFFA5D6A7)),
                      ),
                      child: const Row(
                        children: [
                          Icon(Icons.info_outline, color: Color(0xFF2E7D32), size: 20),
                          SizedBox(width: 10),
                          Expanded(
                            child: Text(
                              'أظهر هذه البطاقة للطبيب أو الطوارئ عند أي مراجعة طبية لضمان عدم كتابة أي علاج يتعارض مع قائمتك الحالية.',
                              style: TextStyle(fontSize: 12, color: Color(0xFF1B5E20), fontWeight: FontWeight.w500),
                            ),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  // TAB 2: Today's Doses Timeline
  Widget _buildDailyDosesTab() {
    final meds = (_cardData!['medications'] as List<dynamic>?) ?? [];

    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        Container(
          padding: const EdgeInsets.all(16),
          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.circular(12),
            border: Border.all(color: const Color(0xFFDCE5E3)),
          ),
          child: Row(
            children: [
              const Icon(Icons.today, color: Color(0xFF1B5E5A), size: 28),
              const SizedBox(width: 12),
              const Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text('مواعيد الجرعات اليومية', style: TextStyle(fontSize: 17, fontWeight: FontWeight.bold)),
                    Text('انقر لتأكيد أخذ الجرعة للحفاظ على انتظام علاجك', style: TextStyle(fontSize: 13, color: Color(0xFF5F7370))),
                  ],
                ),
              ),
              Text(
                '${_takenDoses.values.where((v) => v).length} / ${meds.length} مكتمل',
                style: const TextStyle(fontWeight: FontWeight.bold, color: Color(0xFF1B5E5A)),
              ),
            ],
          ),
        ),
        const SizedBox(height: 16),
        ...meds.map((m) {
          final id = m['shortCode'] ?? m['moleculeCode'];
          final isTaken = _takenDoses[id] == true;

          return Container(
            margin: const EdgeInsets.only(bottom: 12),
            decoration: BoxDecoration(
              color: isTaken ? const Color(0xFFE8F5E9) : Colors.white,
              borderRadius: BorderRadius.circular(12),
              border: Border.all(color: isTaken ? const Color(0xFFA5D6A7) : const Color(0xFFDCE5E3)),
            ),
            child: ListTile(
              contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
              leading: Icon(
                isTaken ? Icons.check_circle : Icons.radio_button_unchecked,
                color: isTaken ? const Color(0xFF2A9D8F) : const Color(0xFF5F7370),
                size: 28,
              ),
              title: Text(
                m['brandName'] ?? m['moleculeCode'] ?? '',
                style: TextStyle(
                  fontWeight: FontWeight.bold,
                  decoration: isTaken ? TextDecoration.lineThrough : null,
                ),
              ),
              subtitle: Text('${m['strength'] ?? ''} · التكرار: ${m['frequency'] ?? ''}'),
              trailing: ElevatedButton(
                style: ElevatedButton.styleFrom(
                  backgroundColor: isTaken ? Colors.white : const Color(0xFF1B5E5A),
                  foregroundColor: isTaken ? const Color(0xFF2E7D32) : Colors.white,
                  elevation: 0,
                ),
                onPressed: () {
                  setState(() {
                    _takenDoses[id] = !isTaken;
                  });
                },
                child: Text(isTaken ? 'تم الأخذ ✓' : 'أخذت الجرعة'),
              ),
            ),
          );
        }),
      ],
    );
  }

  // TAB 3: Medications List Details & Explanations
  Widget _buildMedicationsListTab() {
    final meds = (_cardData!['medications'] as List<dynamic>?) ?? [];

    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        const Text(
          'دليل أدويتي والهدف العلاجي',
          style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold, color: Color(0xFF0D1F1E)),
        ),
        const SizedBox(height: 6),
        const Text(
          'شرح مبسط لكل علاج واستخدامه وفق البيانات المعتمدة',
          style: TextStyle(fontSize: 13, color: Color(0xFF5F7370)),
        ),
        const SizedBox(height: 16),
        ...meds.map((m) => Container(
          margin: const EdgeInsets.only(bottom: 12),
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
                  Text(
                    m['brandName'] ?? m['moleculeCode'] ?? '',
                    style: const TextStyle(fontSize: 16, fontWeight: FontWeight.bold, fontFamily: 'Consolas, monospace'),
                  ),
                  const Spacer(),
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                    decoration: BoxDecoration(
                      color: const Color(0xFF1B5E5A).withOpacity(0.1),
                      borderRadius: BorderRadius.circular(4),
                    ),
                    child: Text(m['shortCode'] ?? '', style: const TextStyle(fontWeight: FontWeight.bold, color: Color(0xFF1B5E5A), fontSize: 12)),
                  ),
                ],
              ),
              const SizedBox(height: 8),
              Text('المادة الفعالة: ${m['moleculeCode'] ?? ''} (${m['strength'] ?? ''})', style: const TextStyle(color: Color(0xFF5F7370), fontSize: 13)),
              const SizedBox(height: 4),
              Text('طريقة الاستخدام: ${m['frequency'] ?? ''} لمدة ${m['durationDays'] ?? '-'} أيام', style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w500)),
              const SizedBox(height: 12),
              Container(
                padding: const EdgeInsets.all(10),
                decoration: BoxDecoration(
                  color: const Color(0xFFF4F7F6),
                  borderRadius: BorderRadius.circular(8),
                ),
                child: const Row(
                  children: [
                    Icon(Icons.lightbulb_outline, size: 16, color: Color(0xFFF77F00)),
                    SizedBox(width: 8),
                    Expanded(
                      child: Text(
                        'تم تدقيق هذا العلاج ومطابقته لمنع أي تداخلات أو تكرار دوائي غير مقصود.',
                        style: TextStyle(fontSize: 12, color: Color(0xFF5F7370)),
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
        )),
      ],
    );
  }

  // TAB 4: Consent & Privacy (Grant/Revoke clinic access)
  Widget _buildConsentTab() {
    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        Container(
          padding: const EdgeInsets.all(20),
          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.circular(12),
            border: Border.all(color: const Color(0xFFDCE5E3)),
          ),
          child: const Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Icon(Icons.shield, color: Color(0xFF1B5E5A), size: 28),
                  SizedBox(width: 12),
                  Text('إدارة الخصوصية والموافقة (Consent)', style: TextStyle(fontSize: 17, fontWeight: FontWeight.bold)),
                ],
              ),
              SizedBox(height: 8),
              Text(
                'أنت من يملك سجلك الطبي. يمكنك منح أو إلغاء صلاحية الوصول لقائمتك الدوائية لأي عيادة أو مركز طبي خاص في أي وقت.',
                style: TextStyle(fontSize: 13, color: Color(0xFF5F7370)),
              ),
            ],
          ),
        ),
        const SizedBox(height: 16),
        _buildConsentItem('عيادة الدكتور سامر - باطنية (Amman Clinic)', 'ممنوح (Granted)', true),
        _buildConsentItem('مركز الشميساني الطبي (Shmeisani Health)', 'ممنوح (Granted)', true),
        _buildConsentItem('مستشفى الأمل التخصصي', 'ملغي (Revoked)', false),
      ],
    );
  }

  Widget _buildConsentItem(String clinicName, String status, bool isActive) {
    return Container(
      margin: const EdgeInsets.only(bottom: 12),
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: const Color(0xFFDCE5E3)),
      ),
      child: Row(
        children: [
          Icon(isActive ? Icons.verified_user : Icons.remove_moderator, color: isActive ? const Color(0xFF2A9D8F) : const Color(0xFFD62828)),
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(clinicName, style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 15)),
                Text('نطاق الصلاحية: قراءة قائمة الأدوية (read_medications) · الحالة: $status', style: const TextStyle(fontSize: 12, color: Color(0xFF5F7370))),
              ],
            ),
          ),
          TextButton(
            onPressed: () {
              ScaffoldMessenger.of(context).showSnackBar(
                SnackBar(content: Text('تم تحديث إذن الوصول لـ $clinicName')),
              );
            },
            child: Text(isActive ? 'إلغاء الإذن' : 'إعادة التفعيل', style: TextStyle(color: isActive ? const Color(0xFFD62828) : const Color(0xFF1B5E5A))),
          ),
        ],
      ),
    );
  }
}
