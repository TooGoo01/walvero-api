# Flutter UI: ProgressBased Cards Display Prompt

Bu sənəd Flutter UI-da "My Cards" bölməsində ProgressBased kartların düzgün göstərilməsi üçün dəyişiklikləri təsvir edir.

## Məqsəd

API-dən gələn `GetMyCards` response-unda ProgressBased kartlar üçün yeni field-lər əlavə edilib. Bu field-ləri istifadə edərək kart detail-də progress məlumatlarını düzgün göstərmək lazımdır.

## API Response Strukturu

### UserCardDto Response

```dart
class UserCardDto {
  int cardId;
  int programId;
  String programName;
  double currentPoints;
  String cardNumber;
  String barcodeValue;
  
  // YENİ: ProgressBased kartlar üçün
  String? progressText;        // "5/10" formatında
  String? freeRewardText;      // "1 Pulsuz içki" formatında
  
  CardDesignDto design;
  // ...
}

class CardDesignDto {
  String backgroundColor;
  String foregroundColor;
  String labelColor;
  String? heroImageUrl;        // ProgressBased üçün: progress icon URL
  String? logoImageUrl;
  String? stripImageUrl;       // ProgressBased üçün: progress icon URL
  String programName;
  String? tierName;
  String? programDescription;
  String? rewardDescription;
}
```

## Dəyişikliklər

### 1. Card Detail UI-da ProgressBased Kartların Göstərilməsi

**Mövcud vəziyyət:**
- Kart detail-də bütün kart tipləri eyni şəkildə göstərilir
- ProgressBased kartlar üçün progress məlumatı göstərilmir

**Tələb olunan:**
- ProgressBased kartlar üçün progress icon, progress text və free reward text göstərilməlidir
- PointsBased və TierBased kartlar mövcud görünüşdə qalmalıdır

### 2. Kart Tipinin Müəyyən Edilməsi

**Yoxlama:**
- `progressText != null` olduqda → ProgressBased kart
- `progressText == null` olduqda → PointsBased və ya TierBased kart (mövcud UI qalsın)

### 3. ProgressBased Kart Üçün UI Elementləri

#### 3.1 Progress Icon
- **Mənbə:** `design.heroImageUrl` (və ya `design.stripImageUrl`)
- **Yeri:** Kart detail-də üst hissədə böyük icon kimi göstərilməlidir
- **Qeyd:** Hər iki URL eyni progress icon-u göstərir (Google və Apple üçün)

#### 3.2 Progress Text
- **Mənbə:** `progressText` field-i (məs: "5/10")
- **Format:** `currentSteps/totalSteps`
- **Yeri:** Progress icon-un altında və ya yanında göstərilməlidir
- **Design:** Bold, böyük font, görünən rəng

#### 3.3 Free Reward Text
- **Mənbə:** `freeRewardText` field-i (məs: "1 Pulsuz içki")
- **Format:** `{count} {label}`
- **Yeri:** Progress text-in altında və ya yanında
- **Design:** Mükafat məlumatı kimi vurğulanmalıdır

## UI Layout Tövsiyələri

### ProgressBased Kart Detail Layout

```
┌─────────────────────────────────┐
│                                 │
│      [Progress Icon Image]      │  ← design.heroImageUrl
│         (Böyük, mərkəzdə)       │
│                                 │
├─────────────────────────────────┤
│  Progress:  5/10               │  ← progressText
│                                 │
│  Bonus:     1 Pulsuz içki       │  ← freeRewardText
│                                 │
├─────────────────────────────────┤
│  Kart Nömrəsi: 123456          │
│  Barcode: [QR/Barcode]          │
│  ...                            │
└─────────────────────────────────┘
```

### PointsBased/TierBased Kart Detail Layout

```
┌─────────────────────────────────┐
│  [Tier/Points Icon]            │  ← Mövcud icon logic
│                                 │
│  Points: 150                   │  ← currentPoints
│  Tier: Gold                     │  ← design.tierName
│                                 │
├─────────────────────────────────┤
│  Kart Nömrəsi: 123456          │
│  ...                            │
└─────────────────────────────────┘
```

## Flutter Implementation Guide

### 1. Card Detail Widget-ında Type Checking

```dart
class CardDetailWidget extends StatelessWidget {
  final UserCardDto card;
  
  @override
  Widget build(BuildContext context) {
    // ProgressBased kart yoxlaması
    final bool isProgressBased = card.progressText != null;
    
    if (isProgressBased) {
      return _buildProgressBasedCard(card);
    } else {
      return _buildStandardCard(card); // Mövcud UI
    }
  }
}
```

### 2. ProgressBased Card Widget

