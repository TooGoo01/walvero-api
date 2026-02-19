# Frontend - Reset Password (Şifrə Sıfırlama) Tətbiqi - Yeni Endpoint Strukturu

## Tapşırıq
Reset password funksionallığını frontend-də tətbiq edin. 3 addımda şifrə sıfırlama prosesi:
1. Telefon nömrəsi ilə OTP tələb et
2. OTP-ni yoxla və reset token al
3. Reset token və yeni şifrə ilə şifrəni sıfırla

## API Endpoint-lər

### 0. Aktiv Ölkələri Yüklə (Country Codes)
```
GET /api/Public/countries
```

**Response:**
```typescript
{
  success: boolean;
  data: Array<{
    countryCode: number; // Məs: 994 (Azərbaycan)
    name: string; // "Azerbaijan"
    code: string; // "AZ"
    flag: string; // Bayraq emoji və ya URL
  }>;
  message?: string;
}
```

**Nümunə:**
```json
{
  "success": true,
  "data": [
    {
      "countryCode": 994,
      "name": "Azerbaijan",
      "code": "AZ",
      "flag": "🇦🇿"
    },
    {
      "countryCode": 1,
      "name": "United States",
      "code": "US",
      "flag": "🇺🇸"
    }
  ]
}
```

### 1. Şifrə Unutma - OTP Göndər
```
POST /api/Users/ForgotPassword/forgot-password
Content-Type: application/json
```

**Request Body:**
```typescript
{
  phoneNumber: string; // Ölkə kodu ilə (məs: +994501234567)
}
```

**Response:**
```typescript
{
  success: boolean;
  data: {
    otpSent: boolean;
    message: string; // "OTP has been sent to your phone number." və ya "No user found with this phone number."
  };
  message?: string;
}
```

**Nümunə:**
```json
{
  "success": true,
  "data": {
    "otpSent": true,
    "message": "OTP has been sent to your phone number."
  },
  "message": "OTP has been sent to your phone number."
}
```

### 2. OTP Yoxla və Reset Token Al
```
POST /api/Users/VerifyForgotPasswordOtp/verify-forgot-password-otp
Content-Type: application/json
```

**Request Body:**
```typescript
{
  phoneNumber: string; // Eyni telefon nömrəsi
  otpCode: string; // 4-6 rəqəmli OTP kodu
}
```

**Response:**
```typescript
{
  success: boolean;
  data: {
    success: boolean;
    resetToken: string; // Bu token-ı reset-password endpoint-ində istifadə edəcəksiniz
    message: string; // "OTP verified. You can now reset your password."
  };
  message?: string;
}
```

**Nümunə:**
```json
{
  "success": true,
  "data": {
    "success": true,
    "resetToken": "base64EncodedTokenString...",
    "message": "OTP verified. You can now reset your password."
  },
  "message": "OTP verified. You can now reset your password."
}
```

### 3. Şifrəni Sıfırla
```
POST /api/Users/ResetPassword/reset-password
Content-Type: application/json
```

**Request Body:**
```typescript
{
  resetToken: string; // verify-forgot-password-otp-dən alınan token
  newPassword: string; // Minimum 4 simvol
  confirmPassword: string; // newPassword ilə eyni olmalıdır
}
```

**Response:**
```typescript
{
  success: boolean;
  data?: any;
  message: string; // "Password has been reset successfully." və ya xəta mesajı
}
```

**Nümunə:**
```json
{
  "success": true,
  "message": "Password has been reset successfully."
}
```

### 4. OTP-ni Yenidən Göndər
```
POST /api/Users/ResendOtp/resend-otp
Content-Type: application/json
```

**Request Body:**
```typescript
{
  userId: number; // İstifadəçi ID-si (forgot-password-dən sonra telefon nömrəsindən tapılmalıdır)
  purpose: string; // "PasswordReset" (hərf böyük-kiçik həssas deyil)
}
```

