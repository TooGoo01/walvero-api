# Flutter UI: ProgressBased Redeem Controller Dəyişiklikləri Prompt

Bu sənəd Flutter UI-da ProgressBased kartlar üçün Redeem funksionallığının dəyişikliklərini təsvir edir.

## Məqsəd

API-də ProgressBased kartlar üçün free reward (pulsuz mükafat) xərcləmə funksionallığı əlavə edilib. Bu dəyişikliklər UI-da aşağıdakı endpoint-lərdə əks olunmalıdır:
- `GET /api/Redeem/LookupByCode` - Kart məlumatlarının yoxlanması
- `POST /api/Redeem/Start` - Redeem əməliyyatının başlatılması

## API Response Dəyişiklikləri

### 1. LookupByCode Endpoint (GET /api/Redeem/LookupByCode)

#### Request
```
GET /api/Redeem/LookupByCode?code={barcode|walveroId}&currentPoints={points}
```

#### Response (ProgressBased kartlar üçün yeni field-lər)

```json
{
  "success": true,
  "customerFullName": "John Doe",
  "cardNumber": "123456",
  "currentPoints": 150,
  "completedCycles": 2,          // YENİ: Mövcud pulsuz mükafat sayı
  "availableRewardCount": 2,      // YENİ: Xərclənə bilən mükafat sayı (= completedCycles)
  "maxSpendCount": 2,             // YENİ: UI input limiti (= completedCycles)
  "freeRewardLabel": "Pulsuz içki", // YENİ: Mükafat label-i (default: "Pulsuz içki")
  "tierName": null,               // TierBased üçün
  "tierPercent": null,            // TierBased üçün
  "tierPercentCash": null,        // TierBased üçün
  "tierIconUrl": null             // TierBased üçün
}
```

#### Field-lərin İzahı

- **completedCycles**: Kartın hazırda sahib olduğu pulsuz mükafat sayı (spendable rewards)
- **availableRewardCount**: Xərclənə bilən mükafat sayı (completedCycles ilə eyni)
- **maxSpendCount**: UI-da input field-inin maksimum dəyəri (completedCycles ilə eyni)
- **freeRewardLabel**: Mükafatın adı/label-i (məs: "Pulsuz içki", "Burger", və s.)

### 2. Start Redeem Endpoint (POST /api/Redeem/Start)

#### Request

**PointsBased/TierBased kartlar üçün (mövcud):**
```json
{
  "code": "123456",
  "delta": 100,
  "orderId": "ORDER123",
  "mode": "auto",
  "paymentMethod": "card",
  "spendCount": null  // ProgressBased olmayan kartlar üçün null
}
```

**ProgressBased kartlar üçün (yeni):**
```json
{
  "code": "123456",
  "delta": 0,                     // Free reward xərcləməsi üçün 0 olmalıdır
  "orderId": "ORDER123",
  "mode": "auto",
  "paymentMethod": "card",
  "spendCount": 1                 // YENİ: Xərclənə bilən mükafat sayı (1, 2, 3, ...)
}
```

#### Response

```json
{
  "success": true,
  "message": "Redeem completed",
  "data": {
    "requestId": 123,
    "remainingRewardCount": 1,    // YENİ: Redeem-dən sonra qalan CompletedCycles
    "freeRewardLabel": "Pulsuz içki", // YENİ: Mükafat label-i
    "appliedDelta": -1            // YENİ: Free reward xərcləməsi üçün: -spendCount
  }
}
```

## UI Dəyişiklikləri

### 1. LookupByCode UI (Kart Yoxlanması Ekranı)

#### ProgressBased Kartlar Üçün

**Mövcud vəziyyət:**
- Yalnız `currentPoints` göstərilir
- Redeem üçün point xərcləmə input-u var

**Tələb olunan:**
- ProgressBased kartlar üçün `completedCycles`, `availableRewardCount`, `maxSpendCount`, və `freeRewardLabel` field-lərini göstərmək
- Free reward xərcləməsi üçün input field əlavə etmək
- Points-based redeem və free reward redeem arasında seçim imkanı (radio button və ya tab)

#### UI Layout Tövsiyəsi

