# Flutter Profile Update Implementation Guide

## API Endpoints

### 1. Update Profile
```
POST /api/profile/update
Authorization: Bearer {token}
Content-Type: application/json

Request Body:
{
  "name": "John",
  "surname": "Doe",
  "email": "john.doe@example.com",
  "phoneNumber": "+994501234567", // Optional - only if changing phone
  "gender": 0, // Optional: 0 = Male, 1 = Female
  "dateOfBirth": "1990-01-01T00:00:00Z" // Optional
}

Response:
{
  "success": true,
  "data": {
    "requiresOldPhoneVerification": false,
    "requiresNewPhoneVerification": false,
    "message": "Profile updated successfully.",
    "pendingNewPhoneNumber": null
  }
}
```

### 2. Verify Old Phone Number
```
POST /api/profile/verify-old-phone
Authorization: Bearer {token}
Content-Type: application/json

Request Body:
{
  "otpCode": "123456"
}

Response:
{
  "success": true,
  "message": "Old phone number verified successfully. Please proceed with new phone verification."
}
```

### 3. Verify New Phone Number
```
POST /api/profile/verify-new-phone
Authorization: Bearer {token}
Content-Type: application/json

Request Body:
{
  "newPhoneNumber": "+994501234567",
  "otpCode": "123456"
}

Response:
{
  "success": true,
  "message": "Phone number updated successfully."
}
```

### 4. Resend Phone Update OTP
```
POST /api/profile/resend-phone-update-otp
Authorization: Bearer {token}
Content-Type: application/json

Request Body (for old phone):
{
  "otpType": "OldPhone"
}

Request Body (for new phone):
{
  "otpType": "NewPhone",
  "newPhoneNumber": "+994501234567"
}

Response:
{
  "success": true,
  "message": "OTP has been re-sent to your current phone number."
}
```

## Implementation Flow

### Scenario 1: Update Profile Without Phone Change
1. User fills profile form (name, surname, email, etc.)
2. Call `POST /api/profile/update` without `phoneNumber` field
3. If success, show success message
4. Update local user data

### Scenario 2: Update Profile With Phone Change
1. User fills profile form including new phone number
2. Call `POST /api/profile/update` with `phoneNumber` field
3. Check response:
   - If `requiresOldPhoneVerification == true`:
     - Show OTP input screen for old phone
     - User enters OTP received on current phone
     - Call `POST /api/profile/verify-old-phone`
     - If success, automatically proceed to new phone verification
   - If `requiresNewPhoneVerification == true`:
     - Show OTP input screen for new phone
     - Display `pendingNewPhoneNumber` to user
     - User enters OTP received on new phone
     - Call `POST /api/profile/verify-new-phone` with new phone number and OTP
     - If success, show success message and update local user data

### Scenario 3: Wrong Phone Number Entered
1. User enters wrong phone number in update form
2. System sends OTP to wrong number
3. User realizes mistake
4. User calls `POST /api/profile/update` again with correct phone number
5. System cancels old OTP and sends new OTP to correct number
6. User verifies with correct OTP

### Scenario 4: OTP Resend
1. If OTP expired or not received:
   - For old phone: Call `POST /api/profile/resend-phone-update-otp` with `otpType: "OldPhone"`
   - For new phone: Call `POST /api/profile/resend-phone-update-otp` with `otpType: "NewPhone"` and `newPhoneNumber`
2. Show "OTP has been re-sent" message
3. User enters new OTP

## Flutter Implementation

### 1. Create Models

