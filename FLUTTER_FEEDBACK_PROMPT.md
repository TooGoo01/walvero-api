# Flutter - My Cards Bölməsində Feedback Funksionallığı

## Tapşırıq
"My Cards" bölməsində hər bir tenant (mağaza) kartının altında feedback göndərmək üçün icon əlavə edilməlidir. İstifadəçi icon-a basdıqda feedback formu açılmalıdır. Form-da mesaj və ulduzlarla dəyərləndirmə (rating) olmalıdır. Müştərinin feedback-inə mağaza tərəfindən cavab verildikdə bildirim gəlməlidir və müştəri feedback-i close edə bilməlidir.

## API Məlumatları

### 1. Feedback Yaratmaq
**Endpoint:** `POST /api/Feedback/CreateFeedback`

**Authentication:** JWT Token tələb olunur (Bearer token header-də göndərilməlidir)

**Request Body:**
```json
{
  "tenantId": 1,
  "type": 0,  // 0 = Suggestion, 1 = Complaint
  "message": "Feedback mesajı burada",
  "rating": 4.5,  // 0-5 arası, nullable
  "sharePhoneNumber": false
}
```

**Response:**
```json
{
  "id": 1,
  "tenantId": 1,
  "tenantName": "Mağaza Adı",
  "customerId": 1,
  "customerName": "Müştəri Adı",
  "type": 0,
  "message": "Feedback mesajı",
  "rating": 4.5,
  "sharePhoneNumber": false,
  "status": 0,  // 0 = Open, 1 = Closed
  "createdAt": "2025-01-15T10:30:00Z",
  "updatedAt": null,
  "replies": []
}
```

### 2. Müştərinin Feedback-lərini Almaq
**Endpoint:** `GET /api/Feedback/GetMyFeedbacks?status=0` (status optional: 0=Open, 1=Closed)

**Response:**
```json
[
  {
    "id": 1,
    "tenantId": 1,
    "tenantName": "Mağaza Adı",
    "customerId": 1,
    "customerName": "Müştəri Adı",
    "type": 0,
    "message": "Feedback mesajı",
    "rating": 4.5,
    "status": 0,
    "createdAt": "2025-01-15T10:30:00Z",
    "replies": [
      {
        "id": 1,
        "storeFeedbackId": 1,
        "senderType": 0,  // 0 = Store, 1 = Customer
        "message": "Cavab mesajı",
        "createdAt": "2025-01-15T11:00:00Z"
      }
    ]
  }
]
```

### 3. Feedback Detallarını Almaq
**Endpoint:** `GET /api/Feedback/GetFeedbackDetails?id=1`

**Response:** GetMyFeedbacks ilə eyni format

### 4. Feedback-i Bağlamaq (Close)
**Endpoint:** `POST /api/Feedback/CloseFeedback?id=1`

**Response:**
```json
{
  "message": "Feedback closed successfully."
}
```

### 5. Bildirimləri Almaq (Notification)
**Endpoint:** `GET /api/Notification/GetMyNotifications` (və ya oxşar endpoint)

**Response (təxmini):**
```json
[
  {
    "id": 1,
    "userId": 1,
    "title": "New reply from the store",
    "body": "The store responded to your suggestion.",
    "isRead": false,
    "createdAt": "2025-01-15T11:00:00Z",
    "relatedFeedbackId": 1
  }
]
```

## Flutter İmplementasiyası

### 1. Model Class-ları

