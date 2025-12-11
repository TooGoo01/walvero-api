# Flutter - "Feedback-lərim" Tərcüməsi

## Tapşırıq
Profile səhifəsində "Feedback-lərim" yazısını tərcümə etmək (multilingual dəstək əlavə etmək).

## Flutter Localization Setup

### 1. Dependencies Əlavəsi

`pubspec.yaml` faylına aşağıdakı dependencies əlavə edin:

```yaml
dependencies:
  flutter:
    sdk: flutter
  flutter_localizations:
    sdk: flutter
  intl: ^0.19.0

flutter:
  generate: true
```

### 2. l10n.yaml Konfiqurasiyası

Proyektin root qovluğunda `l10n.yaml` faylı yaradın:

```yaml
arb-dir: lib/l10n
template-arb-file: app_en.arb
output-localization-file: app_localizations.dart
```

### 3. Localization ARB Faylları

`lib/l10n/` qovluğunda aşağıdakı faylları yaradın:

**lib/l10n/app_en.arb** (İngiliscə):
```json
{
  "@@locale": "en",
  "myFeedbacks": "My Feedbacks",
  "@myFeedbacks": {
    "description": "Title for feedback list page"
  },
  "feedback": "Feedback",
  "@feedback": {
    "description": "General feedback term"
  },
  "sendFeedback": "Send Feedback",
  "@sendFeedback": {
    "description": "Button text to send feedback"
  },
  "noFeedback": "No feedback",
  "@noFeedback": {
    "description": "Message when there are no feedbacks"
  },
  "suggestion": "Suggestion",
  "complaint": "Complaint",
  "rating": "Rating",
  "message": "Message",
  "close": "Close",
  "closed": "Closed",
  "open": "Open"
}
```

**lib/l10n/app_az.arb** (Azərbaycanca):
```json
{
  "@@locale": "az",
  "myFeedbacks": "Feedback-lərim",
  "feedback": "Feedback",
  "sendFeedback": "Feedback göndər",
  "noFeedback": "Feedback yoxdur",
  "suggestion": "Təklif",
  "complaint": "Şikayət",
  "rating": "Dəyərləndirmə",
  "message": "Mesaj",
  "close": "Bağla",
  "closed": "Bağlı",
  "open": "Açıq"
}
```

**lib/l10n/app_ru.arb** (Rusca - istəyə bağlı):
```json
{
  "@@locale": "ru",
  "myFeedbacks": "Мои отзывы",
  "feedback": "Отзыв",
  "sendFeedback": "Отправить отзыв",
  "noFeedback": "Нет отзывов",
  "suggestion": "Предложение",
  "complaint": "Жалоба",
  "rating": "Оценка",
  "message": "Сообщение",
  "close": "Закрыть",
  "closed": "Закрыто",
  "open": "Открыто"
}
```

### 4. MaterialApp-də Localization Konfiqurasiyası

`main.dart` və ya əsas MaterialApp widget-ında:

```dart
import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_gen/gen_l10n/app_localizations.dart';

void main() {
  runApp(const MyApp());
}

class MyApp extends StatelessWidget {
  const MyApp({Key? key}) : super(key: key);

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Loyalty App',
      // Localization konfiqurasiyası
      localizationsDelegates: const [
        AppLocalizations.delegate,
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
      supportedLocales: const [
        Locale('en', ''), // İngiliscə
        Locale('az', ''), // Azərbaycanca
        Locale('ru', ''), // Rusca
      ],
      locale: const Locale('az'), // Default dil (və ya device locale-dən götür)
      // ... digər konfiqurasiyalar
      home: const HomeScreen(),
    );
  }
}
```

### 5. My Feedbacks Screen-də Tərcümə İstifadəsi