**Response:**
```typescript
{
  success: boolean;
  data: {
    userId: number;
    otpRequired: boolean;
    otpSent: boolean;
    purpose: string;
  };
  message?: string;
}
```

**Nümunə:**
```json
{
  "success": true,
  "data": {
    "userId": 123,
    "otpRequired": true,
    "otpSent": true,
    "purpose": "PasswordReset"
  },
  "message": "OTP has been re-sent successfully."
}
```

**Qeyd:** ResendOtp üçün userId lazımdır. Bunu əldə etmək üçün forgot-password-dən sonra telefon nömrəsindən istifadəçini tapmalısınız. Alternativ olaraq, forgot-password response-dan userId qaytara bilərsiniz və ya ayrıca endpoint istifadə edə bilərsiniz.

## React Component Strukturu

### 1. TypeScript Interface-lər

```typescript
interface CountrySequence {
  countryCode: number;
  name: string;
  code: string;
  flag: string;
}

interface ForgotPasswordRequest {
  phoneNumber: string;
}

interface ForgotPasswordResponse {
  otpSent: boolean;
  message: string;
}

interface VerifyOtpRequest {
  phoneNumber: string;
  otpCode: string;
}

interface VerifyOtpResponse {
  success: boolean;
  resetToken: string;
  message: string;
}

interface ResetPasswordRequest {
  resetToken: string;
  newPassword: string;
  confirmPassword: string;
}

interface ResendOtpRequest {
  userId: number;
  purpose: string; // "PasswordReset"
}

interface ResendOtpResponse {
  userId: number;
  otpRequired: boolean;
  otpSent: boolean;
  purpose: string;
}

interface ServiceResult<T = any> {
  success: boolean;
  data?: T;
  message?: string;
}
```

### 2. Reset Password Flow Component