```dart
// update_profile_dto.dart
class UpdateProfileDto {
  final String? name;
  final String? surname;
  final String? email;
  final String? phoneNumber;
  final int? gender; // 0 = Male, 1 = Female
  final DateTime? dateOfBirth;

  UpdateProfileDto({
    this.name,
    this.surname,
    this.email,
    this.phoneNumber,
    this.gender,
    this.dateOfBirth,
  });

  Map<String, dynamic> toJson() {
    return {
      if (name != null) 'name': name,
      if (surname != null) 'surname': surname,
      if (email != null) 'email': email,
      if (phoneNumber != null) 'phoneNumber': phoneNumber,
      if (gender != null) 'gender': gender,
      if (dateOfBirth != null) 'dateOfBirth': dateOfBirth?.toIso8601String(),
    };
  }
}

// update_profile_response_dto.dart
class UpdateProfileResponseDto {
  final bool requiresOldPhoneVerification;
  final bool requiresNewPhoneVerification;
  final String? message;
  final String? pendingNewPhoneNumber;

  UpdateProfileResponseDto({
    required this.requiresOldPhoneVerification,
    required this.requiresNewPhoneVerification,
    this.message,
    this.pendingNewPhoneNumber,
  });

  factory UpdateProfileResponseDto.fromJson(Map<String, dynamic> json) {
    return UpdateProfileResponseDto(
      requiresOldPhoneVerification: json['requiresOldPhoneVerification'] ?? false,
      requiresNewPhoneVerification: json['requiresNewPhoneVerification'] ?? false,
      message: json['message'],
      pendingNewPhoneNumber: json['pendingNewPhoneNumber'],
    );
  }
}

// verify_old_phone_dto.dart
class VerifyOldPhoneDto {
  final String otpCode;

  VerifyOldPhoneDto({required this.otpCode});

  Map<String, dynamic> toJson() => {'otpCode': otpCode};
}

// verify_new_phone_dto.dart
class VerifyNewPhoneDto {
  final String newPhoneNumber;
  final String otpCode;

  VerifyNewPhoneDto({
    required this.newPhoneNumber,
    required this.otpCode,
  });

  Map<String, dynamic> toJson() => {
    'newPhoneNumber': newPhoneNumber,
    'otpCode': otpCode,
  };
}

// resend_phone_update_otp_dto.dart
class ResendPhoneUpdateOtpDto {
  final String otpType; // "OldPhone" or "NewPhone"
  final String? newPhoneNumber; // Required only for "NewPhone"

  ResendPhoneUpdateOtpDto({
    required this.otpType,
    this.newPhoneNumber,
  });

  Map<String, dynamic> toJson() {
    final json = {'otpType': otpType};
    if (newPhoneNumber != null) {
      json['newPhoneNumber'] = newPhoneNumber;
    }
    return json;
  }
}
```

### 2. Create API Service

```dart
// profile_service.dart
class ProfileService {
  final ApiClient _apiClient;

  ProfileService(this._apiClient);

  Future<ServiceResult<UpdateProfileResponseDto>> updateProfile(
    UpdateProfileDto dto,
  ) async {
    try {
      final response = await _apiClient.post(
        '/api/profile/update',
        data: dto.toJson(),
      );
      
      if (response.data['success'] == true) {
        return ServiceResult.success(
          UpdateProfileResponseDto.fromJson(response.data['data']),
          message: response.data['data']['message'],
        );
      } else {
        return ServiceResult.failure(
          response.data['message'] ?? 'Failed to update profile',
        );
      }
    } catch (e) {
      return ServiceResult.failure('Error updating profile: $e');
    }
  }

  Future<ServiceResult> verifyOldPhone(String otpCode) async {
    try {
      final response = await _apiClient.post(
        '/api/profile/verify-old-phone',
        data: VerifyOldPhoneDto(otpCode: otpCode).toJson(),
      );
      
      if (response.data['success'] == true) {
        return ServiceResult.success(
          null,
          message: response.data['message'],
        );
      } else {
        return ServiceResult.failure(
          response.data['message'] ?? 'Failed to verify old phone',
        );
      }
    } catch (e) {
      return ServiceResult.failure('Error verifying old phone: $e');
    }
  }

  Future<ServiceResult> verifyNewPhone(
    String newPhoneNumber,
    String otpCode,
  ) async {
    try {
      final response = await _apiClient.post(
        '/api/profile/verify-new-phone',
        data: VerifyNewPhoneDto(
          newPhoneNumber: newPhoneNumber,
          otpCode: otpCode,
        ).toJson(),
      );
      
      if (response.data['success'] == true) {
        return ServiceResult.success(
          null,
          message: response.data['message'],
        );
      } else {
        return ServiceResult.failure(
          response.data['message'] ?? 'Failed to verify new phone',
        );
      }
    } catch (e) {
      return ServiceResult.failure('Error verifying new phone: $e');
    }
  }

  Future<ServiceResult> resendPhoneUpdateOtp(
    ResendPhoneUpdateOtpDto dto,
  ) async {
    try {
      final response = await _apiClient.post(
        '/api/profile/resend-phone-update-otp',
        data: dto.toJson(),
      );
      
      if (response.data['success'] == true) {
        return ServiceResult.success(
          null,
          message: response.data['message'],
        );
      } else {
        return ServiceResult.failure(
          response.data['message'] ?? 'Failed to resend OTP',
        );
      }
    } catch (e) {
      return ServiceResult.failure('Error resending OTP: $e');
    }
  }
}
```