```dart
// screens/my_feedbacks_screen.dart

import 'package:flutter/material.dart';
import 'package:flutter_gen/gen_l10n/app_localizations.dart';
import '../services/feedback_service.dart';
import '../models/feedback_models.dart';

class MyFeedbacksScreen extends StatefulWidget {
  final int? tenantId;

  const MyFeedbacksScreen({
    Key? key,
    this.tenantId,
  }) : super(key: key);

  @override
  _MyFeedbacksScreenState createState() => _MyFeedbacksScreenState();
}

class _MyFeedbacksScreenState extends State<MyFeedbacksScreen> {
  final FeedbackService _feedbackService = FeedbackService(
    baseUrl: 'YOUR_BASE_URL',
    getToken: () async => 'YOUR_TOKEN',
  );

  List<Feedback> _feedbacks = [];
  bool _isLoading = true;
  FeedbackStatus? _selectedStatus;
  int? _selectedTenantId;

  @override
  void initState() {
    super.initState();
    _selectedTenantId = widget.tenantId;
    _loadFeedbacks();
  }

  Future<void> _loadFeedbacks() async {
    setState(() {
      _isLoading = true;
    });

    try {
      final feedbacks = await _feedbackService.getMyFeedbacks(
        status: _selectedStatus,
        tenantId: _selectedTenantId,
      );
      setState(() {
        _feedbacks = feedbacks;
        _isLoading = false;
      });
    } catch (e) {
      setState(() {
        _isLoading = false;
      });
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Xəta: ${e.toString()}'),
          backgroundColor: Colors.red,
        ),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context)!; // Tərcümə obyekti

    return Scaffold(
      appBar: AppBar(
        title: Text(l10n.myFeedbacks), // "Feedback-lərim" tərcüməsi
        actions: [
          PopupMenuButton<String>(
            icon: const Icon(Icons.filter_list),
            onSelected: (value) {
              if (value == 'all') {
                setState(() {
                  _selectedTenantId = null;
                });
              }
              _loadFeedbacks();
            },
            itemBuilder: (BuildContext context) => [
              PopupMenuItem<String>(
                value: 'all',
                child: Text(l10n.allStores ?? 'Bütün mağazalar'), // Tərcümə
              ),
            ],
          ),
        ],
      ),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator())
          : _feedbacks.isEmpty
              ? Center(child: Text(l10n.noFeedback)) // "Feedback yoxdur" tərcüməsi
              : RefreshIndicator(
                  onRefresh: _loadFeedbacks,
                  child: ListView.builder(
                    itemCount: _feedbacks.length,
                    itemBuilder: (context, index) {
                      final feedback = _feedbacks[index];
                      return Card(
                        margin: const EdgeInsets.all(8),
                        child: ExpansionTile(
                          title: Text(feedback.tenantName),
                          subtitle: Text(
                            feedback.type == FeedbackType.suggestion
                                ? l10n.suggestion // "Təklif" tərcüməsi
                                : l10n.complaint, // "Şikayət" tərcüməsi
                          ),
                          trailing: feedback.status == FeedbackStatus.open
                              ? IconButton(
                                  icon: const Icon(Icons.close),
                                  tooltip: l10n.close, // "Bağla" tərcüməsi
                                  onPressed: () => _closeFeedback(feedback.id),
                                )
                              : Chip(
                                  label: Text(l10n.closed), // "Bağlı" tərcüməsi
                                  backgroundColor: Colors.grey,
                                ),
                          children: [
                            Padding(
                              padding: const EdgeInsets.all(16.0),
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  if (feedback.rating != null)
                                    Row(
                                      children: [
                                        Text('${l10n.rating}: '), // "Dəyərləndirmə" tərcüməsi
                                        ...List.generate(5, (i) {
                                          return Icon(
                                            i < feedback.rating!.round()
                                                ? Icons.star
                                                : Icons.star_border,
                                            color: Colors.amber,
                                            size: 20,
                                          );
                                        }),
                                        const SizedBox(width: 8),
                                        Text('${feedback.rating!.toStringAsFixed(1)}/5'),
                                      ],
                                    ),
                                  const SizedBox(height: 8),
                                  Text(
                                    '${l10n.message}:', // "Mesaj" tərcüməsi
                                    style: Theme.of(context).textTheme.titleSmall,
                                  ),
                                  const SizedBox(height: 4),
                                  Text(feedback.message),
                                  const SizedBox(height: 16),
                                  if (feedback.replies.isNotEmpty) ...[
                                    const Divider(),
                                    Text(
                                      'Cavablar:', // Bu da tərcümə edilə bilər
                                      style: Theme.of(context).textTheme.titleSmall,
                                    ),
                                    // ... digər kod
                                  ],
                                ],
                              ),
                            ),
                          ],
                        ),
                      );
                    },
                  ),
                ),
    );
  }

  Future<void> _closeFeedback(int feedbackId) async {
    final l10n = AppLocalizations.of(context)!;
    
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(l10n.closeFeedback ?? 'Feedback-i Bağla'),
        content: Text(l10n.closeFeedbackConfirmation ?? 'Bu feedback-i bağlamaq istədiyinizə əminsiniz?'),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: Text(l10n.cancel ?? 'Ləğv et'),
          ),
          ElevatedButton(
            onPressed: () => Navigator.of(context).pop(true),
            child: Text(l10n.close),
          ),
        ],
      ),
    );

    if (confirmed != true) return;

    try {
      await _feedbackService.closeFeedback(feedbackId);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(l10n.feedbackClosed ?? 'Feedback bağlandı'),
          backgroundColor: Colors.green,
        ),
      );
      _loadFeedbacks();
    } catch (e) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Xəta: ${e.toString()}'),
          backgroundColor: Colors.red,
        ),
      );
    }
  }
}
```