```typescript
import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import axios from 'axios';

type ResetPasswordStep = 'forgot' | 'verify' | 'reset';

const ResetPasswordPage: React.FC = () => {
  const navigate = useNavigate();
  
  const [step, setStep] = useState<ResetPasswordStep>('forgot');
  const [selectedCountryCode, setSelectedCountryCode] = useState<string>('+994'); // Default: Azerbaijan
  const [phoneNumber, setPhoneNumber] = useState('');
  const [fullPhoneNumber, setFullPhoneNumber] = useState(''); // Country code + phone number
  const [userId, setUserId] = useState<number | null>(null); // ResendOtp üçün lazımdır
  const [countries, setCountries] = useState<CountrySequence[]>([]);
  const [otpCode, setOtpCode] = useState('');
  const [resetToken, setResetToken] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  
  const [loading, setLoading] = useState(false);
  const [loadingCountries, setLoadingCountries] = useState(true);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [message, setMessage] = useState('');

  const API_BASE_URL = process.env.REACT_APP_API_BASE_URL || 'https://your-api-url.com';

  // Ölkələri yüklə
  useEffect(() => {
    const fetchCountries = async () => {
      try {
        setLoadingCountries(true);
        const response = await axios.get<ServiceResult<CountrySequence[]>>(
          `${API_BASE_URL}/api/Public/countries`
        );

        if (response.data.success && response.data.data) {
          setCountries(response.data.data);
          // Default olaraq Azərbaycanı seç (countryCode: 994)
          const azerbaijan = response.data.data.find(c => c.countryCode === 994);
          if (azerbaijan) {
            setSelectedCountryCode(`+${azerbaijan.countryCode}`);
          } else if (response.data.data.length > 0) {
            // Azərbaycan yoxdursa, ilk ölkəni seç
            setSelectedCountryCode(`+${response.data.data[0].countryCode}`);
          }
        }
      } catch (error) {
        console.error('Countries yüklənərkən xəta:', error);
        // Xəta olsa belə, default olaraq +994 istifadə et
        setSelectedCountryCode('+994');
      } finally {
        setLoadingCountries(false);
      }
    };

    fetchCountries();
  }, []);

  // Telefon nömrəsi dəyişdikdə full phone number-i yenilə
  useEffect(() => {
    if (phoneNumber.trim()) {
      setFullPhoneNumber(`${selectedCountryCode}${phoneNumber.trim()}`);
    } else {
      setFullPhoneNumber('');
    }
  }, [selectedCountryCode, phoneNumber]);

  // Step 1: Şifrə unutma - OTP göndər
  const handleForgotPassword = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setErrors({});
    setMessage('');

    // Validation
    if (!phoneNumber.trim()) {
      setErrors({ phoneNumber: 'Telefon nömrəsi məcburidir' });
      setLoading(false);
      return;
    }

    // Telefon nömrəsi formatını yoxla (yalnız rəqəmlər)
    const phoneRegex = /^\d+$/;
    if (!phoneRegex.test(phoneNumber.trim())) {
      setErrors({ phoneNumber: 'Telefon nömrəsi yalnız rəqəmlərdən ibarət olmalıdır' });
      setLoading(false);
      return;
    }

    // Full phone number-i hazırla (country code + phone number)
    const fullPhone = `${selectedCountryCode}${phoneNumber.trim()}`;

    try {
      const response = await axios.post<ServiceResult<ForgotPasswordResponse>>(
        `${API_BASE_URL}/api/Users/ForgotPassword/forgot-password`,
        { phoneNumber: fullPhone }
      );

      if (response.data.success && response.data.data?.otpSent) {
        setMessage(response.data.data.message || 'OTP telefon nömrənizə göndərildi');
        setStep('verify');
        
        // Qeyd: Backend-dən userId qayıdırsa, onu saxla. Əks halda telefon nömrəsindən tapmalı olacaqsınız
        // Bu nümunədə userId-ni telefon nömrəsindən tapmaq üçün ayrı endpoint lazım ola bilər
        // və ya backend-də forgot-password response-a userId əlavə edilə bilər
      } else {
        setMessage(response.data.data?.message || response.data.message || 'Xəta baş verdi');
        setErrors({ phoneNumber: response.data.data?.message || 'Bu telefon nömrəsi ilə istifadəçi tapılmadı' });
      }
    } catch (error: any) {
      console.error('Forgot password error:', error);
      const errorMessage = error.response?.data?.message || 'OTP göndərilərkən xəta baş verdi';
      setMessage(errorMessage);
      setErrors({ phoneNumber: errorMessage });
    } finally {
      setLoading(false);
    }
  };

  // Step 2: OTP yoxla
  const handleVerifyOtp = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setErrors({});
    setMessage('');

    // Validation
    if (!otpCode.trim()) {
      setErrors({ otpCode: 'OTP kodu məcburidir' });
      setLoading(false);
      return;
    }

    if (otpCode.length < 4 || otpCode.length > 6) {
      setErrors({ otpCode: 'OTP kodu 4-6 rəqəmli olmalıdır' });
      setLoading(false);
      return;
    }

    // Full phone number-i hazırla
    const fullPhone = `${selectedCountryCode}${phoneNumber.trim()}`;

    try {
      const response = await axios.post<ServiceResult<VerifyOtpResponse>>(
        `${API_BASE_URL}/api/Users/VerifyForgotPasswordOtp/verify-forgot-password-otp`,
        {
          phoneNumber: fullPhone,
          otpCode: otpCode.trim()
        }
      );

      if (response.data.success && response.data.data?.success && response.data.data?.resetToken) {
        setResetToken(response.data.data.resetToken);
        setMessage(response.data.data.message || 'OTP doğrulandı. İndi şifrənizi sıfırlaya bilərsiniz');
        setStep('reset');
      } else {
        setMessage(response.data.data?.message || response.data.message || 'OTP kodu yanlışdır');
        setErrors({ otpCode: 'OTP kodu yanlışdır və ya müddəti bitib' });
      }
    } catch (error: any) {
      console.error('Verify OTP error:', error);
      const errorMessage = error.response?.data?.message || 'OTP yoxlanılarkən xəta baş verdi';
      setMessage(errorMessage);
      setErrors({ otpCode: errorMessage });
    } finally {
      setLoading(false);
    }
  };

  // Step 3: Şifrəni sıfırla
  const handleResetPassword = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setErrors({});
    setMessage('');

    // Validation
    const validationErrors: Record<string, string> = {};

    if (!newPassword.trim()) {
      validationErrors.newPassword = 'Yeni şifrə məcburidir';
    } else if (newPassword.length < 4) {
      validationErrors.newPassword = 'Şifrə minimum 4 simvol olmalıdır';
    }

    if (!confirmPassword.trim()) {
      validationErrors.confirmPassword = 'Şifrə təsdiqi məcburidir';
    } else if (newPassword !== confirmPassword) {
      validationErrors.confirmPassword = 'Şifrələr uyğun gəlmir';
    }

    if (Object.keys(validationErrors).length > 0) {
      setErrors(validationErrors);
      setLoading(false);
      return;
    }

    try {
      const response = await axios.post<ServiceResult>(
        `${API_BASE_URL}/api/Users/ResetPassword/reset-password`,
        {
          resetToken: resetToken,
          newPassword: newPassword.trim(),
          confirmPassword: confirmPassword.trim()
        }
      );

      if (response.data.success) {
        setMessage(response.data.message || 'Şifrə uğurla sıfırlandı');
        
        // 3 saniyə sonra login səhifəsinə yönləndir
        setTimeout(() => {
          navigate('/login');
        }, 3000);
      } else {
        setMessage(response.data.message || 'Şifrə sıfırlanarkən xəta baş verdi');
        setErrors({ general: response.data.message || 'Xəta baş verdi' });
      }
    } catch (error: any) {
      console.error('Reset password error:', error);
      
      if (error.response?.data?.errors) {
        // Backend validation xətaları
        const backendErrors: Record<string, string> = {};
        Object.keys(error.response.data.errors).forEach(key => {
          backendErrors[key] = error.response.data.errors[key][0];
        });
        setErrors(backendErrors);
      } else {
        const errorMessage = error.response?.data?.message || 'Şifrə sıfırlanarkən xəta baş verdi';
        setMessage(errorMessage);
        setErrors({ general: errorMessage });
      }
    } finally {
      setLoading(false);
    }
  };

  // OTP input formatı (yalnız rəqəm)
  const handleOtpChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const value = e.target.value.replace(/\D/g, ''); // Yalnız rəqəmlər
    if (value.length <= 6) {
      setOtpCode(value);
    }
  };

  // OTP-ni yenidən göndər
  const handleResendOtp = async () => {
    if (!userId) {
      // Əgər userId yoxdursa, telefon nömrəsindən tapmalıyıq
      // Bu üçün ayrıca endpoint lazım ola bilər və ya forgot-password-dən userId qayıdır
      setMessage('Xəta: İstifadəçi ID-si tapılmadı');
      return;
    }

    setLoading(true);
    setErrors({});
    setMessage('');

    try {
      const response = await axios.post<ServiceResult<ResendOtpResponse>>(
        `${API_BASE_URL}/api/Users/ResendOtp/resend-otp`,
        {
          userId: userId,
          purpose: 'PasswordReset'
        }
      );

      if (response.data.success && response.data.data?.otpSent) {
        setMessage(response.data.message || 'OTP yenidən göndərildi');
        setOtpCode(''); // OTP input-unu təmizlə
      } else {
        setMessage(response.data.message || 'OTP yenidən göndərilərkən xəta baş verdi');
      }
    } catch (error: any) {
      console.error('Resend OTP error:', error);
      const errorMessage = error.response?.data?.message || 'OTP yenidən göndərilərkən xəta baş verdi';
      setMessage(errorMessage);
    } finally {
      setLoading(false);
    }
  };

  // Geri qayıt
  const handleBack = () => {
    if (step === 'verify') {
      setStep('forgot');
      setOtpCode('');
      setMessage('');
    } else if (step === 'reset') {
      setStep('verify');
      setNewPassword('');
      setConfirmPassword('');
      setMessage('');
    }
  };

  return (
    <div className="reset-password-page">
      <div className="reset-password-container">
        <h1>Şifrəni Sıfırla</h1>

        {/* Step 1: Telefon nömrəsi */}
        {step === 'forgot' && (
          <form onSubmit={handleForgotPassword} className="reset-form">
            <div className="form-group">
              <label htmlFor="phoneNumber">Telefon Nömrəsi *</label>
              <div className="phone-input-container">
                <select
                  value={selectedCountryCode}
                  onChange={(e) => setSelectedCountryCode(e.target.value)}
                  disabled={loading || loadingCountries}
                  className="country-code-select"
                >
                  {loadingCountries ? (
                    <option value="+994">Yüklənir...</option>
                  ) : (
                    countries.map((country) => (
                      <option key={country.countryCode} value={`+${country.countryCode}`}>
                        {country.flag} {country.name} (+{country.countryCode})
                      </option>
                    ))
                  )}
                </select>
                <input
                  type="tel"
                  id="phoneNumber"
                  value={phoneNumber}
                  onChange={(e) => {
                    const value = e.target.value.replace(/\D/g, ''); // Yalnız rəqəmlər
                    setPhoneNumber(value);
                  }}
                  placeholder="501234567"
                  disabled={loading || loadingCountries}
                  className={`phone-number-input ${errors.phoneNumber ? 'error' : ''}`}
                />
              </div>
              {fullPhoneNumber && (
                <div className="full-phone-display">
                  Tam nömrə: <strong>{fullPhoneNumber}</strong>
                </div>
              )}
              {errors.phoneNumber && (
                <span className="error-message">{errors.phoneNumber}</span>
              )}
            </div>

            {message && (
              <div className={`message ${errors.phoneNumber ? 'error' : 'success'}`}>
                {message}
              </div>
            )}

            <div className="form-actions">
              <button type="submit" disabled={loading || loadingCountries}>
                {loading ? 'Göndərilir...' : 'OTP Göndər'}
              </button>
              <button type="button" onClick={() => navigate('/login')}>
                Geri
              </button>
            </div>
          </form>
        )}

        {/* Step 2: OTP yoxlama */}
        {step === 'verify' && (
          <form onSubmit={handleVerifyOtp} className="reset-form">
            <div className="info-message">
              <p>Telefon nömrənizə göndərilən OTP kodunu daxil edin:</p>
              <p className="phone-display">{fullPhoneNumber || `${selectedCountryCode}${phoneNumber}`}</p>
            </div>

            <div className="form-group">
              <label htmlFor="otpCode">OTP Kodu *</label>
              <input
                type="text"
                id="otpCode"
                value={otpCode}
                onChange={handleOtpChange}
                placeholder="123456"
                maxLength={6}
                disabled={loading}
                className={errors.otpCode ? 'error' : ''}
                autoFocus
              />
              {errors.otpCode && (
                <span className="error-message">{errors.otpCode}</span>
              )}
            </div>

            {message && (
              <div className={`message ${errors.otpCode ? 'error' : 'success'}`}>
                {message}
              </div>
            )}

            <div className="form-actions">
              <button type="submit" disabled={loading || otpCode.length < 4}>
                {loading ? 'Yoxlanılır...' : 'Yoxla'}
              </button>
              <button type="button" onClick={handleBack} disabled={loading}>
                Geri
              </button>
            </div>

            <div className="resend-otp">
              <button
                type="button"
                onClick={handleResendOtp}
                disabled={loading || !userId}
                className="link-button"
              >
                OTP-ni yenidən göndər
              </button>
              {!userId && (
                <p className="resend-note">
                  Qeyd: OTP-ni yenidən göndərmək üçün istifadəçi ID-si lazımdır. 
                  Əgər backend-dən userId qayıtmırsa, bu funksionallıq işləməyə bilər.
                </p>
              )}
            </div>
          </form>
        )}

        {/* Step 3: Yeni şifrə */}
        {step === 'reset' && (
          <form onSubmit={handleResetPassword} className="reset-form">
            <div className="info-message">
              <p>Yeni şifrənizi daxil edin:</p>
            </div>

            <div className="form-group">
              <label htmlFor="newPassword">Yeni Şifrə *</label>
              <input
                type="password"
                id="newPassword"
                value={newPassword}
                onChange={(e) => setNewPassword(e.target.value)}
                placeholder="Minimum 4 simvol"
                disabled={loading}
                className={errors.newPassword ? 'error' : ''}
                autoFocus
              />
              {errors.newPassword && (
                <span className="error-message">{errors.newPassword}</span>
              )}
            </div>

            <div className="form-group">
              <label htmlFor="confirmPassword">Şifrəni Təsdiqlə *</label>
              <input
                type="password"
                id="confirmPassword"
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                placeholder="Şifrəni təkrar daxil edin"
                disabled={loading}
                className={errors.confirmPassword ? 'error' : ''}
              />
              {errors.confirmPassword && (
                <span className="error-message">{errors.confirmPassword}</span>
              )}
            </div>

            {message && (
              <div className={`message ${errors.general ? 'error' : 'success'}`}>
                {message}
              </div>
            )}

            {errors.general && (
              <div className="error-message">{errors.general}</div>
            )}

            <div className="form-actions">
              <button type="submit" disabled={loading}>
                {loading ? 'Sıfırlanır...' : 'Şifrəni Sıfırla'}
              </button>
              <button type="button" onClick={handleBack} disabled={loading}>
                Geri
              </button>
            </div>
          </form>
        )}
      </div>
    </div>
  );
};

export default ResetPasswordPage;
```