### 3. Create Profile Update Screen

```dart
// profile_update_screen.dart
class ProfileUpdateScreen extends StatefulWidget {
  final User currentUser;

  const ProfileUpdateScreen({Key? key, required this.currentUser}) : super(key: key);

  @override
  State<ProfileUpdateScreen> createState() => _ProfileUpdateScreenState();
}

class _ProfileUpdateScreenState extends State<ProfileUpdateScreen> {
  final _formKey = GlobalKey<FormState>();
  final _nameController = TextEditingController();
  final _surnameController = TextEditingController();
  final _emailController = TextEditingController();
  final _phoneController = TextEditingController();
  
  String? _selectedGender;
  DateTime? _selectedDateOfBirth;
  
  bool _isLoading = false;
  bool _isPhoneChanging = false;
  String? _currentPhoneNumber;

  @override
  void initState() {
    super.initState();
    _nameController.text = widget.currentUser.name ?? '';
    _surnameController.text = widget.currentUser.surname ?? '';
    _emailController.text = widget.currentUser.email ?? '';
    _phoneController.text = widget.currentUser.phone ?? '';
    _currentPhoneNumber = widget.currentUser.phone;
    _selectedGender = widget.currentUser.gender?.toString();
    _selectedDateOfBirth = widget.currentUser.dateOfBirth;
  }

  Future<void> _updateProfile() async {
    if (!_formKey.currentState!.validate()) return;

    setState(() => _isLoading = true);

    final dto = UpdateProfileDto(
      name: _nameController.text.trim(),
      surname: _surnameController.text.trim(),
      email: _emailController.text.trim(),
      phoneNumber: _phoneController.text.trim() != _currentPhoneNumber
          ? _phoneController.text.trim()
          : null,
      gender: _selectedGender != null ? int.tryParse(_selectedGender!) : null,
      dateOfBirth: _selectedDateOfBirth,
    );

    final result = await ProfileService(ApiClient()).updateProfile(dto);

    if (!mounted) return;

    setState(() => _isLoading = false);

    if (result.isSuccess) {
      final response = result.data!;
      
      if (response.requiresOldPhoneVerification) {
        // Navigate to old phone verification
        _navigateToOldPhoneVerification();
      } else if (response.requiresNewPhoneVerification) {
        // Navigate to new phone verification
        _navigateToNewPhoneVerification(
          response.pendingNewPhoneNumber ?? _phoneController.text.trim(),
        );
      } else {
        // Profile updated successfully
        _showSuccessMessage('Profile updated successfully');
        Navigator.pop(context, true); // Return true to refresh profile
      }
    } else {
      _showErrorMessage(result.message ?? 'Failed to update profile');
    }
  }

  void _navigateToOldPhoneVerification() {
    Navigator.push(
      context,
      MaterialPageRoute(
        builder: (context) => OldPhoneVerificationScreen(
          onVerified: () {
            // After old phone verified, automatically proceed to new phone verification
            _navigateToNewPhoneVerification(_phoneController.text.trim());
          },
        ),
      ),
    );
  }

  void _navigateToNewPhoneVerification(String newPhoneNumber) {
    Navigator.push(
      context,
      MaterialPageRoute(
        builder: (context) => NewPhoneVerificationScreen(
          newPhoneNumber: newPhoneNumber,
          onVerified: () {
            _showSuccessMessage('Phone number updated successfully');
            Navigator.popUntil(context, (route) => route.isFirst);
            // Refresh profile data
          },
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Update Profile')),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: Form(
          key: _formKey,
          child: Column(
            children: [
              TextFormField(
                controller: _nameController,
                decoration: const InputDecoration(labelText: 'Name'),
                validator: (value) => value?.isEmpty ?? true ? 'Name is required' : null,
              ),
              const SizedBox(height: 16),
              TextFormField(
                controller: _surnameController,
                decoration: const InputDecoration(labelText: 'Surname'),
                validator: (value) => value?.isEmpty ?? true ? 'Surname is required' : null,
              ),
              const SizedBox(height: 16),
              TextFormField(
                controller: _emailController,
                decoration: const InputDecoration(labelText: 'Email'),
                keyboardType: TextInputType.emailAddress,
                validator: (value) {
                  if (value?.isEmpty ?? true) return 'Email is required';
                  if (!value!.contains('@')) return 'Invalid email';
                  return null;
                },
              ),
              const SizedBox(height: 16),
              TextFormField(
                controller: _phoneController,
                decoration: const InputDecoration(labelText: 'Phone Number'),
                keyboardType: TextInputType.phone,
                onChanged: (value) {
                  setState(() {
                    _isPhoneChanging = value.trim() != _currentPhoneNumber;
                  });
                },
              ),
              if (_isPhoneChanging)
                const Padding(
                  padding: EdgeInsets.only(top: 8),
                  child: Text(
                    'You will need to verify both old and new phone numbers',
                    style: TextStyle(color: Colors.orange, fontSize: 12),
                  ),
                ),
              const SizedBox(height: 16),
              // Gender dropdown
              DropdownButtonFormField<String>(
                value: _selectedGender,
                decoration: const InputDecoration(labelText: 'Gender'),
                items: const [
                  DropdownMenuItem(value: '0', child: Text('Male')),
                  DropdownMenuItem(value: '1', child: Text('Female')),
                ],
                onChanged: (value) => setState(() => _selectedGender = value),
              ),
              const SizedBox(height: 16),
              // Date of birth picker
              InkWell(
                onTap: () async {
                  final date = await showDatePicker(
                    context: context,
                    initialDate: _selectedDateOfBirth ?? DateTime.now(),
                    firstDate: DateTime(1900),
                    lastDate: DateTime.now(),
                  );
                  if (date != null) {
                    setState(() => _selectedDateOfBirth = date);
                  }
                },
                child: InputDecorator(
                  decoration: const InputDecoration(labelText: 'Date of Birth'),
                  child: Text(
                    _selectedDateOfBirth != null
                        ? DateFormat('yyyy-MM-dd').format(_selectedDateOfBirth!)
                        : 'Select date',
                  ),
                ),
              ),
              const SizedBox(height: 32),
              ElevatedButton(
                onPressed: _isLoading ? null : _updateProfile,
                child: _isLoading
                    ? const CircularProgressIndicator()
                    : const Text('Update Profile'),
              ),
            ],
          ),
        ),
      ),
    );
  }

  void _showSuccessMessage(String message) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(message), backgroundColor: Colors.green),
    );
  }

  void _showErrorMessage(String message) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(message), backgroundColor: Colors.red),
    );
  }

  @override
  void dispose() {
    _nameController.dispose();
    _surnameController.dispose();
    _emailController.dispose();
    _phoneController.dispose();
    super.dispose();
  }
}
```

