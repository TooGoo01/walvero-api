# Flutter - My Cards Bölməsində Kart Silmə Funksionallığı

## Tapşırıq
"My Cards" bölməsində kartları silmək üçün iki üsul təmin etmək lazımdır:
1. **Dismissable (Sürüşdürərək silmə)** - Kartı sola və ya sağa sürüşdürərkən silmə
2. **3 nöqtə menyusu** - Kartın sağ yuxarı küncündə 3 nöqtə ikonuna basaraq "Delete" seçmək

Hər iki halda istifadəçiyə təsdiq popup-ı göstərilməlidir və təsdiq edildikdən sonra API-yə müraciət edilməlidir.

## API Məlumatları

**Endpoint:** `POST /api/Cards/Deactive`

**Authentication:** JWT Token tələb olunur (Bearer token header-də göndərilməlidir)

**Request:**
- Method: POST
- URL: `{baseUrl}/api/Cards/Deactive`
- Body: `id` parametri (query parametr və ya body-də göndərilə bilər)
- Headers:
  ```
  Authorization: Bearer {token}
  Content-Type: application/json
  ```

**Response:**
```json
{
  "success": true,
  "message": "Delete completed",
  "data": null
}
```

və ya uğursuz olarsa:
```json
{
  "success": false,
  "message": "Error message",
  "data": null
}
```

## Flutter İmplementasiyası

### 1. API Service Metodunun Əlavə Edilməsi

```dart
// api_service.dart və ya cards_service.dart faylında

Future<ServiceResult> deactiveCard(int cardId) async {
  try {
    final response = await http.post(
      Uri.parse('$baseUrl/api/Cards/Deactive?id=$cardId'),
      headers: {
        'Authorization': 'Bearer ${await getToken()}', // Token alma funksiyanız
        'Content-Type': 'application/json',
      },
    );

    if (response.statusCode == 200) {
      final data = json.decode(response.body);
      return ServiceResult.fromJson(data);
    } else {
      throw Exception('Failed to delete card');
    }
  } catch (e) {
    throw Exception('Error: $e');
  }
}
```

**ServiceResult Model:**
```dart
class ServiceResult {
  final bool success;
  final String message;
  final dynamic data;

  ServiceResult({
    required this.success,
    required this.message,
    this.data,
  });

  factory ServiceResult.fromJson(Map<String, dynamic> json) {
    return ServiceResult(
      success: json['success'] ?? false,
      message: json['message'] ?? '',
      data: json['data'],
    );
  }
}
```

### 2. Təsdiq Dialog Widget-ı

```dart
// widgets/confirm_delete_dialog.dart

import 'package:flutter/material.dart';

Future<bool> showDeleteConfirmationDialog(BuildContext context) async {
  return await showDialog<bool>(
    context: context,
    builder: (BuildContext context) {
      return AlertDialog(
        title: const Text('Kartı Sil'),
        content: const Text('Bu kartı silmək istədiyinizə əminsiniz? Bu əməliyyat geri alına bilməz.'),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: const Text('Ləğv et'),
          ),
          ElevatedButton(
            onPressed: () => Navigator.of(context).pop(true),
            style: ElevatedButton.styleFrom(
              backgroundColor: Colors.red,
              foregroundColor: Colors.white,
            ),
            child: const Text('Sil'),
          ),
        ],
      );
    },
  ) ?? false;
}
```

### 3. My Cards Səhifəsi - Dismissable İmplementasiyası