### 6. Feedback Form Dialog-da Tərcümə

```dart
// widgets/feedback_form_dialog.dart

import 'package:flutter/material.dart';
import 'package:flutter_gen/gen_l10n/app_localizations.dart';
import 'package:flutter_rating_bar/flutter_rating_bar.dart';
import '../models/feedback_models.dart';

class FeedbackFormDialog extends StatefulWidget {
  final int tenantId;
  final String tenantName;

  const FeedbackFormDialog({
    Key? key,
    required this.tenantId,
    required this.tenantName,
  }) : super(key: key);

  @override
  _FeedbackFormDialogState createState() => _FeedbackFormDialogState();
}

class _FeedbackFormDialogState extends State<FeedbackFormDialog> {
  final _formKey = GlobalKey<FormState>();
  final _messageController = TextEditingController();
  FeedbackType _selectedType = FeedbackType.suggestion;
  double? _rating;
  bool _sharePhoneNumber = false;

  @override
  void dispose() {
    _messageController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context)!;

    return Dialog(
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(16),
      ),
      child: Container(
        padding: const EdgeInsets.all(20),
        constraints: const BoxConstraints(maxWidth: 500, maxHeight: 600),
        child: SingleChildScrollView(
          child: Form(
            key: _formKey,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Text(
                      l10n.sendFeedback, // "Feedback göndər" tərcüməsi
                      style: Theme.of(context).textTheme.titleLarge,
                    ),
                    IconButton(
                      icon: const Icon(Icons.close),
                      onPressed: () => Navigator.of(context).pop(),
                    ),
                  ],
                ),
                const SizedBox(height: 8),
                Text(
                  widget.tenantName,
                  style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                        color: Colors.grey[600],
                      ),
                ),
                const SizedBox(height: 24),
                
                // Feedback Type Selection
                Text(
                  '${l10n.feedback} ${l10n.type ?? 'Tipi'}', // "Feedback Tipi"
                  style: Theme.of(context).textTheme.titleMedium,
                ),
                const SizedBox(height: 8),
                Row(
                  children: [
                    Expanded(
                      child: RadioListTile<FeedbackType>(
                        title: Text(l10n.suggestion), // "Təklif"
                        value: FeedbackType.suggestion,
                        groupValue: _selectedType,
                        onChanged: (value) {
                          setState(() {
                            _selectedType = value!;
                          });
                        },
                      ),
                    ),
                    Expanded(
                      child: RadioListTile<FeedbackType>(
                        title: Text(l10n.complaint), // "Şikayət"
                        value: FeedbackType.complaint,
                        groupValue: _selectedType,
                        onChanged: (value) {
                          setState(() {
                            _selectedType = value!;
                          });
                        },
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 16),

                // Rating
                Text(
                  '${l10n.rating} (${l10n.optional ?? 'İstəyə bağlı'})', // "Dəyərləndirmə (İstəyə bağlı)"
                  style: Theme.of(context).textTheme.titleMedium,
                ),
                // ... rating bar kod
                const SizedBox(height: 24),

                // Message Field
                TextFormField(
                  controller: _messageController,
                  decoration: InputDecoration(
                    labelText: '${l10n.message} *', // "Mesaj *"
                    hintText: l10n.feedbackMessageHint ?? 'Feedback mesajınızı yazın...',
                    border: const OutlineInputBorder(),
                  ),
                  maxLines: 5,
                  maxLength: 2000,
                  validator: (value) {
                    if (value == null || value.trim().isEmpty) {
                      return l10n.messageRequired ?? 'Mesaj daxil edilməlidir';
                    }
                    return null;
                  },
                ),
                const SizedBox(height: 16),

                // Share Phone Number Checkbox
                CheckboxListTile(
                  title: Text(l10n.sharePhoneNumber ?? 'Telefon nömrəmi paylaş'),
                  value: _sharePhoneNumber,
                  onChanged: (value) {
                    setState(() {
                      _sharePhoneNumber = value ?? false;
                    });
                  },
                ),
                const SizedBox(height: 24),

                // Submit Button
                SizedBox(
                  width: double.infinity,
                  child: ElevatedButton(
                    onPressed: () {
                      if (_formKey.currentState!.validate()) {
                        Navigator.of(context).pop({
                          'type': _selectedType,
                          'message': _messageController.text.trim(),
                          'rating': _rating,
                          'sharePhoneNumber': _sharePhoneNumber,
                        });
                      }
                    },
                    style: ElevatedButton.styleFrom(
                      padding: const EdgeInsets.symmetric(vertical: 16),
                    ),
                    child: Text(l10n.send), // "Göndər" tərcüməsi
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
```