```dart
// models/feedback_models.dart

enum FeedbackType {
  suggestion(0),
  complaint(1);

  final int value;
  const FeedbackType(this.value);

  static FeedbackType fromInt(int value) {
    return FeedbackType.values.firstWhere(
      (e) => e.value == value,
      orElse: () => FeedbackType.suggestion,
    );
  }
}

enum FeedbackStatus {
  open(0),
  closed(1);

  final int value;
  const FeedbackStatus(this.value);

  static FeedbackStatus fromInt(int value) {
    return FeedbackStatus.values.firstWhere(
      (e) => e.value == value,
      orElse: () => FeedbackStatus.open,
    );
  }
}

enum FeedbackReplySenderType {
  store(0),
  customer(1);

  final int value;
  const FeedbackReplySenderType(this.value);

  static FeedbackReplySenderType fromInt(int value) {
    return FeedbackReplySenderType.values.firstWhere(
      (e) => e.value == value,
      orElse: () => FeedbackReplySenderType.store,
    );
  }
}

class CreateFeedbackRequest {
  final int tenantId;
  final FeedbackType type;
  final String message;
  final double? rating;
  final bool sharePhoneNumber;

  CreateFeedbackRequest({
    required this.tenantId,
    required this.type,
    required this.message,
    this.rating,
    this.sharePhoneNumber = false,
  });

  Map<String, dynamic> toJson() {
    return {
      'tenantId': tenantId,
      'type': type.value,
      'message': message,
      'rating': rating,
      'sharePhoneNumber': sharePhoneNumber,
    };
  }
}

class FeedbackReply {
  final int id;
  final int storeFeedbackId;
  final FeedbackReplySenderType senderType;
  final String message;
  final DateTime createdAt;

  FeedbackReply({
    required this.id,
    required this.storeFeedbackId,
    required this.senderType,
    required this.message,
    required this.createdAt,
  });

  factory FeedbackReply.fromJson(Map<String, dynamic> json) {
    return FeedbackReply(
      id: json['id'],
      storeFeedbackId: json['storeFeedbackId'],
      senderType: FeedbackReplySenderType.fromInt(json['senderType']),
      message: json['message'],
      createdAt: DateTime.parse(json['createdAt']),
    );
  }
}

class Feedback {
  final int id;
  final int tenantId;
  final String tenantName;
  final int customerId;
  final String customerName;
  final FeedbackType type;
  final String message;
  final double? rating;
  final bool sharePhoneNumber;
  final FeedbackStatus status;
  final DateTime createdAt;
  final DateTime? updatedAt;
  final List<FeedbackReply> replies;

  Feedback({
    required this.id,
    required this.tenantId,
    required this.tenantName,
    required this.customerId,
    required this.customerName,
    required this.type,
    required this.message,
    this.rating,
    required this.sharePhoneNumber,
    required this.status,
    required this.createdAt,
    this.updatedAt,
    required this.replies,
  });

  factory Feedback.fromJson(Map<String, dynamic> json) {
    return Feedback(
      id: json['id'],
      tenantId: json['tenantId'],
      tenantName: json['tenantName'] ?? '',
      customerId: json['customerId'],
      customerName: json['customerName'] ?? '',
      type: FeedbackType.fromInt(json['type']),
      message: json['message'] ?? '',
      rating: json['rating']?.toDouble(),
      sharePhoneNumber: json['sharePhoneNumber'] ?? false,
      status: FeedbackStatus.fromInt(json['status']),
      createdAt: DateTime.parse(json['createdAt']),
      updatedAt: json['updatedAt'] != null ? DateTime.parse(json['updatedAt']) : null,
      replies: (json['replies'] as List<dynamic>?)
          ?.map((r) => FeedbackReply.fromJson(r))
          .toList() ?? [],
    );
  }
}

class Notification {
  final int id;
  final int userId;
  final String title;
  final String body;
  final bool isRead;
  final DateTime createdAt;
  final int? relatedFeedbackId;

  Notification({
    required this.id,
    required this.userId,
    required this.title,
    required this.body,
    required this.isRead,
    required this.createdAt,
    this.relatedFeedbackId,
  });

  factory Notification.fromJson(Map<String, dynamic> json) {
    return Notification(
      id: json['id'],
      userId: json['userId'],
      title: json['title'] ?? '',
      body: json['body'] ?? '',
      isRead: json['isRead'] ?? false,
      createdAt: DateTime.parse(json['createdAt']),
      relatedFeedbackId: json['relatedFeedbackId'],
    );
  }
}
```

### 2. API Service Metodları

```dart
// services/feedback_service.dart

import 'dart:convert';
import 'package:http/http.dart' as http;
import '../models/feedback_models.dart';

class FeedbackService {
  final String baseUrl;
  final Future<String> Function() getToken;

  FeedbackService({
    required this.baseUrl,
    required this.getToken,
  });

  Future<Feedback> createFeedback(CreateFeedbackRequest request) async {
    try {
      final response = await http.post(
        Uri.parse('$baseUrl/api/Feedback/CreateFeedback'),
        headers: {
          'Authorization': 'Bearer ${await getToken()}',
          'Content-Type': 'application/json',
        },
        body: json.encode(request.toJson()),
      );

      if (response.statusCode == 200) {
        final data = json.decode(response.body);
        return Feedback.fromJson(data);
      } else {
        throw Exception('Failed to create feedback: ${response.body}');
      }
    } catch (e) {
      throw Exception('Error creating feedback: $e');
    }
  }

  Future<List<Feedback>> getMyFeedbacks({FeedbackStatus? status}) async {
    try {
      String url = '$baseUrl/api/Feedback/GetMyFeedbacks';
      if (status != null) {
        url += '?status=${status.value}';
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

  Future<Feedback> getFeedbackDetails(int feedbackId) async {
    try {
      final response = await http.get(
        Uri.parse('$baseUrl/api/Feedback/GetFeedbackDetails?id=$feedbackId'),
        headers: {
          'Authorization': 'Bearer ${await getToken()}',
          'Content-Type': 'application/json',
        },
      );

      if (response.statusCode == 200) {
        final data = json.decode(response.body);
        return Feedback.fromJson(data);
      } else {
        throw Exception('Failed to get feedback details: ${response.body}');
      }
    } catch (e) {
      throw Exception('Error getting feedback details: $e');
    }
  }

  Future<void> closeFeedback(int feedbackId) async {
    try {
      final response = await http.post(
        Uri.parse('$baseUrl/api/Feedback/CloseFeedback?id=$feedbackId'),
        headers: {
          'Authorization': 'Bearer ${await getToken()}',
          'Content-Type': 'application/json',
        },
      );

      if (response.statusCode != 200) {
        throw Exception('Failed to close feedback: ${response.body}');
      }
    } catch (e) {
      throw Exception('Error closing feedback: $e');
    }
  }
}
```

