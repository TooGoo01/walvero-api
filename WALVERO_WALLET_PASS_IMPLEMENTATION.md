# Walvero Wallet Pass Implementation Guide

## 📱 Wallet-ə Kart Əlavə Etmə Mexanizmi

### Apple Wallet (iOS)

**Backend-də nə olur:**
1. `.pkpass` faylı yaradılır və serverdə saxlanılır
2. Absolute URL qaytarılır: `https://walvero.com/passes/walvero/{passRecordId}/{serial}.pkpass`

**Flutter tərəfində nə etmək lazımdır:**

#### Metod 1: URL açmaq (Sadə, amma bəzən işləməyə bilər)

```dart
import 'package:url_launcher/url_launcher.dart';

// Response-dan Apple Pass URL-i alınır
final applePassUrl = response.data?.applePassUrl; 
// "https://walvero.com/passes/walvero/1/AL-00000001.pkpass"

if (applePassUrl != null) {
  // iOS-da bu URL-i açdıqda, sistem avtomatik olaraq pass-i wallet-ə əlavə edir
  if (await canLaunchUrl(Uri.parse(applePassUrl))) {
    await launchUrl(
      Uri.parse(applePassUrl),
      mode: LaunchMode.externalApplication,
    );
  }
}
```

#### Metod 2: PassKit Framework istifadə etmək (Tövsiyə olunur) ✅

```dart
import 'package:passkit_flutter/passkit_flutter.dart';
import 'package:http/http.dart' as http;
import 'dart:io';

// 1. .pkpass faylını download et
Future<void> addPassToAppleWallet(String applePassUrl) async {
  try {
    // Download .pkpass faylını
    final response = await http.get(Uri.parse(applePassUrl));
    
    if (response.statusCode == 200) {
      // Temporary file yarat
      final tempDir = await getTemporaryDirectory();
      final file = File('${tempDir.path}/walvero_pass.pkpass');
      await file.writeAsBytes(response.bodyBytes);
      
      // PassKit ilə wallet-ə əlavə et
      final result = await PassKitFlutter.addPass(file.path);
      
      if (result == AddPassResult.added) {
        print('Pass successfully added to Apple Wallet!');
      } else if (result == AddPassResult.cancelled) {
        print('User cancelled adding pass');
      } else {
        print('Failed to add pass: $result');
      }
    }
  } catch (e) {
    print('Error adding pass to wallet: $e');
  }
}

// İstifadə:
final applePassUrl = response.data?.applePassUrl;
if (applePassUrl != null) {
  await addPassToAppleWallet(applePassUrl);
}
```

#### Metod 3: Platform Channel ilə Native iOS kodu (Ən etibarlı) ✅✅

**Flutter tərəfində:**
```dart
import 'package:flutter/services.dart';

class AppleWalletService {
  static const platform = MethodChannel('com.walvero.applewallet');
  
  static Future<bool> addPassToWallet(String passUrl) async {
    try {
      if (Platform.isIOS) {
        final result = await platform.invokeMethod('addPassToWallet', {
          'passUrl': passUrl,
        });
        return result == true;
      }
      return false;
    } on PlatformException catch (e) {
      print('Error: ${e.message}');
      return false;
    }
  }
}
```

**iOS Native (Swift) - `ios/Runner/AppDelegate.swift` və ya yeni plugin:**
```swift
import UIKit
import PassKit

@UIApplicationMain
@objc class AppDelegate: FlutterAppDelegate {
  override func application(
    _ application: UIApplication,
    didFinishLaunchingWithOptions launchOptions: [UIApplication.LaunchOptionsKey: Any]?
  ) -> Bool {
    
    let controller = window?.rootViewController as! FlutterViewController
    let walletChannel = FlutterMethodChannel(
      name: "com.walvero.applewallet",
      binaryMessenger: controller.binaryMessenger
    )
    
    walletChannel.setMethodCallHandler { (call: FlutterMethodCall, result: @escaping FlutterResult) in
      if call.method == "addPassToWallet" {
        guard let args = call.arguments as? [String: Any],
              let passUrl = args["passUrl"] as? String else {
          result(FlutterError(code: "INVALID_ARGS", message: "passUrl required", details: nil))
          return
        }
        
        self.downloadAndAddPass(urlString: passUrl, result: result)
      } else {
        result(FlutterMethodNotImplemented)
      }
    }
    
    GeneratedPluginRegistrant.register(with: self)
    return super.application(application, didFinishLaunchingWithOptions: launchOptions)
  }
  
  func downloadAndAddPass(urlString: String, result: @escaping FlutterResult) {
    guard let url = URL(string: urlString) else {
      result(FlutterError(code: "INVALID_URL", message: "Invalid pass URL", details: nil))
      return
    }
    
    URLSession.shared.dataTask(with: url) { data, response, error in
      guard let data = data, error == nil else {
        DispatchQueue.main.async {
          result(FlutterError(code: "DOWNLOAD_ERROR", message: error?.localizedDescription, details: nil))
        }
        return
      }
      
      DispatchQueue.main.async {
        do {
          let pass = try PKPass(data: data)
          let passLibrary = PKPassLibrary()
          
          if passLibrary.containsPass(pass) {
            result(true) // Pass already in wallet
            return
          }
          
          let addPassViewController = PKAddPassesViewController(pass: pass)!
          addPassViewController.delegate = self
          
          if let rootVC = self.window?.rootViewController {
            rootVC.present(addPassViewController, animated: true)
            result(true)
          } else {
            result(FlutterError(code: "NO_ROOT_VC", message: "No root view controller", details: nil))
          }
        } catch {
          result(FlutterError(code: "PASS_ERROR", message: error.localizedDescription, details: nil))
        }
      }
    }.resume()
  }
}

extension AppDelegate: PKAddPassesViewControllerDelegate {
  func addPassesViewControllerDidFinish(_ controller: PKAddPassesViewController) {
    controller.dismiss(animated: true)
  }
}
```

