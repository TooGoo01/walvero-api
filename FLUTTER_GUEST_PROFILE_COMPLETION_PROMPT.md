# Flutter: Guest User Profil Tamamlama və OTP Təsdiqi

## Məqsəd
Guest user-lərin profillərini tamamlamaq, telefon nömrəsini OTP ilə təsdiqləmək və tam istifadəçiyə çevrilmək.

## API Endpoint-ləri

### 1. Profil Statusu Yoxlama
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

### 2. Telefon Nömrəsinə OTP Göndərmə
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

### 3. OTP Təsdiq Etmə
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

### 4. Profil Tamamlama
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

### 5. Token Refresh
```
POST /api/auth/refresh-token
Content-Type: application/json

{
  "refreshToken": "refresh-token-string"
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

### 1. API Service Class

```dart
import 'dart:convert';
import 'dart:async';
import 'package:http/http.dart' as http;

class ProfileService {
  final String baseUrl;
  final Future<String> Function() getToken;
  final Future<void> Function({
    required String accessToken,
    required String refreshToken,
    required DateTime expiration,
    required DateTime refreshExpiration,
  }) saveTokens;

  ProfileService({
    required this.baseUrl,
    required this.getToken,
    required this.saveTokens,
  });

  // Profil statusu yoxlama
  Future<ProfileStatusDto> getProfileStatus() async {
    try {
      final token = await getToken();
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
      final token = await getToken();
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
      final token = await getToken();
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
      final token = await getToken();
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
          await saveTokens(
            accessToken: data['token'],
            refreshToken: data['refreshToken'],
            expiration: DateTime.parse(data['expiration']),
            refreshExpiration: DateTime.parse(data['refreshTokenExpiration']),
          );
          
          return CompleteProfileResponse.fromJson(data);
        }
        throw Exception(responseData['message'] ?? 'Profil tamamlanmadı');
      } else {
        final errorData = json.decode(response.body);
        throw Exception(errorData['message'] ?? 'Profil tamamlanmadı');
      }
    } catch (e) {
      throw Exception('Failed to complete profile: $e');
    }
  }

  // Token refresh
  Future<RefreshTokenResponse> refreshToken(String refreshTokenValue) async {
    try {
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
          
          await saveTokens(
            accessToken: data['token'],
            refreshToken: data['refreshToken'],
            expiration: DateTime.parse(data['expiration']),
            refreshExpiration: DateTime.parse(data['refreshTokenExpiration']),
          );
          
          return RefreshTokenResponse.fromJson(data);
        }
        throw Exception(responseData['message'] ?? 'Token yenilənmədi');
      } else {
        final errorData = json.decode(response.body);
        throw Exception(errorData['message'] ?? 'Token yenilənmədi');
      }
    } catch (e) {
      throw Exception('Failed to refresh token: $e');
    }
  }
}
```

### 2. DTO Classes

```dart
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

### 3. Profile Completion Screen

```dart
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
  
  late final ProfileService _profileService;
  
  @override
  void initState() {
    super.initState();
    // ProfileService-i initialize et
    _profileService = ProfileService(
      baseUrl: 'YOUR_BASE_URL', // Məs: 'https://api.example.com'
      getToken: () async {
        // Token storage-dan token al
        // SharedPreferences, SecureStorage və s.
        // Məs: return await secureStorage.read(key: 'access_token') ?? '';
        return '';
      },
      saveTokens: ({
        required String accessToken,
        required String refreshToken,
        required DateTime expiration,
        required DateTime refreshExpiration,
      }) async {
        // Token-ları storage-da saxla
        // SharedPreferences, SecureStorage və s.
        // Məs:
        // await secureStorage.write(key: 'access_token', value: accessToken);
        // await secureStorage.write(key: 'refresh_token', value: refreshToken);
        // await secureStorage.write(key: 'token_expiration', value: expiration.toIso8601String());
        // await secureStorage.write(key: 'refresh_expiration', value: refreshExpiration.toIso8601String());
      },
    );
  }
  
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
                  // Telefon nömrəsi formatı yoxlaması
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

### 4. Profile Status Check (App Start-da)

```dart
import 'package:flutter/material.dart';

class SplashScreen extends StatefulWidget {
  const SplashScreen({Key? key}) : super(key: key);

  @override
  State<SplashScreen> createState() => _SplashScreenState();
}

class _SplashScreenState extends State<SplashScreen> {
  @override
  void initState() {
    super.initState();
    _checkProfileStatus();
  }