### 7. ARB Faylına Əlavə Key-lər

**lib/l10n/app_az.arb** (tam versiya):
```json
{
  "@@locale": "az",
  "myFeedbacks": "Feedback-lərim",
  "feedback": "Feedback",
  "sendFeedback": "Feedback göndər",
  "noFeedback": "Feedback yoxdur",
  "suggestion": "Təklif",
  "complaint": "Şikayət",
  "rating": "Dəyərləndirmə",
  "message": "Mesaj",
  "close": "Bağla",
  "closed": "Bağlı",
  "open": "Açıq",
  "type": "Tipi",
  "optional": "İstəyə bağlı",
  "messageRequired": "Mesaj daxil edilməlidir",
  "send": "Göndər",
  "cancel": "Ləğv et",
  "closeFeedback": "Feedback-i Bağla",
  "closeFeedbackConfirmation": "Bu feedback-i bağlamaq istədiyinizə əminsiniz?",
  "feedbackClosed": "Feedback bağlandı",
  "sharePhoneNumber": "Telefon nömrəmi paylaş",
  "feedbackMessageHint": "Feedback mesajınızı yazın...",
  "allStores": "Bütün mağazalar",
  "replies": "Cavablar",
  "store": "Mağaza",
  "you": "Siz"
}
```

### 8. Code Generation

Terminal-də aşağıdakı komandanı işə salın:

```bash
flutter gen-l10n
```

Və ya:

```bash
flutter pub get
```

Bu komanda `lib/l10n/` qovluğundakı ARB fayllarından `app_localizations.dart` faylını generate edəcək.

### 9. Device Locale-dən Avtomatik Dil Seçimi

```dart
import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';

class MyApp extends StatelessWidget {
  const MyApp({Key? key}) : super(key: key);

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Loyalty App',
      localizationsDelegates: const [
        AppLocalizations.delegate,
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
      supportedLocales: const [
        Locale('en', ''),
        Locale('az', ''),
        Locale('ru', ''),
      ],
      // Device locale-dən avtomatik dil seçimi
      locale: null, // null = device locale istifadə et
      // Və ya spesifik locale:
      // locale: const Locale('az', ''),
      home: const HomeScreen(),
    );
  }
}
```

### 10. Runtime-də Dil Dəyişdirmə

```dart
// services/locale_service.dart

import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';

class LocaleService {
  static const String _localeKey = 'selected_locale';

  static Future<Locale?> getSavedLocale() async {
    final prefs = await SharedPreferences.getInstance();
    final localeString = prefs.getString(_localeKey);
    if (localeString != null) {
      final parts = localeString.split('_');
      return Locale(parts[0], parts.length > 1 ? parts[1] : '');
    }
    return null;
  }

  static Future<void> saveLocale(Locale locale) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_localeKey, '${locale.languageCode}_${locale.countryCode ?? ''}');
  }
}

// main.dart
class MyApp extends StatefulWidget {
  const MyApp({Key? key}) : super(key: key);

  @override
  State<MyApp> createState() => _MyAppState();
}

class _MyAppState extends State<MyApp> {
  Locale? _locale;

  @override
  void initState() {
    super.initState();
    _loadLocale();
  }

  Future<void> _loadLocale() async {
    final savedLocale = await LocaleService.getSavedLocale();
    setState(() {
      _locale = savedLocale;
    });
  }

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Loyalty App',
      localizationsDelegates: const [
        AppLocalizations.delegate,
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
      supportedLocales: const [
        Locale('en', ''),
        Locale('az', ''),
        Locale('ru', ''),
      ],
      locale: _locale,
      home: const HomeScreen(),
    );
  }
}

// Settings screen-də dil dəyişdirmə
class SettingsScreen extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Tənzimləmələr')),
      body: ListView(
        children: [
          ListTile(
            title: const Text('Dil'),
            subtitle: const Text('Azərbaycanca'),
            onTap: () async {
              await LocaleService.saveLocale(const Locale('az'));
              // App-i restart etmək lazımdır
            },
          ),
          ListTile(
            title: const Text('Language'),
            subtitle: const Text('English'),
            onTap: () async {
              await LocaleService.saveLocale(const Locale('en'));
            },
          ),
        ],
      ),
    );
  }
}
```

## Əlavə Qeydlər

1. **Code Generation:** `flutter gen-l10n` komandasını işə saldıqdan sonra `AppLocalizations` class-ı avtomatik generate olunur.

2. **Null Safety:** `AppLocalizations.of(context)!` istifadə edərkən `!` operatoru istifadə edirik, çünki MaterialApp-də localization delegate-ləri təmin edilmişdir.

3. **Missing Translations:** Əgər bir key tərcümə edilməyibsə, Flutter default locale-dən (en) götürəcək.

4. **Pluralization:** ARB fayllarında plural form-lar da əlavə edə bilərsiniz:

```json
{
  "feedbackCount": "{count, plural, =0{No feedback} =1{One feedback} other{{count} feedbacks}}",
  "@feedbackCount": {
    "placeholders": {
      "count": {
        "type": "int"
      }
    }
  }
}
```

5. **Test:** Fərqli dillərdə test edin və bütün yazıların düzgün tərcümə edildiyini yoxlayın.

