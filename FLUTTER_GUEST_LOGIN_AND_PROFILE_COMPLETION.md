# Flutter: Guest Login, Token Refresh və Profil Tamamlama

## Məqsəd
Mobil tətbiq ilk açılanda guest login ilə başlayır, token refresh mexanizmi ilə davamlı olaraq login qalır, sonra istifadəçi profili tamamlayır.

## API Endpoint-ləri

### 1. Guest Login / Register (İlk Açılış)
```
POST /api/users/LogIn
Content-Type: application/json

{
  "deviceId": "unique-device-id-123"
}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "id": 123,
    "userName": "guest-abc123...",
    "firstName": "Guest",
    "lastName": "User",
    "email": null,
    "birthDate": "2024-12-08T10:00:00Z",
    "roles": ["Customer"],
    "tenantId": null,
    "token": "eyJhbGciOiJIUzI1NiIs...",
    "expiration": "2024-12-09T10:00:00Z",
    "refreshToken": "base64-refresh-token",
    "refreshTokenExpiration": "2025-01-08T10:00:00Z"
  },
  "message": "guest login success"
}
```

**Qeyd:** `UserName` və `Password` optional-dır. Əgər verilibsə, normal login cəhd edilir, uğursuz olsa guest login istifadə olunur.

### 2. Register (Eyni Guest Login)
```
POST /api/users/Register
Content-Type: application/json

{
  "deviceId": "unique-device-id-123"
}
```

**Response:** Eyni guest login response-u qaytarır.

### 3. Token Refresh
```
POST /api/auth/refresh-token
Content-Type: application/json

{
  "refreshToken": "base64-refresh-token"
}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "token": "new-access-token",
    "expiration": "2024-12-09T12:00:00Z",
    "refreshToken": "new-refresh-token",
    "refreshTokenExpiration": "2025-01-08T12:00:00Z",
    "userId": 123,
    "isGuest": true,
    "isProfileCompleted": false,
    "userName": "guest-abc123..."
  }
}
```

### 4. Profil Statusu Yoxlama
```
GET /api/profile/status
Authorization: Bearer {token}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "isGuest": true,
    "isProfileCompleted": false,
    "requiresProfileCompletion": true
  }
}
```

### 5. Telefon Nömrəsinə OTP Göndərmə
```
POST /api/profile/send-phone-otp
Authorization: Bearer {token}
Content-Type: application/json

{
  "phoneNumber": "+994501234567"
}
```

**Response:**
```json
{
  "success": true,
  "message": "OTP kod göndərildi",
  "data": {
    "maskedPhone": "*******4567",
    "expiresInMinutes": 5
  }
}
```

### 6. OTP Təsdiq Etmə
```
POST /api/profile/verify-phone-otp
Authorization: Bearer {token}
Content-Type: application/json

{
  "phoneNumber": "+994501234567",
  "otpCode": "123456"
}
```

**Response:**
```json
{
  "success": true,
  "message": "Telefon nömrəsi təsdiqləndi",
  "data": {
    "phoneVerified": true
  }
}
```

### 7. Profil Tamamlama
```
POST /api/profile/complete
Authorization: Bearer {token}
Content-Type: application/json

{
  "fullName": "John Doe",
  "phoneNumber": "+994501234567",
  "email": "john@example.com"
}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "token": "new-jwt-token",
    "expiration": "2024-12-09T12:00:00Z",
    "refreshToken": "new-refresh-token",
    "refreshTokenExpiration": "2025-01-08T12:00:00Z",
    "userId": 123,
    "isGuest": false,
    "isProfileCompleted": true,
    "userName": "+994501234567"
  }
}
```

## Package Requirements

```yaml
# pubspec.yaml
dependencies:
  http: ^1.1.0
  # Token storage üçün (birini seçin):
  flutter_secure_storage: ^9.0.0
  # və ya
  shared_preferences: ^2.2.0
```

## Flutter Implementation

### 1. Token Storage Service

```dart
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
// və ya
// import 'package:shared_preferences/shared_preferences.dart';

class TokenStorage {
  final FlutterSecureStorage _storage = const FlutterSecureStorage();
  // və ya
  // final SharedPreferences _prefs = await SharedPreferences.getInstance();

  static const String _accessTokenKey = 'access_token';
  static const String _refreshTokenKey = 'refresh_token';
  static const String _tokenExpirationKey = 'token_expiration';
  static const String _refreshExpirationKey = 'refresh_expiration';
  static const String _deviceIdKey = 'device_id';

  // Device ID al/saxla
  Future<String?> getDeviceId() async {
    return await _storage.read(key: _deviceIdKey);
  }

  Future<void> saveDeviceId(String deviceId) async {
    await _storage.write(key: _deviceIdKey, value: deviceId);
  }

  // Token-ları al
  Future<String?> getAccessToken() async {
    return await _storage.read(key: _accessTokenKey);
  }

  Future<String?> getRefreshToken() async {
    return await _storage.read(key: _refreshTokenKey);
  }

  Future<DateTime?> getTokenExpiration() async {
    final expStr = await _storage.read(key: _tokenExpirationKey);
    if (expStr != null) {
      return DateTime.parse(expStr);
    }
    return null;
  }

  // Token-ları saxla
  Future<void> saveTokens({
    required String accessToken,
    required String refreshToken,
    required DateTime expiration,
    required DateTime refreshExpiration,
  }) async {
    await _storage.write(key: _accessTokenKey, value: accessToken);
    await _storage.write(key: _refreshTokenKey, value: refreshToken);
    await _storage.write(key: _tokenExpirationKey, value: expiration.toIso8601String());
    await _storage.write(key: _refreshExpirationKey, value: refreshExpiration.toIso8601String());
  }

  // Token-ları sil (logout)
  Future<void> clearTokens() async {
    await _storage.delete(key: _accessTokenKey);
    await _storage.delete(key: _refreshTokenKey);
    await _storage.delete(key: _tokenExpirationKey);
    await _storage.delete(key: _refreshExpirationKey);
  }

  // Token vaxtı bitib?
  Future<bool> isTokenExpired() async {
    final expiration = await getTokenExpiration();
    if (expiration == null) return true;
    return DateTime.now().isAfter(expiration.subtract(const Duration(minutes: 5))); // 5 dəqiqə əvvəl refresh et
  }
}
```