```
┌─────────────────────────────────┐
│ Kart Məlumatları                │
├─────────────────────────────────┤
│ Müştəri: John Doe               │
│ Kart Nömrəsi: 123456            │
│                                 │
│ Progress: 5/10                  │ ← ProgressBased üçün
│                                 │
│ Bonus: 2 Pulsuz içki            │ ← freeRewardText
│                                 │
├─────────────────────────────────┤
│ Redeem Seçimi                   │
│ ○ Points Xərclə                │
│ ● Free Reward Xərclə            │ ← YENİ: Radio button
│                                 │
│ Xərcləmə sayı: [1] / 2          │ ← YENİ: Input (max=maxSpendCount)
│                                 │
│ [Xərclə]                        │
└─────────────────────────────────┘
```

#### Dart Code Example

```dart
class RedeemLookupResponse {
  final bool success;
  final String? customerFullName;
  final String? cardNumber;
  final double? currentPoints;
  final int? completedCycles;        // YENİ
  final int? availableRewardCount;   // YENİ
  final int? maxSpendCount;          // YENİ
  final String? freeRewardLabel;     // YENİ
  final String? tierName;
  final int? tierPercent;
  final int? tierPercentCash;
  final String? tierIconUrl;

  RedeemLookupResponse({
    required this.success,
    this.customerFullName,
    this.cardNumber,
    this.currentPoints,
    this.completedCycles,
    this.availableRewardCount,
    this.maxSpendCount,
    this.freeRewardLabel,
    this.tierName,
    this.tierPercent,
    this.tierPercentCash,
    this.tierIconUrl,
  });

  // ProgressBased kart yoxlaması
  bool get isProgressBased => 
      completedCycles != null && 
      availableRewardCount != null && 
      maxSpendCount != null;

  factory RedeemLookupResponse.fromJson(Map<String, dynamic> json) {
    return RedeemLookupResponse(
      success: json['success'] ?? false,
      customerFullName: json['customerFullName'],
      cardNumber: json['cardNumber'],
      currentPoints: json['currentPoints']?.toDouble(),
      completedCycles: json['completedCycles'],
      availableRewardCount: json['availableRewardCount'],
      maxSpendCount: json['maxSpendCount'],
      freeRewardLabel: json['freeRewardLabel'] ?? 'Pulsuz içki',
      tierName: json['tierName'],
      tierPercent: json['tierPercent'],
      tierPercentCash: json['tierPercentCash'],
      tierIconUrl: json['tierIconUrl'],
    );
  }
}
```

#### UI Widget Example

```dart
Widget buildRedeemOptions(RedeemLookupResponse response) {
  if (response.isProgressBased) {
    return _buildProgressBasedRedeem(response);
  } else {
    return _buildPointsBasedRedeem(response);
  }
}

Widget _buildProgressBasedRedeem(RedeemLookupResponse response) {
  RedeemMode selectedMode = RedeemMode.points; // Points və ya FreeReward
  int spendCount = 1;

  return Column(
    children: [
      // Free reward info
      if (response.completedCycles != null)
        Card(
          child: Padding(
            padding: EdgeInsets.all(16),
            child: Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text(
                  '${response.completedCycles} ${response.freeRewardLabel ?? "Pulsuz içki"}',
                  style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
                ),
                Text(
                  'Mövcuddur',
                  style: TextStyle(color: Colors.green),
                ),
              ],
            ),
          ),
        ),

      SizedBox(height: 16),

      // Redeem mode selection
      Row(
        children: [
          Radio<RedeemMode>(
            value: RedeemMode.points,
            groupValue: selectedMode,
            onChanged: (value) => setState(() => selectedMode = value!),
          ),
          Text('Points Xərclə'),
          
          SizedBox(width: 16),
          
          Radio<RedeemMode>(
            value: RedeemMode.freeReward,
            groupValue: selectedMode,
            onChanged: (value) => setState(() => selectedMode = value!),
          ),
          Text('${response.freeRewardLabel ?? "Pulsuz içki"} Xərclə'),
        ],
      ),

      SizedBox(height: 16),

      // Spend count input (yalnız free reward mode-da)
      if (selectedMode == RedeemMode.freeReward)
        Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Xərcləmə sayı:'),
            Row(
              children: [
                Expanded(
                  child: TextField(
                    keyboardType: TextInputType.number,
                    controller: TextEditingController(text: spendCount.toString()),
                    onChanged: (value) {
                      spendCount = int.tryParse(value) ?? 1;
                      // Clamp to maxSpendCount
                      if (response.maxSpendCount != null) {
                        spendCount = spendCount.clamp(1, response.maxSpendCount!);
                      }
                    },
                  ),
                ),
                if (response.maxSpendCount != null)
                  Text(' / ${response.maxSpendCount}'),
              ],
            ),
          ],
        ),

      SizedBox(height: 16),

      // Redeem button
      ElevatedButton(
        onPressed: () => _startRedeem(
          code: response.cardNumber ?? '',
          delta: selectedMode == RedeemMode.points ? 100 : 0, // Points üçün delta, free reward üçün 0
          spendCount: selectedMode == RedeemMode.freeReward ? spendCount : null,
        ),
        child: Text('Xərclə'),
      ),
    ],
  );
}

enum RedeemMode {
  points,
  freeReward,
}
```