  Future<void> _checkProfileStatus() async {
    try {
      // Token storage-dan token al (məs: SecureStorage, SharedPreferences)
      // final tokenStorage = SecureStorage();
      // final accessToken = await tokenStorage.read(key: 'access_token') ?? '';
      
      final profileService = ProfileService(
        baseUrl: 'YOUR_BASE_URL', // Məs: 'https://api.example.com'
        getToken: () async {
          // Token storage-dan token al
          // return await tokenStorage.read(key: 'access_token') ?? '';
          return '';
        },
        saveTokens: ({
          required String accessToken,
          required String refreshToken,
          required DateTime expiration,
          required DateTime refreshExpiration,
        }) async {
          // Token-ları storage-da saxla
          // await tokenStorage.write(key: 'access_token', value: accessToken);
          // await tokenStorage.write(key: 'refresh_token', value: refreshToken);
          // await tokenStorage.write(key: 'token_expiration', value: expiration.toIso8601String());
          // await tokenStorage.write(key: 'refresh_expiration', value: refreshExpiration.toIso8601String());
        },
      );
      final status = await profileService.getProfileStatus();
      
      if (mounted) {
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
      // Xəta halında ana səhifəyə yönləndir
      if (mounted) {
        Navigator.of(context).pushReplacementNamed('/home');
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

### 5. HTTP Client Wrapper (Token Refresh ilə)

```dart
import 'dart:convert';
import 'package:http/http.dart' as http;

class ApiClient {
  final String baseUrl;
  final Future<String?> Function() getAccessToken;
  final Future<String?> Function() getRefreshToken;
  final Future<void> Function({
    required String accessToken,
    required String refreshToken,
    required DateTime expiration,
    required DateTime refreshExpiration,
  }) saveTokens;
  final Future<void> Function() onTokenRefreshFailed;

  ApiClient({
    required this.baseUrl,
    required this.getAccessToken,
    required this.getRefreshToken,
    required this.saveTokens,
    required this.onTokenRefreshFailed,
  });

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
      final token = await getAccessToken();
      if (token != null) {
        requestHeaders['Authorization'] = 'Bearer $token';
      }
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

    // 401 xətası alındıqda token refresh et
    if (response.statusCode == 401 && requireAuth) {
      final refreshed = await _refreshTokenAndRetry(
        method,
        uri,
        requestHeaders,
        body,
      );
      if (refreshed != null) {
        return refreshed;
      }
    }

    return response;
  }

  Future<http.Response?> _refreshTokenAndRetry(
    String method,
    Uri uri,
    Map<String, String> headers,
    Object? body,
  ) async {
    try {
      final refreshTokenValue = await getRefreshToken();
      if (refreshTokenValue == null) {
        await onTokenRefreshFailed();
        return null;
      }

      // Token refresh et
      final profileService = ProfileService(
        baseUrl: baseUrl,
        getToken: getAccessToken,
        saveTokens: saveTokens,
      );
      
      final refreshResponse = await profileService.refreshToken(refreshTokenValue);
      
      // Yeni token ilə request-i təkrarla
      headers['Authorization'] = 'Bearer ${refreshResponse.token}';
      
      if (method == 'GET') {
        return await http.get(uri, headers: headers);
      } else if (method == 'POST') {
        return await http.post(
          uri,
          headers: headers,
          body: body is String ? body : json.encode(body),
        );
      }
    } catch (e) {
      // Refresh failed, login ekranına yönləndir
      await onTokenRefreshFailed();
    }
    return null;
  }
}

// İstifadə nümunəsi:
class ProfileService {
  final ApiClient _client;

  ProfileService(this._client);

  Future<ProfileStatusDto> getProfileStatus() async {
    final response = await _client.get('/api/profile/status');
    
    if (response.statusCode == 200) {
      final data = json.decode(response.body);
      if (data['success'] == true) {
        return ProfileStatusDto.fromJson(data['data']);
      }
      throw Exception(data['message'] ?? 'Unknown error');
    }
    throw Exception('HTTP ${response.statusCode}: ${response.body}');
  }

  // ... digər metodlar eyni şəkildə
}
```

## UX/UI Tövsiyələri

1. **OTP Countdown Timer**: OTP göndərildikdən sonra geri sayım göstərin
2. **Auto-focus**: OTP input-a avtomatik focus
3. **Auto-submit**: 6 rəqəm daxil edildikdə avtomatik təsdiq et
4. **Resend OTP**: Vaxt bitdikdən sonra "Yenidən göndər" düyməsi
5. **Phone Formatting**: Telefon nömrəsini formatla (+994 XX XXX XX XX)
6. **Loading States**: Bütün async əməliyyatlarda loading göstər
7. **Error Handling**: Aydın xəta mesajları
8. **Success Feedback**: Uğurlu əməliyyatlarda snackbar/alert

## Test Ssenariləri

1. ✅ Guest user profil tamamlama
2. ✅ OTP göndərmə və təsdiq
3. ✅ Yanlış OTP kodu
4. ✅ OTP vaxtının bitməsi
5. ✅ Token refresh
6. ✅ Network xətası
7. ✅ Profil artıq tamamlanıbsa

## Qeydlər

**Backend-də lazım olan endpoint-lər:**
- `POST /api/profile/send-phone-otp` - OTP göndərmə
- `POST /api/profile/verify-phone-otp` - OTP təsdiq

Bu endpoint-lər hələ yoxdursa, backend-də əlavə edilməlidir.