### 2. Device ID Generator

```dart
import 'package:device_info_plus/device_info_plus.dart';
import 'package:shared_preferences/shared_preferences.dart';

class DeviceIdService {
  static const String _deviceIdKey = 'app_device_id';

  Future<String> getOrCreateDeviceId() async {
    final prefs = await SharedPreferences.getInstance();
    String? deviceId = prefs.getString(_deviceIdKey);

    if (deviceId == null || deviceId.isEmpty) {
      // Yeni device ID yarat
      final deviceInfo = DeviceInfoPlugin();
      
      if (Platform.isAndroid) {
        final androidInfo = await deviceInfo.androidInfo;
        deviceId = androidInfo.id; // Android ID
      } else if (Platform.isIOS) {
        final iosInfo = await deviceInfo.iosInfo;
        deviceId = iosInfo.identifierForVendor ?? _generateFallbackId();
      } else {
        deviceId = _generateFallbackId();
      }

      // Saxla
      await prefs.setString(_deviceIdKey, deviceId);
    }

    return deviceId;
  }

  String _generateFallbackId() {
    // UUID və ya random string
    return 'device-${DateTime.now().millisecondsSinceEpoch}-${Random().nextInt(1000000)}';
  }
}
```

### 3. API Service Class

```dart
import 'dart:convert';
import 'dart:async';
import 'package:http/http.dart' as http;

class AuthService {
  final String baseUrl;
  final TokenStorage _tokenStorage;
  final DeviceIdService _deviceIdService;

  AuthService({
    required this.baseUrl,
    required TokenStorage tokenStorage,
    required DeviceIdService deviceIdService,
  }) : _tokenStorage = tokenStorage,
       _deviceIdService = deviceIdService;

  // Guest Login / Register (ilk açılış)
  Future<LoginResponse> guestLogin() async {
    try {
      final deviceId = await _deviceIdService.getOrCreateDeviceId();
      await _tokenStorage.saveDeviceId(deviceId);

      final uri = Uri.parse('$baseUrl/api/users/LogIn');
      
      final response = await http.post(
        uri,
        headers: {
          'Content-Type': 'application/json',
        },
        body: json.encode({
          'deviceId': deviceId,
        }),
      );
      
      if (response.statusCode == 200) {
        final data = json.decode(response.body);
        if (data['success'] == true) {
          final userData = data['data'];
          
          // Token-ları saxla
          await _tokenStorage.saveTokens(
            accessToken: userData['token'],
            refreshToken: userData['refreshToken'],
            expiration: DateTime.parse(userData['expiration']),
            refreshExpiration: DateTime.parse(userData['refreshTokenExpiration']),
          );
          
          return LoginResponse.fromJson(userData);
        }
        throw Exception(data['message'] ?? 'Login failed');
      } else {
        final errorData = json.decode(response.body);
        throw Exception(errorData['message'] ?? 'Login failed');
      }
    } catch (e) {
      throw Exception('Failed to login: $e');
    }
  }

  // Token refresh
  Future<RefreshTokenResponse> refreshToken() async {
    try {
      final refreshTokenValue = await _tokenStorage.getRefreshToken();
      if (refreshTokenValue == null) {
        throw Exception('No refresh token available');
      }

      final uri = Uri.parse('$baseUrl/api/auth/refresh-token');
      
      final response = await http.post(
        uri,
        headers: {
          'Content-Type': 'application/json',
        },
        body: json.encode({
          'refreshToken': refreshTokenValue,
        }),
      );
      
      if (response.statusCode == 200) {
        final responseData = json.decode(response.body);
        if (responseData['success'] == true) {
          final data = responseData['data'];
          
          await _tokenStorage.saveTokens(
            accessToken: data['token'],
            refreshToken: data['refreshToken'],
            expiration: DateTime.parse(data['expiration']),
            refreshExpiration: DateTime.parse(data['refreshTokenExpiration']),
          );
          
          return RefreshTokenResponse.fromJson(data);
        }
        throw Exception(responseData['message'] ?? 'Token refresh failed');
      } else {
        final errorData = json.decode(response.body);
        throw Exception(errorData['message'] ?? 'Token refresh failed');
      }
    } catch (e) {
      throw Exception('Failed to refresh token: $e');
    }
  }

  // Token yoxla və lazım olsa refresh et
  Future<String> getValidToken() async {
    final token = await _tokenStorage.getAccessToken();
    
    if (token == null) {
      // Token yoxdursa guest login et
      await guestLogin();
      return (await _tokenStorage.getAccessToken())!;
    }

    // Token vaxtı bitməyə yaxındırsa refresh et
    if (await _tokenStorage.isTokenExpired()) {
      try {
        await refreshToken();
        return (await _tokenStorage.getAccessToken())!;
      } catch (e) {
        // Refresh uğursuz oldu, yenidən guest login et
        await guestLogin();
        return (await _tokenStorage.getAccessToken())!;
      }
    }

    return token;
  }
}

class ProfileService {
  final String baseUrl;
  final TokenStorage _tokenStorage;
  final AuthService _authService;

  ProfileService({
    required this.baseUrl,
    required TokenStorage tokenStorage,
    required AuthService authService,
  }) : _tokenStorage = tokenStorage,
       _authService = authService;

  // Profil statusu yoxlama
  Future<ProfileStatusDto> getProfileStatus() async {
    try {
      final token = await _authService.getValidToken();
      final uri = Uri.parse('$baseUrl/api/profile/status');
      
      final response = await http.get(
        uri,
        headers: {
          'Authorization': 'Bearer $token',
          'Content-Type': 'application/json',
        },
      );
      
      if (response.statusCode == 200) {
        final data = json.decode(response.body);
        if (data['success'] == true) {
          return ProfileStatusDto.fromJson(data['data']);
        }
        throw Exception(data['message'] ?? 'Unknown error');
      } else if (response.statusCode == 401) {
        // Token expired, refresh et və təkrarla
        await _authService.refreshToken();
        return getProfileStatus();
      } else {
        throw Exception('HTTP ${response.statusCode}: ${response.body}');
      }
    } catch (e) {
      throw Exception('Failed to get profile status: $e');
    }
  }

  // OTP göndərmə
  Future<OtpSendResponse> sendPhoneOtp(String phoneNumber) async {
    try {
      final token = await _authService.getValidToken();
      final uri = Uri.parse('$baseUrl/api/profile/send-phone-otp');
      
      final response = await http.post(
        uri,
        headers: {
          'Authorization': 'Bearer $token',
          'Content-Type': 'application/json',
        },
        body: json.encode({
          'phoneNumber': phoneNumber,
        }),
      );
      
      if (response.statusCode == 200) {
        final data = json.decode(response.body);
        if (data['success'] == true) {
          return OtpSendResponse.fromJson(data['data']);
        }
        throw Exception(data['message'] ?? 'OTP göndərilmədi');
      } else if (response.statusCode == 401) {
        await _authService.refreshToken();
        return sendPhoneOtp(phoneNumber);
      } else {
        final errorData = json.decode(response.body);
        throw Exception(errorData['message'] ?? 'OTP göndərilmədi');
      }
    } catch (e) {
      throw Exception('Failed to send OTP: $e');
    }
  }

  // OTP təsdiq etmə
  Future<bool> verifyPhoneOtp(String phoneNumber, String otpCode) async {
    try {
      final token = await _authService.getValidToken();
      final uri = Uri.parse('$baseUrl/api/profile/verify-phone-otp');
      
      final response = await http.post(
        uri,
        headers: {
          'Authorization': 'Bearer $token',
          'Content-Type': 'application/json',
        },
        body: json.encode({
          'phoneNumber': phoneNumber,
          'otpCode': otpCode,
        }),
      );
      
      if (response.statusCode == 200) {
        final data = json.decode(response.body);
        return data['success'] == true;
      } else if (response.statusCode == 401) {
        await _authService.refreshToken();
        return verifyPhoneOtp(phoneNumber, otpCode);
      } else {
        final errorData = json.decode(response.body);
        throw Exception(errorData['message'] ?? 'OTP təsdiqlənmədi');
      }
    } catch (e) {
      throw Exception('Failed to verify OTP: $e');
    }
  }

  // Profil tamamlama
  Future<CompleteProfileResponse> completeProfile({
    required String fullName,
    required String phoneNumber,
    String? email,
  }) async {
    try {
      final token = await _authService.getValidToken();
      final uri = Uri.parse('$baseUrl/api/profile/complete');
      
      final requestBody = {
        'fullName': fullName,
        'phoneNumber': phoneNumber,
        if (email != null && email.isNotEmpty) 'email': email,
      };
      
      final response = await http.post(
        uri,
        headers: {
          'Authorization': 'Bearer $token',
          'Content-Type': 'application/json',
        },
        body: json.encode(requestBody),
      );
      
      if (response.statusCode == 200) {
        final responseData = json.decode(response.body);
        if (responseData['success'] == true) {
          final data = responseData['data'];
          
          // Yeni token-ları saxla
          await _tokenStorage.saveTokens(
            accessToken: data['token'],
            refreshToken: data['refreshToken'],
            expiration: DateTime.parse(data['expiration']),
            refreshExpiration: DateTime.parse(data['refreshTokenExpiration']),
          );
          
          return CompleteProfileResponse.fromJson(data);
        }
        throw Exception(responseData['message'] ?? 'Profil tamamlanmadı');
      } else if (response.statusCode == 401) {
        await _authService.refreshToken();
        return completeProfile(fullName: fullName, phoneNumber: phoneNumber, email: email);
      } else {
        final errorData = json.decode(response.body);
        throw Exception(errorData['message'] ?? 'Profil tamamlanmadı');
      }
    } catch (e) {
      throw Exception('Failed to complete profile: $e');
    }
  }
}
```