**iOS-da avtomatik:**
- `.pkpass` faylı açılanda iOS avtomatik olaraq Wallet app-ini açır
- İstifadəçi "Add" düyməsinə basır və pass wallet-ə əlavə olunur

---

### Google Wallet (Android)

**Backend-də nə olur:**
1. Google Wallet-də `LoyaltyObject` yaradılır (points/balance olmadan)
2. "Save to Google Wallet" JWT linki yaradılır: `https://pay.google.com/gp/v/save/{jwt}`

**Flutter tərəfində nə etmək lazımdır:**

```dart
// Response-dan Google Save URL-i alınır
final googleSaveUrl = response.data?.googleSaveUrl; 
// "https://pay.google.com/gp/v/save/eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9..."

if (googleSaveUrl != null) {
  // Android-də bu URL-i açdıqda, Google Wallet app açılır
  await launchUrl(Uri.parse(googleSaveUrl));
  
  // Və ya `url_launcher` paketi ilə:
  // if (await canLaunchUrl(Uri.parse(googleSaveUrl))) {
  //   await launchUrl(Uri.parse(googleSaveUrl), mode: LaunchMode.externalApplication);
  // }
}
```

**Android-də avtomatik:**
- Google Wallet save linki açılanda Google Wallet app avtomatik açılır
- İstifadəçi "Save" düyməsinə basır və kart wallet-ə əlavə olunur

---

## 🔄 Tam Proses

### 1. Pass Yaradılması (Backend)

```
POST /api/wallet/passes/walvero/create
{
  "userId": 123,
  "platform": "ios" // və ya "android"
}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "tenantId": 999,
    "userId": 123,
    "platform": "ios",
    "walveroIdMasked": "WLV****001",
    "applePassUrl": "https://walvero.com/passes/walvero/1/AL-00000001.pkpass",
    "googleSaveUrl": null,
    "passId": 1
  }
}
```

### 2. Flutter Tərəfində İstifadə

```dart
// 1. Pass yarat
final response = await walletPassService.createWalveroPass(
  userId: currentUserId,
  platform: Platform.isIOS ? 'ios' : 'android',
);

if (response.success && response.data != null) {
  final passData = response.data!;
  
  // 2. Platforma görə URL aç
  if (Platform.isIOS && passData.applePassUrl != null) {
    // iOS: .pkpass faylını aç
    await launchUrl(Uri.parse(passData.applePassUrl!));
  } else if (Platform.isAndroid && passData.googleSaveUrl != null) {
    // Android: Google Wallet save linkini aç
    await launchUrl(Uri.parse(passData.googleSaveUrl!));
  }
}
```

---

## 📝 Qeydlər

1. **Apple Wallet:**
   - `.pkpass` faylı serverdə saxlanılır
   - URL açılanda iOS avtomatik Wallet app-ini açır
   - İstifadəçi təsdiq edir və pass əlavə olunur

2. **Google Wallet:**
   - Object Google Wallet API-də yaradılır
   - JWT linki yaradılır və qaytarılır
   - Link açılanda Google Wallet app açılır
   - İstifadəçi təsdiq edir və kart əlavə olunur

3. **Idempotent:**
   - Eyni `userId` və `platform` üçün yenidən çağırılsa, mövcud pass qaytarılır
   - Yeni pass yaradılmır

4. **Security:**
   - Pass-lər yalnız istifadəçinin öz WalveroId-si ilə yaradılır
   - Başqa istifadəçi üçün pass yaratmaq qadağandır (403)

---

## 🛠️ Flutter Paketləri