### 3. CSS Stil

```css
.reset-password-page {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  padding: 20px;
}

.reset-password-container {
  background: white;
  border-radius: 12px;
  box-shadow: 0 10px 40px rgba(0, 0, 0, 0.1);
  padding: 40px;
  max-width: 450px;
  width: 100%;
}

.reset-password-container h1 {
  text-align: center;
  margin-bottom: 30px;
  color: #333;
  font-size: 28px;
}

.reset-form {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.form-group label {
  font-weight: 600;
  color: #555;
  font-size: 14px;
}

.form-group input {
  padding: 12px;
  border: 2px solid #e0e0e0;
  border-radius: 8px;
  font-size: 16px;
  transition: border-color 0.3s;
}

.form-group input:focus {
  outline: none;
  border-color: #667eea;
}

.form-group input.error {
  border-color: #e74c3c;
}

.form-group input:disabled {
  background-color: #f5f5f5;
  cursor: not-allowed;
}

.phone-input-container {
  display: flex;
  gap: 8px;
  align-items: stretch;
}

.country-code-select {
  flex: 0 0 auto;
  min-width: 200px;
  padding: 12px;
  border: 2px solid #e0e0e0;
  border-radius: 8px;
  font-size: 16px;
  background: white;
  cursor: pointer;
  transition: border-color 0.3s;
}

.country-code-select:focus {
  outline: none;
  border-color: #667eea;
}

.country-code-select:disabled {
  background-color: #f5f5f5;
  cursor: not-allowed;
}

.phone-number-input {
  flex: 1;
}

.full-phone-display {
  margin-top: 8px;
  font-size: 12px;
  color: #666;
  padding: 8px;
  background-color: #f9f9f9;
  border-radius: 4px;
}

.full-phone-display strong {
  color: #333;
  font-weight: 600;
}

.error-message {
  color: #e74c3c;
  font-size: 12px;
  margin-top: 4px;
}

.message {
  padding: 12px;
  border-radius: 8px;
  font-size: 14px;
  text-align: center;
}

.message.success {
  background-color: #d4edda;
  color: #155724;
  border: 1px solid #c3e6cb;
}

.message.error {
  background-color: #f8d7da;
  color: #721c24;
  border: 1px solid #f5c6cb;
}

.info-message {
  background-color: #e7f3ff;
  padding: 15px;
  border-radius: 8px;
  margin-bottom: 10px;
}

.info-message p {
  margin: 5px 0;
  color: #004085;
  font-size: 14px;
}

.phone-display {
  font-weight: 600;
  font-size: 16px;
}

.form-actions {
  display: flex;
  gap: 10px;
  margin-top: 10px;
}

.form-actions button {
  flex: 1;
  padding: 12px;
  border: none;
  border-radius: 8px;
  font-size: 16px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.3s;
}

.form-actions button[type="submit"] {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
}

.form-actions button[type="submit"]:hover:not(:disabled) {
  transform: translateY(-2px);
  box-shadow: 0 5px 15px rgba(102, 126, 234, 0.4);
}

.form-actions button[type="submit"]:disabled {
  background: #ccc;
  cursor: not-allowed;
  transform: none;
}

.form-actions button[type="button"] {
  background: #f5f5f5;
  color: #333;
}

.form-actions button[type="button"]:hover:not(:disabled) {
  background: #e0e0e0;
}

.resend-otp {
  text-align: center;
  margin-top: 15px;
}

.resend-note {
  font-size: 11px;
  color: #666;
  margin-top: 5px;
  font-style: italic;
}

.link-button {
  background: none;
  border: none;
  color: #667eea;
  cursor: pointer;
  text-decoration: underline;
  font-size: 14px;
  padding: 0;
}

.link-button:hover:not(:disabled) {
  color: #764ba2;
}

.link-button:disabled {
  color: #ccc;
  cursor: not-allowed;
  text-decoration: none;
}

/* Responsive */
@media (max-width: 480px) {
  .reset-password-container {
    padding: 30px 20px;
  }

  .reset-password-container h1 {
    font-size: 24px;
  }

  .phone-input-container {
    flex-direction: column;
  }

  .country-code-select {
    min-width: 100%;
  }
}
```