### 4. DTO Classes

```dart
class LoginResponse {
  final int id;
  final String userName;
  final String firstName;
  final String lastName;
  final String? email;
  final DateTime? birthDate;
  final List<String> roles;
  final int? tenantId;
  final String token;
  final DateTime expiration;
  final String refreshToken;
  final DateTime refreshTokenExpiration;

  LoginResponse({
    required this.id,
    required this.userName,
    required this.firstName,
    required this.lastName,
    this.email,
    this.birthDate,
    required this.roles,
    this.tenantId,
    required this.token,
    required this.expiration,
    required this.refreshToken,
    required this.refreshTokenExpiration,
  });

  factory LoginResponse.fromJson(Map<String, dynamic> json) {
    return LoginResponse(
      id: json['id'] ?? 0,
      userName: json['userName'] ?? '',
      firstName: json['firstName'] ?? '',
      lastName: json['lastName'] ?? '',
      email: json['email'],
      birthDate: json['birthDate'] != null ? DateTime.parse(json['birthDate']) : null,
      roles: (json['roles'] as List<dynamic>?)?.map((e) => e.toString()).toList() ?? [],
      tenantId: json['tenantId'],
      token: json['token'] ?? '',
      expiration: DateTime.parse(json['expiration']),
      refreshToken: json['refreshToken'] ?? '',
      refreshTokenExpiration: DateTime.parse(json['refreshTokenExpiration']),
    );
  }
}

class ProfileStatusDto {
  final bool isGuest;
  final bool isProfileCompleted;
  final bool requiresProfileCompletion;

  ProfileStatusDto({
    required this.isGuest,
    required this.isProfileCompleted,
    required this.requiresProfileCompletion,
  });

  factory ProfileStatusDto.fromJson(Map<String, dynamic> json) {
    return ProfileStatusDto(
      isGuest: json['isGuest'] ?? false,
      isProfileCompleted: json['isProfileCompleted'] ?? false,
      requiresProfileCompletion: json['requiresProfileCompletion'] ?? false,
    );
  }
}

class OtpSendResponse {
  final String maskedPhone;
  final int expiresInMinutes;

  OtpSendResponse({
    required this.maskedPhone,
    required this.expiresInMinutes,
  });

  factory OtpSendResponse.fromJson(Map<String, dynamic> json) {
    return OtpSendResponse(
      maskedPhone: json['maskedPhone'] ?? '',
      expiresInMinutes: json['expiresInMinutes'] ?? 5,
    );
  }
}

class CompleteProfileResponse {
  final String token;
  final DateTime expiration;
  final String refreshToken;
  final DateTime refreshTokenExpiration;
  final int userId;
  final bool isGuest;
  final bool isProfileCompleted;
  final String userName;

  CompleteProfileResponse({
    required this.token,
    required this.expiration,
    required this.refreshToken,
    required this.refreshTokenExpiration,
    required this.userId,
    required this.isGuest,
    required this.isProfileCompleted,
    required this.userName,
  });

  factory CompleteProfileResponse.fromJson(Map<String, dynamic> json) {
    return CompleteProfileResponse(
      token: json['token'] ?? '',
      expiration: DateTime.parse(json['expiration']),
      refreshToken: json['refreshToken'] ?? '',
      refreshTokenExpiration: DateTime.parse(json['refreshTokenExpiration']),
      userId: json['userId'] ?? 0,
      isGuest: json['isGuest'] ?? false,
      isProfileCompleted: json['isProfileCompleted'] ?? false,
      userName: json['userName'] ?? '',
    );
  }
}

class RefreshTokenResponse {
  final String token;
  final DateTime expiration;
  final String refreshToken;
  final DateTime refreshTokenExpiration;
  final int userId;
  final bool isGuest;
  final bool isProfileCompleted;
  final String userName;

  RefreshTokenResponse({
    required this.token,
    required this.expiration,
    required this.refreshToken,
    required this.refreshTokenExpiration,
    required this.userId,
    required this.isGuest,
    required this.isProfileCompleted,
    required this.userName,
  });

  factory RefreshTokenResponse.fromJson(Map<String, dynamic> json) {
    return RefreshTokenResponse(
      token: json['token'] ?? '',
      expiration: DateTime.parse(json['expiration']),
      refreshToken: json['refreshToken'] ?? '',
      refreshTokenExpiration: DateTime.parse(json['refreshTokenExpiration']),
      userId: json['userId'] ?? 0,
      isGuest: json['isGuest'] ?? false,
      isProfileCompleted: json['isProfileCompleted'] ?? false,
      userName: json['userName'] ?? '',
    );
  }
}
```