### 3. Feedback Form Widget-ı

```dart
// widgets/feedback_form_dialog.dart

import 'package:flutter/material.dart';
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
                      'Feedback Göndər',
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
                  'Feedback Tipi',
                  style: Theme.of(context).textTheme.titleMedium,
                ),
                const SizedBox(height: 8),
                Row(
                  children: [
                    Expanded(
                      child: RadioListTile<FeedbackType>(
                        title: const Text('Təklif'),
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
                        title: const Text('Şikayət'),
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

                // Rating (Ulduzlar)
                Text(
                  'Dəyərləndirmə (İstəyə bağlı)',
                  style: Theme.of(context).textTheme.titleMedium,
                ),
                const SizedBox(height: 8),
                Center(
                  child: RatingBar.builder(
                    initialRating: _rating ?? 0,
                    minRating: 0,
                    direction: Axis.horizontal,
                    allowHalfRating: true,
                    itemCount: 5,
                    itemSize: 40,
                    itemPadding: const EdgeInsets.symmetric(horizontal: 4.0),
                    itemBuilder: (context, _) => const Icon(
                      Icons.star,
                      color: Colors.amber,
                    ),
                    onRatingUpdate: (rating) {
                      setState(() {
                        _rating = rating;
                      });
                    },
                  ),
                ),
                const SizedBox(height: 8),
                if (_rating != null)
                  Center(
                    child: Text(
                      '${_rating!.toStringAsFixed(1)} / 5.0',
                      style: Theme.of(context).textTheme.bodyMedium,
                    ),
                  ),
                const SizedBox(height: 24),

                // Message Field
                TextFormField(
                  controller: _messageController,
                  decoration: const InputDecoration(
                    labelText: 'Mesaj *',
                    hintText: 'Feedback mesajınızı yazın...',
                    border: OutlineInputBorder(),
                  ),
                  maxLines: 5,
                  maxLength: 2000,
                  validator: (value) {
                    if (value == null || value.trim().isEmpty) {
                      return 'Mesaj daxil edilməlidir';
                    }
                    if (value.length < 1) {
                      return 'Mesaj çox qısadır';
                    }
                    return null;
                  },
                ),
                const SizedBox(height: 16),

                // Share Phone Number Checkbox
                CheckboxListTile(
                  title: const Text('Telefon nömrəmi paylaş'),
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
                    child: const Text('Göndər'),
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

**pubspec.yaml-a əlavə edin:**
```yaml
dependencies:
  flutter_rating_bar: ^4.0.1
```

### 4. My Cards Səhifəsində Feedback Icon Əlavəsi

```dart
// screens/my_cards_screen.dart (əlavə ediləcək hissə)

import 'package:flutter/material.dart';
import '../widgets/feedback_form_dialog.dart';
import '../services/feedback_service.dart';
import '../models/feedback_models.dart';

class MyCardsScreen extends StatefulWidget {
  @override
  _MyCardsScreenState createState() => _MyCardsScreenState();
}

