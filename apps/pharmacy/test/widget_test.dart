import 'package:flutter_test/flutter_test.dart';
import 'package:pharmacy/main.dart';

void main() {
  testWidgets('Pharmacy app smoke test', (WidgetTester tester) async {
    await tester.pumpWidget(const WasfaPharmacyApp());
    expect(find.textContaining('وصفة'), findsWidgets);
  });
}