### 5. App Initialization (Splash Screen)

```dart
import 'dart:async';
import 'package:flutter/material.dart';

class SplashScreen extends StatefulWidget {
  const SplashScreen({Key? key}) : super(key: key);

  @override
  State<SplashScreen> createState() => _SplashScreenState();
}

class _SplashScreenState extends State<SplashScreen> {
  final TokenStorage _tokenStorage = TokenStorage();
  final DeviceIdService _deviceIdService = DeviceIdService();
  final AuthService _authService = AuthService(
    baseUrl: 'YOUR_BASE_URL',
    tokenStorage: TokenStorage(),
    deviceIdService: DeviceIdService(),
  );
  final ProfileService _profileService = ProfileService(
    baseUrl: 'YOUR_BASE_URL',
    tokenStorage: TokenStorage(),
    authService: AuthService(
      baseUrl: 'YOUR_BASE_URL',
      tokenStorage: TokenStorage(),
      deviceIdService: DeviceIdService(),
    ),
  );

  @override
  void initState() {
    super.initState();
    _initializeApp();
  }

  Future<void> _initializeApp() async {
    try {
      // 1. Device ID yarat/al
      await _deviceIdService.getOrCreateDeviceId();

      // 2. Token yoxla
      final token = await _tokenStorage.getAccessToken();
      
      if (token == null) {
        // Token yoxdursa guest login et
        await _authService.guestLogin();
      } else {
        // Token varsa, vaxtı bitməyə yaxındırsa refresh et
        if (await _tokenStorage.isTokenExpired()) {
          try {
            await _authService.refreshToken();
          } catch (e) {
            // Refresh uğursuz oldu, yenidən guest login et
            await _authService.guestLogin();
          }
        }
      }

      // 3. Profil statusu yoxla
      if (mounted) {
        final status = await _profileService.getProfileStatus();
        
        if (status.requiresProfileCompletion) {
          // Profil tamamlama ekranına yönləndir
          Navigator.of(context).pushReplacement(
            MaterialPageRoute(
              builder: (context) => const ProfileCompletionScreen(),
            ),
          );
        } else {
          // Ana səhifəyə yönləndir
          Navigator.of(context).pushReplacementNamed('/home');
        }
      }
    } catch (e) {
      // Xəta halında guest login cəhd et
      try {
        await _authService.guestLogin();
        if (mounted) {
          Navigator.of(context).pushReplacementNamed('/home');
        }
      } catch (e2) {
        // Hətta guest login də uğursuz oldu
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(
              content: Text('Xəta: ${e2.toString()}'),
              backgroundColor: Colors.red,
            ),
          );
        }
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return const Scaffold(
      body: Center(
        child: CircularProgressIndicator(),
      ),
    );
  }
}
```

