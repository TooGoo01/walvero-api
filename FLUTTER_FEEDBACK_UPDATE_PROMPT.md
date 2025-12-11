# Flutter - Feedback API Dəyişiklikləri (Update)

## Əsas Dəyişikliklər

Feedback API-də aşağıdakı dəyişikliklər edildi:

### 1. GetMyFeedbacks Endpoint Dəyişikliyi

**Əvvəlki endpoint:**
```
GET /api/Feedback/GetMyFeedbacks?status=0
```

**Yeni endpoint:**
```
GET /api/Feedback/GetMyFeedbacks?status=0&tenantId=1
```

**Dəyişiklik:**
- `tenantId` query parametri əlavə edildi (optional)
- Əgər `tenantId` göndərilərsə: yalnız həmin tenant üçün feedback-lər qaytarılır
- Əgər `tenantId` göndərilməzsə: bütün tenant-lar üçün feedback-lər qaytarılır

**Səbəb:** Bir application user-in müxtəlif tenant-larda müxtəlif customer record-ları ola bilər. Ona görə də tenant ID-siz customer tapıla bilməz.

### 2. CustomerReply və CloseFeedback Endpoint-ləri

Bu endpoint-lər dəyişməyib, amma backend məntiq dəyişib:
- Feedback-dən tenant ID götürülür
- O tenant üçün customer tapılır

Flutter tərəfində dəyişiklik lazım deyil, amma error handling yaxşılaşdırıla bilər.

## Flutter İmplementasiyası

### 1. Feedback Service Metodunun Yenilənməsi

```dart
// services/feedback_service.dart

Future<List<Feedback>> getMyFeedbacks({
  FeedbackStatus? status,
  int? tenantId, // YENİ: Optional tenant ID parametri
}) async {
  try {
    String url = '$baseUrl/api/Feedback/GetMyFeedbacks';
    List<String> queryParams = [];
    
    if (status != null) {
      queryParams.add('status=${status.value}');
    }
    
    // YENİ: tenantId parametri əlavə et
    if (tenantId != null) {
      queryParams.add('tenantId=$tenantId');
    }
    
    if (queryParams.isNotEmpty) {
      url += '?${queryParams.join('&')}';
    }

    final response = await http.get(
      Uri.parse(url),
      headers: {
        'Authorization': 'Bearer ${await getToken()}',
        'Content-Type': 'application/json',
      },
    );

    if (response.statusCode == 200) {
      final data = json.decode(response.body) as List<dynamic>;
      return data.map((json) => Feedback.fromJson(json)).toList();
    } else {
      throw Exception('Failed to get feedbacks: ${response.body}');
    }
  } catch (e) {
    throw Exception('Error getting feedbacks: $e');
  }
}
```

### 2. My Feedbacks Screen-də Tenant Filter Əlavəsi

```dart
// screens/my_feedbacks_screen.dart

import 'package:flutter/material.dart';
import '../services/feedback_service.dart';
import '../models/feedback_models.dart';

class MyFeedbacksScreen extends StatefulWidget {
  final int? tenantId; // YENİ: Optional tenant ID

  const MyFeedbacksScreen({
    Key? key,
    this.tenantId, // YENİ: Tenant ID parametri
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
  int? _selectedTenantId; // YENİ: Seçilmiş tenant ID

  @override
  void initState() {
    super.initState();
    _selectedTenantId = widget.tenantId; // YENİ: Initial tenant ID
    _loadFeedbacks();
  }

  Future<void> _loadFeedbacks() async {
    setState(() {
      _isLoading = true;
    });

    try {
      // YENİ: tenantId parametri göndər
      final feedbacks = await _feedbackService.getMyFeedbacks(
        status: _selectedStatus,
        tenantId: _selectedTenantId, // YENİ: Tenant ID göndər
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
    return Scaffold(
      appBar: AppBar(
        title: const Text('Feedback-lərim'),
        actions: [
          // YENİ: Tenant filter dropdown
          PopupMenuButton<String>(
            icon: const Icon(Icons.filter_list),
            onSelected: (value) {
              if (value == 'all') {
                setState(() {
                  _selectedTenantId = null;
                });
              } else {
                // Burada tenant listindən seçim edilə bilər
                // Məsələn, My Cards-dan gələn tenant ID
              }
              _loadFeedbacks();
            },
            itemBuilder: (BuildContext context) => [
              const PopupMenuItem<String>(
                value: 'all',
                child: Text('Bütün mağazalar'),
              ),
              // Burada tenant listi göstərilə bilər
            ],
          ),
        ],
      ),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator())
          : _feedbacks.isEmpty
              ? const Center(child: Text('Feedback yoxdur'))
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
                                ? 'Təklif'
                                : 'Şikayət',
                          ),
                          // ... digər kod
                        ),
                      );
                    },
                  ),
                ),
    );
  }
}
```

### 3. My Cards Screen-dən Feedback Səhifəsinə Keçid

```dart
// screens/my_cards_screen.dart (əlavə ediləcək hissə)

// Feedback icon-a basılanda tenant ID ilə feedback səhifəsinə keç
IconButton(
  icon: const Icon(Icons.feedback),
  tooltip: 'Feedback-ləri gör',
  onPressed: () {
    Navigator.of(context).push(
      MaterialPageRoute(
        builder: (context) => MyFeedbacksScreen(
          tenantId: card.tenantId, // YENİ: Tenant ID göndər
        ),
      ),
    );
  },
),
```