### 2. Start Redeem Request

#### Request Model

```dart
class StartRedeemRequest {
  final String code;
  final int delta;              // Points xərcləməsi üçün (free reward üçün 0)
  final String orderId;
  final String mode;            // "auto", "forcePending", "forceApply"
  final String paymentMethod;   // "card", "cash"
  final int? spendCount;        // YENİ: Free reward xərcləmə sayı (ProgressBased üçün)

  StartRedeemRequest({
    required this.code,
    required this.delta,
    required this.orderId,
    required this.mode,
    required this.paymentMethod,
    this.spendCount,            // Null olarsa, points-based redeem
  });

  Map<String, dynamic> toJson() {
    return {
      'code': code,
      'delta': delta,
      'orderId': orderId,
      'mode': mode,
      'paymentMethod': paymentMethod,
      'spendCount': spendCount, // Null göndərilə bilər
    };
  }
}
```

#### API Call Example

```dart
Future<void> _startRedeem({
  required String code,
  required int delta,
  int? spendCount,
}) async {
  final request = StartRedeemRequest(
    code: code,
    delta: delta,              // Free reward üçün 0
    orderId: 'ORDER_${DateTime.now().millisecondsSinceEpoch}',
    mode: 'auto',
    paymentMethod: 'card',
    spendCount: spendCount,    // ProgressBased üçün: 1, 2, 3, ...
  );

  try {
    final response = await http.post(
      Uri.parse('$baseUrl/api/Redeem/Start'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode(request.toJson()),
    );

    if (response.statusCode == 200) {
      final result = jsonDecode(response.body);
      // Handle success
      // result['data']['remainingRewardCount'] - qalan mükafat sayı
      // result['data']['freeRewardLabel'] - mükafat label-i
    }
  } catch (e) {
    // Handle error
  }
}
```

### 3. Response Handling

#### RedeemResult Model

```dart
class RedeemResult {
  final bool success;
  final String? message;
  final RedeemResultData? data;

  RedeemResult({
    required this.success,
    this.message,
    this.data,
  });

  factory RedeemResult.fromJson(Map<String, dynamic> json) {
    return RedeemResult(
      success: json['success'] ?? false,
      message: json['message'],
      data: json['data'] != null 
          ? RedeemResultData.fromJson(json['data']) 
          : null,
    );
  }
}

class RedeemResultData {
  final int? requestId;
  final int? remainingRewardCount;  // YENİ: Qalan CompletedCycles
  final String? freeRewardLabel;    // YENİ: Mükafat label-i
  final int? appliedDelta;          // YENİ: Tətbiq olunmuş delta (-spendCount)

  RedeemResultData({
    this.requestId,
    this.remainingRewardCount,
    this.freeRewardLabel,
    this.appliedDelta,
  });

  factory RedeemResultData.fromJson(Map<String, dynamic> json) {
    return RedeemResultData(
      requestId: json['requestId'],
      remainingRewardCount: json['remainingRewardCount'],
      freeRewardLabel: json['freeRewardLabel'],
      appliedDelta: json['appliedDelta'],
    );
  }
}
```

## Validasiya Qaydaları