### 6. Profile Completion Screen

```dart
import 'dart:async';
import 'package:flutter/material.dart';

class ProfileCompletionScreen extends StatefulWidget {
  const ProfileCompletionScreen({Key? key}) : super(key: key);

  @override
  State<ProfileCompletionScreen> createState() => _ProfileCompletionScreenState();
}

class _ProfileCompletionScreenState extends State<ProfileCompletionScreen> {
  final _formKey = GlobalKey<FormState>();
  final _nameController = TextEditingController();
  final _phoneController = TextEditingController();
  final _emailController = TextEditingController();
  final _otpController = TextEditingController();
  
  final TokenStorage _tokenStorage = TokenStorage();
  final AuthService _authService = AuthService(
    baseUrl: 'YOUR_BASE_URL',
    tokenStorage: TokenStorage(),
    deviceIdService: DeviceIdService(),
  );
  final ProfileService _profileService = ProfileService(
    baseUrl: 'YOUR_BASE_URL',
    tokenStorage: TokenStorage(),
    authService: AuthService(
      baseUrl: 'YOUR_BASE_URL',
      tokenStorage: TokenStorage(),
      deviceIdService: DeviceIdService(),
    ),
  );
  
  bool _isLoading = false;
  bool _otpSent = false;
  bool _phoneVerified = false;
  String? _maskedPhone;
  int _otpExpiresIn = 0;
  Timer? _otpTimer;

  @override
  void dispose() {
    _nameController.dispose();
    _phoneController.dispose();
    _emailController.dispose();
    _otpController.dispose();
    _otpTimer?.cancel();
    super.dispose();
  }

  Future<void> _sendOtp() async {
    if (!_formKey.currentState!.validate()) return;
    
    setState(() {
      _isLoading = true;
    });

    try {
      final response = await _profileService.sendPhoneOtp(_phoneController.text);
      
      setState(() {
        _otpSent = true;
        _maskedPhone = response.maskedPhone;
        _otpExpiresIn = response.expiresInMinutes * 60; // seconds
        _isLoading = false;
      });

      // OTP countdown timer
      _startOtpTimer();

      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('OTP kodu ${response.maskedPhone} nömrəsinə göndərildi'),
            backgroundColor: Colors.green,
          ),
        );
      }
    } catch (e) {
      setState(() {
        _isLoading = false;
      });
      
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Xəta: ${e.toString()}'),
            backgroundColor: Colors.red,
          ),
        );
      }
    }
  }

  Future<void> _verifyOtp() async {
    if (_otpController.text.length != 6) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('OTP kodu 6 rəqəmli olmalıdır'),
          backgroundColor: Colors.red,
        ),
      );
      return;
    }

    setState(() {
      _isLoading = true;
    });

    try {
      final verified = await _profileService.verifyPhoneOtp(
        _phoneController.text,
        _otpController.text,
      );

      if (verified) {
        setState(() {
          _phoneVerified = true;
          _isLoading = false;
        });

        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(
              content: Text('Telefon nömrəsi təsdiqləndi'),
              backgroundColor: Colors.green,
            ),
          );
        }
      } else {
        setState(() {
          _isLoading = false;
        });
        
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(
              content: Text('Yanlış OTP kodu'),
              backgroundColor: Colors.red,
            ),
          );
        }
      }
    } catch (e) {
      setState(() {
        _isLoading = false;
      });
      
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Xəta: ${e.toString()}'),
            backgroundColor: Colors.red,
          ),
        );
      }
    }
  }

  Future<void> _completeProfile() async {
    if (!_formKey.currentState!.validate()) return;
    
    if (!_phoneVerified) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Zəhmət olmasa telefon nömrəsini təsdiqləyin'),
          backgroundColor: Colors.red,
        ),
      );
      return;
    }

    setState(() {
      _isLoading = true;
    });

    try {
      final response = await _profileService.completeProfile(
        fullName: _nameController.text.trim(),
        phoneNumber: _phoneController.text.trim(),
        email: _emailController.text.trim().isEmpty 
            ? null 
            : _emailController.text.trim(),
      );

      if (mounted) {
        // Profil tamamlandı, ana səhifəyə qayıt
        Navigator.of(context).pushReplacementNamed('/home');
        
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Profil uğurla tamamlandı!'),
            backgroundColor: Colors.green,
          ),
        );
      }
    } catch (e) {
      setState(() {
        _isLoading = false;
      });
      
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Xəta: ${e.toString()}'),
            backgroundColor: Colors.red,
          ),
        );
      }
    }
  }

  void _startOtpTimer() {
    _otpTimer?.cancel();
    _otpTimer = Timer.periodic(const Duration(seconds: 1), (timer) {
      if (_otpExpiresIn > 0) {
        setState(() {
          _otpExpiresIn--;
        });
      } else {
        timer.cancel();
        setState(() {
          _otpSent = false;
        });
      }
    });
  }

  String _formatOtpTime(int seconds) {
    final minutes = seconds ~/ 60;
    final secs = seconds % 60;
    return '${minutes.toString().padLeft(2, '0')}:${secs.toString().padLeft(2, '0')}';
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Profil Tamamlama'),
        centerTitle: true,
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(16.0),
        child: Form(
          key: _formKey,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const SizedBox(height: 20),
              
              // Ad Soyad
              TextFormField(
                controller: _nameController,
                decoration: const InputDecoration(
                  labelText: 'Ad Soyad *',
                  hintText: 'Məs: John Doe',
                  border: OutlineInputBorder(),
                  prefixIcon: Icon(Icons.person),
                ),
                validator: (value) {
                  if (value == null || value.trim().isEmpty) {
                    return 'Ad soyad daxil edin';
                  }
                  if (value.trim().length < 2) {
                    return 'Ad soyad ən azı 2 simvol olmalıdır';
                  }
                  return null;
                },
                textCapitalization: TextCapitalization.words,
              ),
              
              const SizedBox(height: 16),
              
              // Telefon nömrəsi
              TextFormField(
                controller: _phoneController,
                decoration: InputDecoration(
                  labelText: 'Telefon nömrəsi *',
                  hintText: '+994501234567',
                  border: const OutlineInputBorder(),
                  prefixIcon: const Icon(Icons.phone),
                  suffixIcon: _otpSent && !_phoneVerified
                      ? IconButton(
                          icon: const Icon(Icons.refresh),
                          onPressed: _otpExpiresIn > 0 ? null : _sendOtp,
                          tooltip: 'Yenidən göndər',
                        )
                      : null,
                ),
                keyboardType: TextInputType.phone,
                enabled: !_phoneVerified,
                validator: (value) {
                  if (value == null || value.trim().isEmpty) {
                    return 'Telefon nömrəsi daxil edin';
                  }
                  final phoneRegex = RegExp(r'^\+994\d{9}$');
                  if (!phoneRegex.hasMatch(value.trim())) {
                    return 'Düzgün telefon nömrəsi daxil edin (+994XXXXXXXXX)';
                  }
                  return null;
                },
              ),
              
              const SizedBox(height: 16),
              
              // OTP göndərmə düyməsi
              if (!_otpSent && !_phoneVerified)
                ElevatedButton.icon(
                  onPressed: _isLoading ? null : _sendOtp,
                  icon: const Icon(Icons.send),
                  label: const Text('OTP Kodu Göndər'),
                  style: ElevatedButton.styleFrom(
                    padding: const EdgeInsets.symmetric(vertical: 16),
                  ),
                ),
              
              // OTP input (göndərildikdən sonra)
              if (_otpSent && !_phoneVerified) ...[
                const SizedBox(height: 8),
                Text(
                  'OTP kodu ${_maskedPhone ?? _phoneController.text} nömrəsinə göndərildi',
                  style: Theme.of(context).textTheme.bodySmall?.copyWith(
                    color: Colors.green,
                  ),
                ),
                if (_otpExpiresIn > 0)
                  Text(
                    'Qalan vaxt: ${_formatOtpTime(_otpExpiresIn)}',
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: Colors.orange,
                    ),
                  ),
                const SizedBox(height: 16),
                TextFormField(
                  controller: _otpController,
                  decoration: const InputDecoration(
                    labelText: 'OTP Kodu',
                    hintText: '123456',
                    border: OutlineInputBorder(),
                    prefixIcon: Icon(Icons.lock),
                  ),
                  keyboardType: TextInputType.number,
                  maxLength: 6,
                  textAlign: TextAlign.center,
                  style: const TextStyle(
                    fontSize: 24,
                    letterSpacing: 8,
                  ),
                  validator: (value) {
                    if (value == null || value.length != 6) {
                      return 'OTP kodu 6 rəqəmli olmalıdır';
                    }
                    return null;
                  },
                  onChanged: (value) {
                    // 6 rəqəm daxil edildikdə avtomatik təsdiq et
                    if (value.length == 6) {
                      _verifyOtp();
                    }
                  },
                ),
                const SizedBox(height: 16),
                ElevatedButton.icon(
                  onPressed: _isLoading ? null : _verifyOtp,
                  icon: const Icon(Icons.verified),
                  label: const Text('OTP Təsdiq Et'),
                  style: ElevatedButton.styleFrom(
                    padding: const EdgeInsets.symmetric(vertical: 16),
                  ),
                ),
                const SizedBox(height: 8),
                TextButton(
                  onPressed: _otpExpiresIn > 0 ? null : _sendOtp,
                  child: const Text('Kodu yenidən göndər'),
                ),
              ],
              
              // Telefon təsdiqləndi mesajı
              if (_phoneVerified) ...[
                const SizedBox(height: 8),
                Row(
                  children: [
                    const Icon(Icons.check_circle, color: Colors.green),
                    const SizedBox(width: 8),
                    Text(
                      'Telefon nömrəsi təsdiqləndi',
                      style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                        color: Colors.green,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                  ],
                ),
              ],
              
              const SizedBox(height: 16),
              
              // Email (optional)
              TextFormField(
                controller: _emailController,
                decoration: const InputDecoration(
                  labelText: 'Email (İstəyə bağlı)',
                  hintText: 'example@email.com',
                  border: OutlineInputBorder(),
                  prefixIcon: Icon(Icons.email),
                ),
                keyboardType: TextInputType.emailAddress,
                validator: (value) {
                  if (value != null && value.trim().isNotEmpty) {
                    final emailRegex = RegExp(
                      r'^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$',
                    );
                    if (!emailRegex.hasMatch(value.trim())) {
                      return 'Düzgün email daxil edin';
                    }
                  }
                  return null;
                },
              ),
              
              const SizedBox(height: 32),
              
              // Profil tamamlama düyməsi
              ElevatedButton(
                onPressed: (_isLoading || !_phoneVerified) ? null : _completeProfile,
                style: ElevatedButton.styleFrom(
                  padding: const EdgeInsets.symmetric(vertical: 16),
                  backgroundColor: _phoneVerified ? null : Colors.grey,
                ),
                child: _isLoading
                    ? const SizedBox(
                        height: 20,
                        width: 20,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Text(
                        'Profili Tamamla',
                        style: TextStyle(fontSize: 16),
                      ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
```