### 4. Routing (React Router)

```typescript
// App.tsx və ya routing faylında
import { Route, Routes } from 'react-router-dom';
import ResetPasswordPage from './pages/ResetPasswordPage';

<Routes>
  <Route path="/reset-password" element={<ResetPasswordPage />} />
  <Route path="/forgot-password" element={<ResetPasswordPage />} />
</Routes>
```

### 5. Login səhifəsində link

```typescript
// Login səhifəsində
import { Link } from 'react-router-dom';

<Link to="/reset-password" className="forgot-password-link">
  Şifrəni unutdum?
</Link>
```

## İstifadə Flow-u

1. **İstifadəçi "Şifrəni unutdum?" linkinə klik edir**
   - `/reset-password` route-na yönləndirilir

2. **Step 1: Telefon nömrəsi daxil edir**
   - Ölkə kodunu dropdown-dan seçir (default: +994)
   - Telefon nömrəsini daxil edir (yalnız rəqəmlər)
   - "OTP Göndər" düyməsinə basır
   - Backend `/api/Users/ForgotPassword/forgot-password` endpoint-inə request göndərir
   - Backend telefon nömrəsinə OTP göndərir
   - Uğurlu olarsa, Step 2-yə keçir

3. **Step 2: OTP kodu daxil edir**
   - Telefon nömrəsinə gələn OTP kodunu daxil edir
   - "Yoxla" düyməsinə basır
   - Backend `/api/Users/VerifyForgotPasswordOtp/verify-forgot-password-otp` endpoint-inə request göndərir
   - Backend OTP-ni yoxlayır və reset token qaytarır
   - Uğurlu olarsa, Step 3-yə keçir
   - "OTP-ni yenidən göndər" düyməsi ilə `/api/Users/ResendOtp/resend-otp` endpoint-inə request göndərə bilər