class _MyCardsScreenState extends State<MyCardsScreen> {
  final FeedbackService _feedbackService = FeedbackService(
    baseUrl: 'YOUR_BASE_URL',
    getToken: () async => 'YOUR_TOKEN', // Token alma funksiyanız
  );

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

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('My Cards'),
      ),
      body: ListView.builder(
        itemCount: cards.length, // cards listiniz
        itemBuilder: (context, index) {
          final card = cards[index];
          return Card(
            child: Column(
              children: [
                // Kart məlumatları
                ListTile(
                  title: Text(card.tenantName),
                  subtitle: Text(card.programName),
                  // ... digər kart məlumatları
                ),
                
                // Feedback Icon
                Padding(
                  padding: const EdgeInsets.all(8.0),
                  child: Row(
                    mainAxisAlignment: MainAxisAlignment.end,
                    children: [
                      IconButton(
                        icon: const Icon(Icons.feedback),
                        tooltip: 'Feedback göndər',
                        onPressed: () => _showFeedbackForm(
                          card.tenantId,
                          card.tenantName,
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          );
        },
      ),
    );
  }
}
```

### 5. Feedback-ləri Göstərmək və Close Etmək

```dart
// screens/my_feedbacks_screen.dart

import 'package:flutter/material.dart';
import '../services/feedback_service.dart';
import '../models/feedback_models.dart';

class MyFeedbacksScreen extends StatefulWidget {
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

  @override
  void initState() {
    super.initState();
    _loadFeedbacks();
  }

  Future<void> _loadFeedbacks() async {
    setState(() {
      _isLoading = true;
    });

    try {
      final feedbacks = await _feedbackService.getMyFeedbacks();
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

  Future<void> _closeFeedback(int feedbackId) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Feedback-i Bağla'),
        content: const Text('Bu feedback-i bağlamaq istədiyinizə əminsiniz?'),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: const Text('Ləğv et'),
          ),
          ElevatedButton(
            onPressed: () => Navigator.of(context).pop(true),
            child: const Text('Bağla'),
          ),
        ],
      ),
    );

    if (confirmed != true) return;

    try {
      await _feedbackService.closeFeedback(feedbackId);
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Feedback bağlandı'),
          backgroundColor: Colors.green,
        ),
      );
      _loadFeedbacks(); // Listi yenilə
    } catch (e) {
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
                          trailing: feedback.status == FeedbackStatus.open
                              ? IconButton(
                                  icon: const Icon(Icons.close),
                                  tooltip: 'Bağla',
                                  onPressed: () => _closeFeedback(feedback.id),
                                )
                              : const Chip(
                                  label: Text('Bağlı'),
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
                                        const Text('Dəyərləndirmə: '),
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
                                    'Mesaj:',
                                    style: Theme.of(context).textTheme.titleSmall,
                                  ),
                                  const SizedBox(height: 4),
                                  Text(feedback.message),
                                  const SizedBox(height: 16),
                                  if (feedback.replies.isNotEmpty) ...[
                                    const Divider(),
                                    Text(
                                      'Cavablar:',
                                      style: Theme.of(context).textTheme.titleSmall,
                                    ),
                                    const SizedBox(height: 8),
                                    ...feedback.replies.map((reply) {
                                      return Card(
                                        color: reply.senderType ==
                                                FeedbackReplySenderType.store
                                            ? Colors.blue[50]
                                            : Colors.green[50],
                                        child: ListTile(
                                          title: Text(
                                            reply.senderType ==
                                                    FeedbackReplySenderType.store
                                                ? 'Mağaza'
                                                : 'Siz',
                                          ),
                                          subtitle: Text(reply.message),
                                          trailing: Text(
                                            _formatDate(reply.createdAt),
                                            style: const TextStyle(fontSize: 12),
                                          ),
                                        ),
                                      );
                                    }),
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

  String _formatDate(DateTime date) {
    return '${date.day}.${date.month}.${date.year} ${date.hour}:${date.minute.toString().padLeft(2, '0')}';
  }
}
```

### 6. Bildirim Sistemi (Notification)

```dart
// services/notification_service.dart

import 'dart:convert';
import 'package:http/http.dart' as http;
import '../models/feedback_models.dart';

class NotificationService {
  final String baseUrl;
  final Future<String> Function() getToken;

  NotificationService({
    required this.baseUrl,
    required this.getToken,
  });

  Future<List<Notification>> getMyNotifications() async {
    try {
      final response = await http.get(
        Uri.parse('$baseUrl/api/Notification/GetMyNotifications'), // Endpoint-i yoxlayın
        headers: {
          'Authorization': 'Bearer ${await getToken()}',
          'Content-Type': 'application/json',
        },
      );

      if (response.statusCode == 200) {
        final data = json.decode(response.body) as List<dynamic>;
        return data.map((json) => Notification.fromJson(json)).toList();
      } else {
        throw Exception('Failed to get notifications: ${response.body}');
      }
    } catch (e) {
      throw Exception('Error getting notifications: $e');
    }
  }

  Future<void> markAsRead(int notificationId) async {
    try {
      final response = await http.post(
        Uri.parse('$baseUrl/api/Notification/MarkAsRead?id=$notificationId'),
        headers: {
          'Authorization': 'Bearer ${await getToken()}',
          'Content-Type': 'application/json',
        },
      );

      if (response.statusCode != 200) {
        throw Exception('Failed to mark notification as read');
      }
    } catch (e) {
      throw Exception('Error marking notification as read: $e');
    }
  }
}
```

### 7. Bildirimləri Dinləmək və Feedback Səhifəsinə Yönləndirmək

```dart
// widgets/notification_handler.dart

import 'package:flutter/material.dart';
import '../services/notification_service.dart';
import '../models/feedback_models.dart';
import '../screens/my_feedbacks_screen.dart';

class NotificationHandler {
  final NotificationService _notificationService;
  final BuildContext context;

  NotificationHandler({
    required this.context,
    required NotificationService notificationService,
  }) : _notificationService = notificationService;

  Future<void> handleNotification(Notification notification) async {
    // Bildirimi oxunmuş kimi qeyd et
    await _notificationService.markAsRead(notification.id);

    // Əgər feedback ilə bağlıdırsa, feedback səhifəsinə yönləndir
    if (notification.relatedFeedbackId != null) {
      Navigator.of(context).push(
        MaterialPageRoute(
          builder: (context) => MyFeedbacksScreen(),
        ),
      );
    }
  }

  void showNotificationSnackBar(Notification notification) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(notification.title),
        action: SnackBarAction(
          label: 'Bax',
          onPressed: () => handleNotification(notification),
        ),
        duration: const Duration(seconds: 5),
      ),
    );
  }
}
```

### 8. Real-time Bildirimlər (Polling və ya WebSocket)

```dart
// services/notification_polling_service.dart