### 7. HTTP Client Wrapper (Auto Token Refresh)

```dart
import 'dart:convert';
import 'package:http/http.dart' as http;

class ApiClient {
  final String baseUrl;
  final AuthService _authService;

  ApiClient({
    required this.baseUrl,
    required AuthService authService,
  }) : _authService = authService;

  Future<http.Response> get(
    String endpoint, {
    Map<String, String>? headers,
    bool requireAuth = true,
  }) async {
    return _makeRequest(
      'GET',
      endpoint,
      headers: headers,
      requireAuth: requireAuth,
    );
  }

  Future<http.Response> post(
    String endpoint, {
    Map<String, String>? headers,
    Object? body,
    bool requireAuth = true,
  }) async {
    return _makeRequest(
      'POST',
      endpoint,
      headers: headers,
      body: body,
      requireAuth: requireAuth,
    );
  }

  Future<http.Response> _makeRequest(
    String method,
    String endpoint, {
    Map<String, String>? headers,
    Object? body,
    bool requireAuth = true,
  }) async {
    final uri = Uri.parse('$baseUrl$endpoint');
    final requestHeaders = <String, String>{
      'Content-Type': 'application/json',
      ...?headers,
    };

    if (requireAuth) {
      final token = await _authService.getValidToken();
      requestHeaders['Authorization'] = 'Bearer $token';
    }

    http.Response response;
    if (method == 'GET') {
      response = await http.get(uri, headers: requestHeaders);
    } else if (method == 'POST') {
      response = await http.post(
        uri,
        headers: requestHeaders,
        body: body is String ? body : json.encode(body),
      );
    } else {
      throw UnsupportedError('Method $method not supported');
    }

    // 401 xətası alındıqda token refresh et və təkrarla
    if (response.statusCode == 401 && requireAuth) {
      await _authService.refreshToken();
      
      // Yeni token ilə request-i təkrarla
      final newToken = await _authService.getValidToken();
      requestHeaders['Authorization'] = 'Bearer $newToken';
      
      if (method == 'GET') {
        response = await http.get(uri, headers: requestHeaders);
      } else if (method == 'POST') {
        response = await http.post(
          uri,
          headers: requestHeaders,
          body: body is String ? body : json.encode(body),
        );
      }
    }

    return response;
  }
}
```

