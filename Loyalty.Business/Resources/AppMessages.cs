using System.Threading;

namespace Loyalty.Business.Resources
{
    /// <summary>
    /// Mobil-üzlü error/status mesajlarının mərkəzi kataloqu (az/en/ru).
    /// Dil hər request-in əvvəlində middleware tərəfindən AsyncLocal-a yazılır —
    /// servislərdə DI dəyişikliyi tələb olunmur, async axını boyu daşınır.
    /// </summary>
    public static class AppMessages
    {
        private static readonly AsyncLocal<string?> _lang = new();

        /// <summary>"az" | "en" | "ru" — default "az".</summary>
        public static string CurrentLanguage
        {
            get => _lang.Value ?? "az";
            set => _lang.Value = value is "az" or "en" or "ru" ? value : "az";
        }

        private static readonly Dictionary<string, (string az, string en, string ru)> _m = new()
        {
            ["GenericError"] = ("Xəta baş verdi. Yenidən cəhd edin.", "Something went wrong. Please try again.", "Произошла ошибка. Попробуйте ещё раз."),
            ["UserNotFound"] = ("İstifadəçi tapılmadı.", "User not found.", "Пользователь не найден."),
            ["NotAuthenticated"] = ("Sessiya bitib. Yenidən daxil olun.", "Session expired. Please sign in again.", "Сессия истекла. Войдите снова."),
            ["LoginFailed"] = ("Telefon nömrəsi və ya şifrə yanlışdır.", "Invalid phone number or password.", "Неверный номер телефона или пароль."),
            ["AccountBlocked"] = ("Hesabınız bloklanıb.", "Your account is blocked.", "Ваш аккаунт заблокирован."),
            ["PhoneAlreadyExists"] = ("Bu nömrə ilə hesab artıq mövcuddur.", "An account with this phone number already exists.", "Аккаунт с этим номером уже существует."),
            ["RegistrationFailed"] = ("Qeydiyyat alınmadı. Yenidən cəhd edin.", "Registration failed. Please try again.", "Регистрация не удалась. Попробуйте ещё раз."),
            ["PasswordTooWeak"] = ("Şifrə çox sadədir. Ən azı 6 simvol istifadə edin.", "Password is too weak. Use at least 6 characters.", "Пароль слишком простой. Используйте минимум 6 символов."),
            ["PhoneNotFound"] = ("Bu nömrə ilə hesab tapılmadı.", "No account found with this number.", "Аккаунт с этим номером не найден."),
            ["PasswordTooShort"] = ("Şifrə ən azı {0} simvol olmalıdır.", "Password must be at least {0} characters.", "Пароль должен содержать не менее {0} символов."),
            ["PasswordInvalid"] = ("Şifrə tələblərə uyğun deyil.", "Password does not meet the requirements.", "Пароль не соответствует требованиям."),
            ["PhoneInvalidFormat"] = ("Telefon nömrəsi düzgün formatda deyil.", "Invalid phone number format.", "Неверный формат номера телефона."),

            ["OtpNotFound"] = ("Aktiv təsdiq kodu tapılmadı. Yenisini istəyin.", "No active verification code. Please request a new one.", "Активный код не найден. Запросите новый."),
            ["OtpExpired"] = ("Kodun vaxtı bitib. Yenisini istəyin.", "The code has expired. Please request a new one.", "Срок действия кода истёк. Запросите новый."),
            ["OtpMaxAttempts"] = ("Cəhd limiti bitdi. Yeni kod istəyin.", "Too many attempts. Please request a new code.", "Превышен лимит попыток. Запросите новый код."),
            ["OtpInvalid"] = ("Kod yanlışdır. Qalan cəhd: {0}", "Invalid code. Attempts remaining: {0}", "Неверный код. Осталось попыток: {0}"),
            ["OtpInvalidOrExpired"] = ("Kod yanlış və ya vaxtı bitib.", "The code is invalid or has expired.", "Код неверен или истёк."),
            ["OtpSendFailed"] = ("Kod göndərilmədi. Yenidən cəhd edin.", "Failed to send the code. Please try again.", "Не удалось отправить код. Попробуйте ещё раз."),
            ["OtpSent"] = ("Təsdiq kodu göndərildi.", "Verification code sent.", "Код подтверждения отправлен."),
            ["OtpSmsText"] = ("Sizin Walvero təsdiq kodunuz: {0}. 10 dəqiqə etibarlıdır.", "Your Walvero verification code is: {0}. Valid for 10 minutes.", "Ваш код подтверждения Walvero: {0}. Действителен 10 минут."),

            ["ResetTokenInvalid"] = ("Bərpa kodu yanlış və ya vaxtı bitib. Yenidən cəhd edin.", "Reset code is invalid or expired. Please try again.", "Код сброса неверен или истёк. Попробуйте ещё раз."),

            ["VerifyOldPhoneFirst"] = ("Əvvəlcə köhnə nömrənizi təsdiqləyin.", "Please verify your current phone number first.", "Сначала подтвердите текущий номер."),
            ["NewPhoneRequired"] = ("Yeni telefon nömrəsi tələb olunur.", "New phone number is required.", "Требуется новый номер телефона."),

            ["TenantNotResolved"] = ("Məkan tapılmadı.", "Place not found.", "Заведение не найдено."),
            ["NameRequired"] = ("Ad tələb olunur.", "Name is required.", "Требуется имя."),
            ["PhoneRequired"] = ("Telefon nömrəsi tələb olunur.", "Phone number is required.", "Требуется номер телефона."),
            ["Wait30Sec"] = ("Zəhmət olmasa 30 saniyə gözləyin.", "Please wait 30 seconds.", "Пожалуйста, подождите 30 секунд."),
            ["CustomerNotFound"] = ("Müştəri tapılmadı.", "Customer not found.", "Клиент не найден."),
            ["CodeNotFound"] = ("Aktiv kod tapılmadı.", "No active code.", "Активный код не найден."),
            ["CodeExpired"] = ("Kodun vaxtı bitib.", "The code has expired.", "Срок действия кода истёк."),
            ["CodeInvalid"] = ("Kod yanlışdır.", "Invalid code.", "Неверный код."),
            ["CardNotFound"] = ("Kart tapılmadı.", "Card not found.", "Карта не найдена."),
        };

        public static string Get(string code, params object[] args)
        {
            if (!_m.TryGetValue(code, out var t))
                t = _m["GenericError"];

            var text = CurrentLanguage switch
            {
                "en" => t.en,
                "ru" => t.ru,
                _ => t.az
            };
            return args is { Length: > 0 } ? string.Format(text, args) : text;
        }
    }
}