4. **Step 3: Yeni şifrə daxil edir**
   - Yeni şifrəni daxil edir (minimum 4 simvol)
   - Şifrəni təsdiqləyir
   - "Şifrəni Sıfırla" düyməsinə basır
   - Backend `/api/Users/ResetPassword/reset-password` endpoint-inə request göndərir
   - Backend şifrəni sıfırlayır
   - Uğurlu olarsa, 3 saniyə sonra login səhifəsinə yönləndirilir

## Xüsusiyyətlər

✅ **3 addımlı proses** - forgot → verify → reset  
✅ **Country code seçimi** - Dropdown ilə ölkə kodu seçimi  
✅ **Real-time validation** - Form validation  
✅ **Error handling** - Backend xətalarını göstərir  
✅ **Loading states** - Button disabled və loading text  
✅ **OTP resend** - `/api/Users/ResendOtp/resend-otp` endpoint-i ilə OTP-ni yenidən göndərmə  
✅ **Responsive design** - Mobil uyğun  
✅ **User-friendly messages** - Aydın mesajlar  
✅ **Auto-navigation** - Uğurlu reset-dən sonra login-ə yönləndirir  

## Qeydlər

- **Endpoint strukturu**: Yeni endpoint strukturu istifadə olunur:
  - `/api/Users/ForgotPassword/forgot-password`
  - `/api/Users/VerifyForgotPasswordOtp/verify-forgot-password-otp`
  - `/api/Users/ResetPassword/reset-password`
  - `/api/Users/ResendOtp/resend-otp`