### 8. Token Refresh Background Task

```dart
import 'dart:async';

class TokenRefreshScheduler {
  final AuthService _authService;
  Timer? _refreshTimer;

  TokenRefreshScheduler(AuthService authService) : _authService = authService;

  void start() {
    // Hər 30 dəqiqədə bir token refresh et
    _refreshTimer = Timer.periodic(const Duration(minutes: 30), (timer) async {
      try {
        final tokenStorage = TokenStorage();
        final token = await tokenStorage.getAccessToken();
        
        if (token != null) {
          // Token vaxtı bitməyə yaxındırsa refresh et
          if (await tokenStorage.isTokenExpired()) {
            await _authService.refreshToken();
          }
        }
      } catch (e) {
        // Xəta halında növbəti dəfə cəhd edəcək
        print('Token refresh error: $e');
      }
    });
  }

  void stop() {
    _refreshTimer?.cancel();
  }
}
```

## App Flow

### 1. App Start (Splash Screen)
```
App Start
  ↓
Device ID yarat/al
  ↓
Token yoxla
  ├─ Token yoxdursa → Guest Login
  └─ Token varsa → Token vaxtı yoxla
      ├─ Vaxtı bitməyə yaxındırsa → Refresh Token
      └─ Vaxtı bitməyibsə → Davam et
  ↓
Profil statusu yoxla
  ├─ Profil tamamlanmayıbsa → Profile Completion Screen
  └─ Profil tamamlanıbsa → Home Screen
```