import 'dart:async';
import '../services/notification_service.dart';
import '../models/feedback_models.dart';

class NotificationPollingService {
  final NotificationService _notificationService;
  Timer? _timer;
  List<Notification> _lastNotifications = [];

  NotificationPollingService(this._notificationService);

  void startPolling({
    required Function(List<Notification>) onNewNotifications,
    Duration interval = const Duration(seconds: 30),
  }) {
    _timer?.cancel();
    _timer = Timer.periodic(interval, (timer) async {
      try {
        final notifications = await _notificationService.getMyNotifications();
        final unreadNotifications = notifications
            .where((n) => !n.isRead)
            .where((n) => !_lastNotifications.any((ln) => ln.id == n.id))
            .toList();

        if (unreadNotifications.isNotEmpty) {
          onNewNotifications(unreadNotifications);
        }

        _lastNotifications = notifications;
      } catch (e) {
        print('Error polling notifications: $e');
      }
    });
  }

  void stopPolling() {
    _timer?.cancel();
    _timer = null;
  }

  void dispose() {
    stopPolling();
  }
}
```

## Əlavə Qeydlər

1. **Rating Bar Package:** `flutter_rating_bar` package-i istifadə edilmişdir. `pubspec.yaml`-a əlavə edin.

2. **Token İdarəetməsi:** JWT token-ı secure storage-dan (məsələn, `flutter_secure_storage`) alınmalıdır.

3. **Base URL:** API base URL-i environment config-dən götürülməlidir.

4. **Error Handling:** Bütün API çağırışlarında xəta idarəetməsi təmin edilməlidir.

5. **Loading States:** Form göndərilərkən və feedback-lər yüklənərkən loading göstərilməlidir.

6. **Validation:** Form validation düzgün işləməlidir (mesaj minimum 1 simvol, maksimum 2000 simvol).

7. **Bildirim Sistemi:** Real-time bildirimlər üçün polling və ya WebSocket istifadə edilə bilər.

8. **Feedback Status:** Feedback status-u (Open/Closed) UI-da düzgün göstərilməlidir.

9. **Reply Display:** Mağaza və müştəri cavabları fərqli rənglərdə göstərilməlidir.

10. **Date Formatting:** Tarixlər istifadəçi dostu formatda göstərilməlidir.

## Test Edilməli Hallar

- ✅ Feedback form-unun açılması
- ✅ Feedback type seçimi (Təklif/Şikayət)
- ✅ Rating (ulduz) seçimi
- ✅ Mesaj yazılması və validation
- ✅ Feedback göndərilməsi
- ✅ Feedback-lərin listdə göstərilməsi
- ✅ Feedback detallarının görünməsi
- ✅ Mağaza cavablarının görünməsi
- ✅ Feedback-in close edilməsi
- ✅ Bildirimlərin alınması
- ✅ Bildirimdən feedback səhifəsinə yönləndirmə
- ✅ Network xətası idarəetməsi
- ✅ Loading state-lərinin düzgün işləməsi