```dart
// screens/my_cards_screen.dart

import 'package:flutter/material.dart';

class MyCardsScreen extends StatefulWidget {
  @override
  _MyCardsScreenState createState() => _MyCardsScreenState();
}

class _MyCardsScreenState extends State<MyCardsScreen> {
  List<CardModel> cards = []; // Kartlarınızın listi

  Future<void> _deleteCard(int cardId) async {
    // Təsdiq dialog-u göstər
    final confirmed = await showDeleteConfirmationDialog(context);
    
    if (!confirmed) return;

    // Loading göstər
    showDialog(
      context: context,
      barrierDismissible: false,
      builder: (context) => const Center(child: CircularProgressIndicator()),
    );

    try {
      final result = await apiService.deactiveCard(cardId);
      
      Navigator.pop(context); // Loading dialog-u bağla

      if (result.success) {
        // Uğurlu mesaj göstər
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Kart uğurla silindi'),
            backgroundColor: Colors.green,
          ),
        );
        
        // Listdən sil
        setState(() {
          cards.removeWhere((card) => card.id == cardId);
        });
      } else {
        // Xəta mesajı göstər
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(result.message),
            backgroundColor: Colors.red,
          ),
        );
      }
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

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('My Cards'),
      ),
      body: ListView.builder(
        itemCount: cards.length,
        itemBuilder: (context, index) {
          final card = cards[index];
          return Dismissible(
            key: Key(card.id.toString()),
            direction: DismissDirection.endToStart, // Sağdan sola sürüşdürmə
            background: Container(
              alignment: Alignment.centerRight,
              padding: const EdgeInsets.only(right: 20),
              color: Colors.red,
              child: const Icon(
                Icons.delete,
                color: Colors.white,
                size: 30,
              ),
            ),
            confirmDismiss: (direction) async {
              // Sürüşdürmə zamanı birbaşa silmək əvəzinə təsdiq istə
              return await showDeleteConfirmationDialog(context);
            },
            onDismissed: (direction) async {
              // Təsdiq edildikdən sonra API-yə müraciət et
              await _deleteCard(card.id);
            },
            child: Card(
              // Kartınızın UI-ı
              child: ListTile(
                title: Text(card.title),
                subtitle: Text(card.subtitle),
                trailing: PopupMenuButton<String>(
                  icon: const Icon(Icons.more_vert),
                  onSelected: (value) async {
                    if (value == 'delete') {
                      await _deleteCard(card.id);
                    }
                  },
                  itemBuilder: (BuildContext context) => [
                    const PopupMenuItem<String>(
                      value: 'delete',
                      child: Row(
                        children: [
                          Icon(Icons.delete, color: Colors.red),
                          SizedBox(width: 8),
                          Text('Sil', style: TextStyle(color: Colors.red)),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            ),
          );
        },
      ),
    );
  }
}
```

### 4. Alternativ: Dismissible-də Təsdiq Dialog İlə İşləmə

Əgər `confirmDismiss` istifadə etmək istəmirsinizsə, `onDismissed`-dən əvvəl təsdiq dialog-u göstərə bilərsiniz:

```dart
Dismissible(
  key: Key(card.id.toString()),
  direction: DismissDirection.endToStart,
  background: Container(
    alignment: Alignment.centerRight,
    padding: const EdgeInsets.only(right: 20),
    color: Colors.red,
    child: const Icon(Icons.delete, color: Colors.white, size: 30),
  ),
  onDismissed: (direction) async {
    // Burada təsdiq dialog-u göstər və API-yə müraciət et
    final confirmed = await showDeleteConfirmationDialog(context);
    if (confirmed) {
      await _deleteCard(card.id);
    } else {
      // Təsdiq edilmədisə, kartı geri qaytar
      setState(() {
        // Kart listini yenilə
      });
    }
  },
  child: Card(...),
)
```

### 5. Loading və Error Handling

```dart
bool _isDeleting = false;

Future<void> _deleteCard(int cardId) async {
  if (_isDeleting) return; // Əgər artıq silinirsə, gözlə
  
  final confirmed = await showDeleteConfirmationDialog(context);
  if (!confirmed) return;

  setState(() {
    _isDeleting = true;
  });

  try {
    final result = await apiService.deactiveCard(cardId);
    
    if (result.success) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Kart uğurla silindi'),
          backgroundColor: Colors.green,
        ),
      );
      
      setState(() {
        cards.removeWhere((card) => card.id == cardId);
      });
    } else {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(result.message),
          backgroundColor: Colors.red,
        ),
      );
    }
  } catch (e) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text('Xəta: ${e.toString()}'),
        backgroundColor: Colors.red,
      ),
    );
  } finally {
    setState(() {
      _isDeleting = false;
    });
  }
}
```

## Əlavə Qeydlər

1. **Token İdarəetməsi:** JWT token-ı secure storage-dan (məsələn, `flutter_secure_storage`) alınmalıdır.

2. **Base URL:** API base URL-i environment config-dən götürülməlidir.

3. **Network Error Handling:** İnternet bağlantısı yoxdursa, uyğun mesaj göstərilməlidir.

4. **Optimistic Update:** İstəyə görə, API cavabını gözləmədən kartı listdən silə bilərsiniz, lakin uğursuz olarsa geri qaytarmalısınız.

5. **Pull to Refresh:** Kart silindikdən sonra listi yeniləmək üçün pull-to-refresh funksionallığı əlavə edilə bilər.

## Test Edilməli Hallar

- ✅ Dismissable ilə kart silmə
- ✅ 3 nöqtə menyusundan silmə
- ✅ Təsdiq dialog-u "Ləğv et" seçimi
- ✅ Təsdiq dialog-u "Sil" seçimi
- ✅ API uğurlu cavab
- ✅ API uğursuz cavab
- ✅ Network xətası
- ✅ Token yoxdursa authentication xətası
- ✅ Loading state-i düzgün göstərilməsi