### 2. Guest Login Flow
```
POST /api/users/LogIn
{
  "deviceId": "device-123"
}
  ↓
Guest User yaradılır (və ya mövcud session tapılır)
  ↓
JWT + Refresh Token qaytarılır
  ↓
Token-lar storage-da saxlanılır
```

### 3. Token Refresh Flow
```
API Request
  ↓
401 Unauthorized?
  ├─ Bəli → Refresh Token
  │   ├─ Uğurlu → Yeni token ilə request təkrarla
  │   └─ Uğursuz → Guest Login
  └─ Xeyr → Normal response
```

### 4. Profile Completion Flow
```
Profile Status Check
  ↓
requiresProfileCompletion = true?
  ├─ Bəli → Profile Completion Screen
  │   ↓
  │   Ad Soyad daxil et
  │   ↓
  │   Telefon nömrəsi daxil et
  │   ↓
  │   OTP göndər
  │   ↓
  │   OTP təsdiq et
  │   ↓
  │   Profil tamamla
  │   ↓
  │   Yeni token al
  │   ↓
  │   Home Screen
  └─ Xeyr → Home Screen
```

## UX/UI Tövsiyələri

1. **Splash Screen**: App açılanda guest login avtomatik edilsin
2. **Token Refresh**: Background-da avtomatik refresh edilsin
3. **Profile Reminder**: Profil tamamlanmayıbsa, non-blocking reminder göstər
4. **OTP Auto-submit**: 6 rəqəm daxil edildikdə avtomatik təsdiq et
5. **Loading States**: Bütün async əməliyyatlarda loading göstər
6. **Error Handling**: Aydın xəta mesajları
7. **Offline Support**: Network yoxdursa, cached token istifadə et

## Test Ssenariləri

1. ✅ İlk açılış - guest login
2. ✅ Token refresh - avtomatik refresh
3. ✅ Profil tamamlama - OTP ilə
4. ✅ Token expired - avtomatik refresh
5. ✅ Network xətası - offline handling
6. ✅ App restart - token-lar saxlanılır

## Qeydlər

**Backend-də lazım olan endpoint-lər:**
- `POST /api/profile/send-phone-otp` - OTP göndərmə
- `POST /api/profile/verify-phone-otp` - OTP təsdiq

Bu endpoint-lər hələ yoxdursa, backend-də əlavə edilməlidir.

**Device ID:**
- Android: `androidInfo.id`
- iOS: `iosInfo.identifierForVendor`
- Fallback: Random UUID

**Token Refresh:**
- Access token 24 saat etibarlıdır
- Refresh token 30 gün etibarlıdır
- Token vaxtı bitməyə 5 dəqiqə qalmış refresh edilir