### ProgressBased Kartlar Üçün

1. **Free Reward Xərcləməsi:**
   - `spendCount` 1-dən kiçik ola bilməz
   - `spendCount` `maxSpendCount`-dan böyük ola bilməz
   - `spendCount > completedCycles` olduqda error göstərmək
   - Free reward xərcləməsi zamanı `delta` 0 olmalıdır

2. **Points Xərcləməsi (ProgressBased kartlarda da mövcuddur):**
   - `delta` 0-dan böyük olmalıdır
   - `spendCount` null olmalıdır

3. **UI Validasiya:**
   - Input field-inin maksimum dəyəri `maxSpendCount` ilə məhdudlaşdırılmalıdır
   - Minimum dəyər 1 olmalıdır
   - Free reward count 0-dırsa, free reward redeem button-u disable olmalıdır

## Test Senaryoları

### Senaryo 1: Free Reward Xərcləməsi (ProgressBased)
- **Input:** `spendCount = 1`, `delta = 0`
- **Gözlənilən:** `remainingRewardCount = completedCycles - 1`
- **Response:** `appliedDelta = -1`

### Senaryo 2: Free Reward Xərcləməsi (Maksimum)
- **Input:** `spendCount = maxSpendCount`, `delta = 0`
- **Gözlənilən:** `remainingRewardCount = 0`
- **Response:** `appliedDelta = -maxSpendCount`

### Senaryo 3: Points Xərcləməsi (ProgressBased)
- **Input:** `delta = 100`, `spendCount = null`
- **Gözlənilən:** Normal points redeem kimi işləməlidir
- **Response:** `appliedDelta = calculated points delta`

### Senaryo 4: PointsBased Kart (Mövcud davranış)
- **Input:** `delta = 100`, `spendCount = null`
- **Gözlənilən:** Mövcud davranış qalmalıdır (spendCount ignore edilməlidir)

## Qeydlər

1. **Backward Compatibility:**
   - PointsBased və TierBased kartlar üçün `spendCount` parametri ignore edilir
   - `completedCycles`, `availableRewardCount`, `maxSpendCount` field-ləri yalnız ProgressBased kartlar üçün populate olunur

2. **UI/UX:**
   - Free reward xərcləməsi və points xərcləməsi arasında aydın seçim olmalıdır (radio buttons, tabs, və s.)
   - Free reward sayı 0-dırsa, free reward redeem seçimi disable olmalıdır
   - Input validation real-time olmalıdır (maxSpendCount limit)

3. **Error Handling:**
   - `spendCount > completedCycles` olduqda API error qaytaracaq, UI-da bu halı öncədən yoxlamaq məsləhətdir
   - Network error-ları düzgün handle etmək

4. **Label-lar:**
   - `freeRewardLabel` default olaraq "Pulsuz içki"-dir, lakin template-də fərqli ola bilər (məs: "Burger", "Kofe", və s.)
   - UI-da bu label-i dinamik istifadə etmək (hardcode etməmək)

## Example Response Flow

### 1. LookupByCode Call
```dart
GET /api/Redeem/LookupByCode?code=123456&currentPoints=0

Response:
{
  "success": true,
  "completedCycles": 2,
  "availableRewardCount": 2,
  "maxSpendCount": 2,
  "freeRewardLabel": "Pulsuz içki",
  ...
}
```

### 2. User Selects Free Reward Redeem
- User radio button-da "Free Reward Xərclə"-ni seçir
- Input field-də 1 yazır (max: 2)
- "Xərclə" button-unu basır

### 3. Start Redeem Call
```dart
POST /api/Redeem/Start
{
  "code": "123456",
  "delta": 0,
  "orderId": "ORDER123",
  "mode": "auto",
  "paymentMethod": "card",
  "spendCount": 1
}

Response:
{
  "success": true,
  "data": {
    "remainingRewardCount": 1,
    "freeRewardLabel": "Pulsuz içki",
    "appliedDelta": -1
  }
}
```

### 4. UI Update
- Success mesajı göstərilir
- Kart məlumatları yenilənir (remainingRewardCount = 1)
- Free reward count UI-da yenilənir: "1 Pulsuz içki" (2-dən 1-ə endi)