```dart
Widget _buildProgressBasedCard(UserCardDto card) {
  return Column(
    children: [
      // Progress Icon
      if (card.design.heroImageUrl != null)
        Image.network(
          card.design.heroImageUrl!,
          width: 200,
          height: 200,
          fit: BoxFit.cover,
        ),
      
      SizedBox(height: 16),
      
      // Progress Text (5/10)
      if (card.progressText != null)
        Text(
          card.progressText!,
          style: TextStyle(
            fontSize: 24,
            fontWeight: FontWeight.bold,
            color: Colors.black,
          ),
        ),
      
      SizedBox(height: 8),
      
      // Free Reward Text (1 Pulsuz içki)
      if (card.freeRewardText != null)
        Container(
          padding: EdgeInsets.symmetric(horizontal: 16, vertical: 8),
          decoration: BoxDecoration(
            color: Colors.green.shade100,
            borderRadius: BorderRadius.circular(8),
          ),
          child: Text(
            card.freeRewardText!,
            style: TextStyle(
              fontSize: 18,
              fontWeight: FontWeight.w600,
              color: Colors.green.shade800,
            ),
          ),
        ),
      
      SizedBox(height: 24),
      
      // Kart məlumatları (mövcud UI)
      _buildCardInfo(card),
    ],
  );
}
```

### 3. Progress Icon Fallback

```dart
// Icon URL-i tapmaq üçün helper
String? getProgressIconUrl(CardDesignDto design) {
  // ProgressBased üçün heroImageUrl və stripImageUrl eyni icon-u göstərir
  return design.heroImageUrl ?? design.stripImageUrl;
}
```

### 4. Text Parsing (Əgər lazımdırsa)

```dart
// Progress text-i parse etmək (optional)
class ProgressInfo {
  final int current;
  final int total;
  
  ProgressInfo(this.current, this.total);
  
  static ProgressInfo? parse(String? progressText) {
    if (progressText == null) return null;
    final parts = progressText.split('/');
    if (parts.length != 2) return null;
    return ProgressInfo(
      int.tryParse(parts[0]) ?? 0,
      int.tryParse(parts[1]) ?? 0,
    );
  }
}

// İstifadəsi:
final progressInfo = ProgressInfo.parse(card.progressText);
if (progressInfo != null) {
  // Progress bar göstərmək üçün istifadə et
  LinearProgressIndicator(
    value: progressInfo.current / progressInfo.total,
  );
}
```

## Dizayn Tövsiyələri

### Rənglər
- **Progress Text:** Qara və ya qoyu rəng, bold
- **Free Reward Text:** Yaşıl rəng palitrası (mükafatı vurğulamaq üçün)
- **Background:** Kartın `design.backgroundColor`-unu istifadə et

### Layout
- Progress icon mərkəzdə, böyük ölçüdə (200x200 px və ya responsive)
- Progress text və free reward text icon-un altında, mərkəzləşdirilmiş
- Kart məlumatları (card number, barcode) alt hissədə

### Responsive Design
- Mobil cihazlar üçün: Icon 150x150 px
- Tablet üçün: Icon 200x200 px
- Progress text və free reward text font ölçüləri ekrana uyğun

## Test Senaryoları

### 1. ProgressBased Kart (currentSteps = 5, totalSteps = 10, CompletedCycles = 1)
- **Gözlənilən:**
  - `progressText = "5/10"`
  - `freeRewardText = "1 Pulsuz içki"`
  - `design.heroImageUrl` və `design.stripImageUrl` progress icon URL-i

### 2. ProgressBased Kart (currentSteps = 0, totalSteps = 10, CompletedCycles = 0)
- **Gözlənilən:**
  - `progressText = "0/10"`
  - `freeRewardText = "0 Pulsuz içki"`
  - 0-cı icon göstərilməlidir

### 3. PointsBased Kart
- **Gözlənilən:**
  - `progressText = null`
  - `freeRewardText = null`
  - Mövcud UI görünüşü qalmalıdır

## Qeydlər

1. **Backward Compatibility:** PointsBased və TierBased kartlar üçün `progressText` və `freeRewardText` `null` olacaq, ona görə də mövcud UI pozulmamalıdır.

2. **Icon URL-ləri:** ProgressBased kartlar üçün `design.heroImageUrl` və `design.stripImageUrl` eyni progress icon-u göstərir. Hər ikisindən birini istifadə etmək kifayətdir.

3. **Free Reward Text:** Həmişə göstərilməlidir, hətta `"0 Pulsuz içki"` olsa belə (boş qalmamalıdır).

4. **Progress Icon:** Icon URL `null` ola bilər (əgər icon-lar yüklənməyibsə). Bu halda icon göstərməmək və ya placeholder istifadə etmək məsləhətdir.

## Example Response

```json
{
  "cardId": 123,
  "programId": 1,
  "programName": "Coffee Shop Loyalty",
  "currentPoints": 5,
  "progressText": "5/10",
  "freeRewardText": "1 Pulsuz içki",
  "design": {
    "heroImageUrl": "https://example.com/icons/progress-5.png",
    "stripImageUrl": "https://example.com/icons/progress-5.png",
    "backgroundColor": "#FFFFFF",
    "foregroundColor": "#000000",
    "programName": "Coffee Shop Loyalty",
    "rewardDescription": "Get 1 FREE coffee"
  }
}
```