```yaml
dependencies:
  url_launcher: ^6.2.0  # URL açmaq üçün
  dio: ^5.0.0           # HTTP requests
  http: ^1.1.0         # .pkpass download üçün
  passkit_flutter: ^0.1.0  # Apple Wallet üçün (əgər mövcuddursa)
  path_provider: ^2.1.0    # Temporary files üçün
```

---

## 📱 Tam Flutter Implementasiyası

### Service Class

```dart
import 'package:dio/dio.dart';
import 'package:http/http.dart' as http;
import 'package:path_provider/path_provider.dart';
import 'dart:io';
import 'package:passkit_flutter/passkit_flutter.dart'; // və ya platform channel

class WalveroWalletPassService {
  final Dio _dio;
  
  WalveroWalletPassService(this._dio);
  
  Future<WalveroPassResponse?> createWalveroPass({
    required int userId,
    required String platform, // "ios" | "android"
  }) async {
    try {
      final response = await _dio.post(
        '/api/wallet/passes/walvero/create',
        data: {
          'userId': userId,
          'platform': platform,
        },
      );
      
      if (response.data['success'] == true) {
        return WalveroPassResponse.fromJson(response.data['data']);
      }
      return null;
    } catch (e) {
      print('Error creating pass: $e');
      return null;
    }
  }
  
  /// Apple Wallet-ə pass əlavə et
  Future<bool> addToAppleWallet(String passUrl) async {
    if (!Platform.isIOS) return false;
    
    try {
      // 1. Download .pkpass faylını
      final httpResponse = await http.get(Uri.parse(passUrl));
      
      if (httpResponse.statusCode != 200) {
        print('Failed to download pass: ${httpResponse.statusCode}');
        return false;
      }
      
      // 2. Temporary file yarat
      final tempDir = await getTemporaryDirectory();
      final passFile = File('${tempDir.path}/walvero_${DateTime.now().millisecondsSinceEpoch}.pkpass');
      await passFile.writeAsBytes(httpResponse.bodyBytes);
      
      // 3. PassKit ilə wallet-ə əlavə et
      final result = await PassKitFlutter.addPass(passFile.path);
      
      // 4. Temporary file sil
      await passFile.delete();
      
      return result == AddPassResult.added;
    } catch (e) {
      print('Error adding to Apple Wallet: $e');
      return false;
    }
  }
  
  /// Google Wallet-ə kart əlavə et
  Future<bool> addToGoogleWallet(String saveUrl) async {
    if (!Platform.isAndroid) return false;
    
    try {
      // Android-də sadəcə URL-i açmaq kifayətdir
      // Google Wallet app avtomatik açılır
      return await launchUrl(Uri.parse(saveUrl));
    } catch (e) {
      print('Error adding to Google Wallet: $e');
      return false;
    }
  }
}
```

### UI-də İstifadə

```dart
class WalveroPassScreen extends StatefulWidget {
  @override
  _WalveroPassScreenState createState() => _WalveroPassScreenState();
}

class _WalveroPassScreenState extends State<WalveroPassScreen> {
  final _service = WalveroWalletPassService(dio);
  bool _isLoading = false;
  
  Future<void> _createAndAddPass() async {
    setState(() => _isLoading = true);
    
    try {
      // 1. Pass yarat
      final platform = Platform.isIOS ? 'ios' : 'android';
      final response = await _service.createWalveroPass(
        userId: currentUserId,
        platform: platform,
      );
      
      if (response == null) {
        _showError('Failed to create pass');
        return;
      }
      
      // 2. Wallet-ə əlavə et
      bool success = false;
      if (Platform.isIOS && response.applePassUrl != null) {
        success = await _service.addToAppleWallet(response.applePassUrl!);
      } else if (Platform.isAndroid && response.googleSaveUrl != null) {
        success = await _service.addToGoogleWallet(response.googleSaveUrl!);
      }
      
      if (success) {
        _showSuccess('Pass added to wallet!');
      } else {
        _showError('Failed to add pass to wallet');
      }
    } catch (e) {
      _showError('Error: $e');
    } finally {
      setState(() => _isLoading = false);
    }
  }
  
  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text('Add Walvero Pass')),
      body: Center(
        child: _isLoading
          ? CircularProgressIndicator()
          : ElevatedButton(
              onPressed: _createAndAddPass,
              child: Text('Add to Wallet'),
            ),
      ),
    );
  }
}
```

---

## ✅ Tövsiyə olunan yanaşma

**Apple Wallet üçün:**
1. ✅ **PassKit Framework** (Native iOS) - Ən etibarlı
2. ✅ **passkit_flutter** paketi - Əgər mövcuddursa
3. ⚠️ **URL açmaq** - Sadə, amma bəzən işləməyə bilər

**Google Wallet üçün:**
1. ✅ **URL açmaq** - Kifayətdir, Google Wallet app avtomatik açılır