### 4. Create Old Phone Verification Screen

```dart
// old_phone_verification_screen.dart
class OldPhoneVerificationScreen extends StatefulWidget {
  final VoidCallback onVerified;

  const OldPhoneVerificationScreen({
    Key? key,
    required this.onVerified,
  }) : super(key: key);

  @override
  State<OldPhoneVerificationScreen> createState() => _OldPhoneVerificationScreenState();
}

class _OldPhoneVerificationScreenState extends State<OldPhoneVerificationScreen> {
  final _otpController = TextEditingController();
  bool _isLoading = false;
  bool _isResending = false;

  Future<void> _verifyOtp() async {
    if (_otpController.text.length != 6) {
      _showErrorMessage('Please enter 6-digit OTP code');
      return;
    }

    setState(() => _isLoading = true);

    final result = await ProfileService(ApiClient()).verifyOldPhone(
      _otpController.text,
    );

    if (!mounted) return;

    setState(() => _isLoading = false);

    if (result.isSuccess) {
      _showSuccessMessage('Old phone verified successfully');
      widget.onVerified();
    } else {
      _showErrorMessage(result.message ?? 'Invalid OTP code');
    }
  }

  Future<void> _resendOtp() async {
    setState(() => _isResending = true);

    final result = await ProfileService(ApiClient()).resendPhoneUpdateOtp(
      ResendPhoneUpdateOtpDto(otpType: 'OldPhone'),
    );

    if (!mounted) return;

    setState(() => _isResending = false);

    if (result.isSuccess) {
      _showSuccessMessage('OTP has been re-sent to your current phone number');
    } else {
      _showErrorMessage(result.message ?? 'Failed to resend OTP');
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Verify Old Phone')),
      body: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const Text(
              'We sent a verification code to your current phone number. Please enter it below.',
              style: TextStyle(fontSize: 16),
            ),
            const SizedBox(height: 32),
            TextField(
              controller: _otpController,
              decoration: const InputDecoration(
                labelText: 'OTP Code',
                hintText: 'Enter 6-digit code',
              ),
              keyboardType: TextInputType.number,
              maxLength: 6,
            ),
            const SizedBox(height: 16),
            ElevatedButton(
              onPressed: _isLoading ? null : _verifyOtp,
              child: _isLoading
                  ? const CircularProgressIndicator()
                  : const Text('Verify'),
            ),
            const SizedBox(height: 16),
            TextButton(
              onPressed: _isResending ? null : _resendOtp,
              child: _isResending
                  ? const CircularProgressIndicator()
                  : const Text('Resend OTP'),
            ),
          ],
        ),
      ),
    );
  }

  void _showSuccessMessage(String message) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(message), backgroundColor: Colors.green),
    );
  }

  void _showErrorMessage(String message) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(message), backgroundColor: Colors.red),
    );
  }

  @override
  void dispose() {
    _otpController.dispose();
    super.dispose();
  }
}
```

