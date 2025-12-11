# OTP İmplementasiyası - Qeydlər

## 1. Verilənlər Bazası Dəyişiklikləri və Mövcud İstifadəçilər

### ✅ Təhlükəsiz Dəyişikliklər

Yeni əlavə edilən column-lar:
- `IsPhoneVerified` (bit, default: 0/false)
- `Status` (int, default: 1/Active)

**Mövcud istifadəçilər üçün:**
- `Status` default olaraq `Active` (1) olacaq - mövcud istifadəçilər aktiv qalacaq
- `IsPhoneVerified` default olaraq `false` olacaq - bu normaldır, çünki OTP ilə yoxlanmayıblar

### Migration Addım-addım

1. **Migration yaradın:**
```powershell
dotnet ef migrations add AddPhoneOtpSupport --project Loyalty.DataAccess --startup-project Loyalty.WebAPI
```

2. **Migration-ı tətbiq edin:**
```powershell
dotnet ef database update --project Loyalty.DataAccess --startup-project Loyalty.WebAPI
```

3. **Mövcud istifadəçiləri yeniləyin (opsional, amma tövsiyə olunur):**
```sql
-- PhoneNumberConfirmed = true olan istifadəçiləri IsPhoneVerified = true et
UPDATE AspNetUsers 
SET IsPhoneVerified = 1
WHERE PhoneNumberConfirmed = 1;

-- Guest istifadəçiləri Active et (əgər Pending-dirsə)
UPDATE AspNetUsers 
SET Status = 1 
WHERE IsGuest = 1 AND Status = 0;
```

### Web Layihəsi üçün

Web layihəsində yalnız `ApplicationUser.cs`-ə yeni property-ləri əlavə edin:

```csharp
public bool IsPhoneVerified { get; set; } = false;
public UserStatus Status { get; set; } = UserStatus.Active;
```

**Qeyd:** Web layihəsində migration yaratmağa ehtiyac yoxdur - API layihəsində artıq tətbiq olunub.

## 2. Register Endpoint - İki Rejim

### Mövcud Davranış (Guest Login)
```
POST /api/users/Register
{
  "deviceId": "unique-device-id"
}
```
→ Guest user yaradır və token qaytarır

### Yeni Davranış (Phone + Password)
```
POST /api/users/Register
{
  "phone": "+994501234567",
  "password": "password123",
  "confirmPassword": "password123",
  "name": "John",
  "surname": "Doe",
  "email": "john@example.com"
}
```
→ Pending user yaradır, OTP göndərir

### Avtomatik Aşkarlama

`Register` endpoint-i avtomatik olaraq aşkar edir:
- **Əgər `Phone` və `Password` verilibsə** → Phone-based OTP registration
- **Əks halda** → Guest login (DeviceId tələb olunur)

## 3. OTP Flow

### Registration Flow
1. `POST /api/users/Register` (Phone + Password ilə)
   - User Pending status-da yaradılır
   - OTP göndərilir
   - Response: `{ userId, otpSent: true }`

2. `POST /api/users/verify-otp`
   - OTP yoxlanır
   - User Active olur, IsPhoneVerified = true
   - Token qaytarılır

### Password Reset Flow
1. `POST /api/users/forgot-password` (Phone)
   - OTP göndərilir

2. `POST /api/users/verify-forgot-password-otp` (Phone + OTP)
   - OTP yoxlanır
   - Reset token qaytarılır

3. `POST /api/users/reset-password` (ResetToken + NewPassword)
   - Şifrə yenilənir

## 4. Endpoint-lər

### Yeni Endpoint-lər
- `POST /api/users/register-with-phone` - Ayrıca phone registration (əlavə)
- `POST /api/users/verify-otp` - OTP yoxlama
- `POST /api/users/forgot-password` - Şifrə unutma
- `POST /api/users/verify-forgot-password-otp` - Şifrə reset OTP yoxlama
- `POST /api/users/reset-password` - Şifrə reset

### Mövcud Endpoint (Yeniləndi)
- `POST /api/users/Register` - İndi həm guest, həm də phone registration dəstəkləyir

## 5. Təhlükəsizlik

- ✅ OTP kodları hash olunur (SHA256 + Salt)
- ✅ Brute force qorunması (max 5 cəhd)
- ✅ OTP expiration (10 dəqiqə)
- ✅ One-time use OTP
- ✅ Reset token expiration (30 dəqiqə)
- ✅ Password reset zamanı bütün refresh token-lar ləğv olunur

## 6. Logging

Bütün əməliyyatlar loglanır:
- Registration başlanğıcı
- OTP yaradılması və göndərilməsi
- OTP yoxlanması (uğurlu/uğursuz)
- Şifrə reset istəkləri
- Şifrə uğurla dəyişdirilməsi