### 4. Feedback Form Dialog-dan Sonra Tenant ID ilə Feedback-ləri Yenilə

```dart
// screens/my_cards_screen.dart (əlavə ediləcək hissə)

Future<void> _showFeedbackForm(int tenantId, String tenantName) async {
  final result = await showDialog<Map<String, dynamic>>(
    context: context,
    builder: (context) => FeedbackFormDialog(
      tenantId: tenantId,
      tenantName: tenantName,
    ),
  );

  if (result != null) {
    // Loading göstər
    showDialog(
      context: context,
      barrierDismissible: false,
      builder: (context) => const Center(child: CircularProgressIndicator()),
    );

    try {
      final request = CreateFeedbackRequest(
        tenantId: tenantId,
        type: result['type'] as FeedbackType,
        message: result['message'] as String,
        rating: result['rating'] as double?,
        sharePhoneNumber: result['sharePhoneNumber'] as bool,
      );

      await _feedbackService.createFeedback(request);

      Navigator.pop(context); // Loading dialog-u bağla

      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Feedback uğurla göndərildi'),
          backgroundColor: Colors.green,
        ),
      );
      
      // YENİ: Feedback göndərildikdən sonra feedback səhifəsinə keç və tenant ID göndər
      Navigator.of(context).push(
        MaterialPageRoute(
          builder: (context) => MyFeedbacksScreen(
            tenantId: tenantId, // YENİ: Tenant ID göndər
          ),
        ),
      );
    } catch (e) {
      Navigator.pop(context); // Loading dialog-u bağla

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

### 5. Error Handling Yaxşılaşdırması

```dart
// services/feedback_service.dart (əlavə ediləcək)

Future<void> closeFeedback(int feedbackId) async {
  try {
    final response = await http.post(
      Uri.parse('$baseUrl/api/Feedback/CloseFeedback?id=$feedbackId'),
      headers: {
        'Authorization': 'Bearer ${await getToken()}',
        'Content-Type': 'application/json',
      },
    );

    if (response.statusCode == 200) {
      return; // Uğurlu
    } else {
      final errorBody = json.decode(response.body);
      final errorMessage = errorBody['message'] ?? 'Failed to close feedback';
      
      // YENİ: Xüsusi error mesajları
      if (errorMessage.contains('Customer profile not found for this tenant')) {
        throw Exception('Bu tenant üçün customer profili tapılmadı');
      } else if (errorMessage.contains('Feedback not found')) {
        throw Exception('Feedback tapılmadı');
      } else {
        throw Exception(errorMessage);
      }
    }
  } catch (e) {
    throw Exception('Error closing feedback: $e');
  }
}

Future<FeedbackReply> customerReply(int feedbackId, String message) async {
  try {
    final response = await http.post(
      Uri.parse('$baseUrl/api/Feedback/CustomerReply?id=$feedbackId'),
      headers: {
        'Authorization': 'Bearer ${await getToken()}',
        'Content-Type': 'application/json',
      },
      body: json.encode({
        'message': message,
      }),
    );

    if (response.statusCode == 200) {
      final data = json.decode(response.body);
      return FeedbackReply.fromJson(data);
    } else {
      final errorBody = json.decode(response.body);
      final errorMessage = errorBody['message'] ?? 'Failed to reply to feedback';
      
      // YENİ: Xüsusi error mesajları
      if (errorMessage.contains('Customer profile not found for this tenant')) {
        throw Exception('Bu tenant üçün customer profili tapılmadı');
      } else if (errorMessage.contains('Feedback not found')) {
        throw Exception('Feedback tapılmadı');
      } else {
        throw Exception(errorMessage);
      }
    }
  } catch (e) {
    throw Exception('Error replying to feedback: $e');
  }
}
```

## Test Edilməli Hallar

- ✅ GetMyFeedbacks tenantId ilə çağırılması
- ✅ GetMyFeedbacks tenantId olmadan çağırılması (bütün tenant-lar)
- ✅ My Cards-dan tenant ID ilə feedback səhifəsinə keçid
- ✅ Feedback göndərildikdən sonra tenant ID ilə feedback səhifəsinə keçid
- ✅ Tenant filter dropdown-unun işləməsi
- ✅ Error handling - "Customer profile not found for this tenant" mesajı
- ✅ CustomerReply tenant ID ilə düzgün işləməsi
- ✅ CloseFeedback tenant ID ilə düzgün işləməsi

## Əlavə Qeydlər

1. **Tenant ID Mənbəyi:** Tenant ID-ni My Cards səhifəsindən və ya başqa yerdən götürə bilərsiniz.

2. **Filter UI:** İstəyə görə tenant filter dropdown-u əlavə edə bilərsiniz ki, istifadəçi müxtəlif tenant-ların feedback-lərini görə bilsin.

3. **Navigation:** Feedback göndərildikdən sonra avtomatik olaraq feedback səhifəsinə keçid edə bilərsiniz və tenant ID göndərə bilərsiniz.

4. **Error Messages:** Backend-dən gələn xüsusi error mesajlarını istifadəçiyə daha yaxşı göstərə bilərsiniz.