### 5. Create New Phone Verification Screen

```dart
// new_phone_verification_screen.dart
class NewPhoneVerificationScreen extends StatefulWidget {
  final String newPhoneNumber;
  final VoidCallback onVerified;

  const NewPhoneVerificationScreen({
    Key? key,
    required this.newPhoneNumber,
    required this.onVerified,
  }) : super(key: key);

  @override
  State<NewPhoneVerificationScreen> createState() => _NewPhoneVerificationScreenState();
}

class _NewPhoneVerificationScreenState extends State<NewPhoneVerificationScreen> {
  final _otpController = TextEditingController();
  final _phoneController = TextEditingController();
  bool _isLoading = false;
  bool _isResending = false;
  bool _canChangePhone = false;

  @override
  void initState() {
    super.initState();
    _phoneController.text = widget.newPhoneNumber;
  }

  Future<void> _verifyOtp() async {
    if (_otpController.text.length != 6) {
      _showErrorMessage('Please enter 6-digit OTP code');
      return;
    }

    setState(() => _isLoading = true);

    final result = await ProfileService(ApiClient()).verifyNewPhone(
      _phoneController.text.trim(),
      _otpController.text,
    );

    if (!mounted) return;

    setState(() => _isLoading = false);

    if (result.isSuccess) {
      _showSuccessMessage('Phone number updated successfully');
      widget.onVerified();
    } else {
      final message = result.message ?? 'Invalid OTP code';
      _showErrorMessage(message);
      
      // If phone number is wrong, allow user to change it
      if (message.contains('update the phone number')) {
        setState(() => _canChangePhone = true);
      }
    }
  }

  Future<void> _resendOtp() async {
    setState(() => _isResending = true);

    final result = await ProfileService(ApiClient()).resendPhoneUpdateOtp(
      ResendPhoneUpdateOtpDto(
        otpType: 'NewPhone',
        newPhoneNumber: _phoneController.text.trim(),
      ),
    );

    if (!mounted) return;

    setState(() => _isResending = false);

    if (result.isSuccess) {
      _showSuccessMessage('OTP has been re-sent to your new phone number');
    } else {
      _showErrorMessage(result.message ?? 'Failed to resend OTP');
    }
  }

  Future<void> _updatePhoneNumber() async {
    // Navigate back to profile update screen with new phone number
    Navigator.pop(context);
    // Or call update profile API again with new phone number
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Verify New Phone')),
      body: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const Text(
              'We sent a verification code to your new phone number. Please enter it below.',
              style: TextStyle(fontSize: 16),
            ),
            const SizedBox(height: 16),
            if (_canChangePhone) ...[
              TextField(
                controller: _phoneController,
                decoration: const InputDecoration(
                  labelText: 'New Phone Number',
                  hintText: 'Enter correct phone number',
                ),
                keyboardType: TextInputType.phone,
                enabled: _canChangePhone,
              ),
              const SizedBox(height: 16),
              ElevatedButton(
                onPressed: _updatePhoneNumber,
                child: const Text('Update Phone Number'),
              ),
              const SizedBox(height: 16),
            ] else ...[
              Text(
                'Phone: ${widget.newPhoneNumber}',
                style: const TextStyle(fontSize: 14, fontWeight: FontWeight.bold),
              ),
              const SizedBox(height: 16),
            ],
            TextField(
              controller: _otpController,
              decoration: const InputDecoration(
                labelText: 'OTP Code',
                hintText: 'Enter 6-digit code',
              ),
              keyboardType: TextInputType.number,
              maxLength: 6,
            ),
            const SizedBox(height: 16),
            ElevatedButton(
              onPressed: _isLoading ? null : _verifyOtp,
              child: _isLoading
                  ? const CircularProgressIndicator()
                  : const Text('Verify'),
            ),
            const SizedBox(height: 16),
            TextButton(
              onPressed: _isResending ? null : _resendOtp,
              child: _isResending
                  ? const CircularProgressIndicator()
                  : const Text('Resend OTP'),
            ),
          ],
        ),
      ),
    );
  }

  void _showSuccessMessage(String message) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(message), backgroundColor: Colors.green),
    );
  }

  void _showErrorMessage(String message) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(message), backgroundColor: Colors.red),
    );
  }

  @override
  void dispose() {
    _otpController.dispose();
    _phoneController.dispose();
    super.dispose();
  }
}
```

## Key Points

1. **Phone Number Change Detection**: Check if entered phone differs from current phone
2. **Two-Step Verification**: Always verify old phone first, then new phone
3. **OTP Resend**: Provide resend option for both old and new phone OTPs
4. **Wrong Phone Number**: Allow user to update phone number if entered incorrectly
5. **Error Handling**: Show clear error messages and guide user on next steps
6. **State Management**: Track verification state and update UI accordingly
7. **Navigation Flow**: Navigate between screens based on verification requirements

## UI/UX Recommendations

1. Show clear indicators when phone number is being changed
2. Display which phone number OTP was sent to
3. Provide countdown timer for OTP resend (optional)
4. Show loading states during API calls
5. Display success/error messages clearly
6. Allow user to cancel and go back at any point
7. Auto-focus OTP input field
8. Auto-submit when 6 digits are entered (optional)