- **Telefon nömrəsi formatı**: Ölkə kodu dropdown-dan seçilir və avtomatik olaraq telefon nömrəsinin əvvəlinə əlavə olunur (məs: +994 + 501234567 = +994501234567)

- **Country Codes**: Ölkə kodları `/api/Public/countries` endpoint-indən yüklənir və dropdown-da göstərilir

- **Default Country**: Default olaraq Azərbaycan (+994) seçilir

- **ResendOtp üçün userId**: ResendOtp endpoint-i üçün `userId` lazımdır. Əgər backend-də `forgot-password` response-dan `userId` qayıtmırsa, bu funksionallıq işləməyə bilər. Bu halda:
  - Backend-də `forgot-password` response-a `userId` əlavə edilməlidir, VƏ YA
  - Telefon nömrəsindən userId tapmaq üçün ayrıca endpoint istifadə edilməlidir

- **OTP müddəti**: OTP kodunun müddəti 10 dəqiqədir

- **Reset token müddəti**: Reset token-ın müddəti 30 dəqiqədir

- **Şifrə tələbləri**: Minimum 4 simvol

- **API_BASE_URL**: Environment variable-dan götürülməlidir

## Test Senaryoları

1. ✅ Düzgün telefon nömrəsi ilə OTP göndər
2. ✅ Yanlış telefon nömrəsi ilə OTP göndər (xəta mesajı)
3. ✅ Düzgün OTP ilə yoxla
4. ✅ Yanlış OTP ilə yoxla (xəta mesajı)
5. ✅ Müddəti bitmiş OTP ilə yoxla (xəta mesajı)
6. ✅ OTP-ni yenidən göndər (ResendOtp)
7. ✅ Düzgün şifrə ilə reset et
8. ✅ Uyğun gəlməyən şifrələrlə reset et (validation xətası)
9. ✅ Müddəti bitmiş token ilə reset et (xəta mesajı)
10. ✅ Country code seçimi və telefon nömrəsi formatı
