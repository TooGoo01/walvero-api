# Web Layihəsi üçün Sadə Dəyişikliklər

## Minimum dəyişiklik (tövsiyə olunur)

Web layihəsində **yalnız ApplicationUser.cs** faylını yeniləyin. Navigation property-ləri əlavə etməyə ehtiyac yoxdur.

### ApplicationUser.cs

```csharp
public class ApplicationUser : IdentityUser<int>
{
    // ... mövcud property-lər ...
    
    // ========== YENİ PROPERTY-LƏR (ƏLAVƏ EDİN) ==========
    // Guest user properties
    public bool IsGuest { get; set; } = false;
    public bool IsProfileCompleted { get; set; } = false;
    public DateTime? GuestCreatedAt { get; set; }
    public DateTime? ProfileCompletedAt { get; set; }
    public string? GuestDeviceId { get; set; }
    public DateTime? LastProfileReminderAt { get; set; }
    
    // QEYD: Navigation property-ləri (DeviceSessions, RefreshTokens) 
    // əlavə etməyə ehtiyac yoxdur, çünki web layihəsində istifadə olunmayacaq
}
```

### DbContext

**Heç bir dəyişiklik lazım deyil!** Yeni cədvəllər (`DeviceSessions`, `RefreshTokens`) web layihəsində istifadə olunmayacaq, ona görə də DbContext-də ignore etməyə və ya DbSet əlavə etməyə ehtiyac yoxdur.

### Migration

**Web layihəsində migration yaratmayın!** 

1. Əvvəlcə **API layihəsində** migration yaradın və tətbiq edin:
   ```powershell
   dotnet ef migrations add AddGuestUserSupport --project ../Loyalty.DataAccess --startup-project .
   dotnet ef database update --project ../Loyalty.DataAccess --startup-project .
   ```

2. Sonra **web layihəsində** yalnız ApplicationUser property-lərini əlavə edin.

3. Web layihəsi verilənlər bazasında mövcud column-ları avtomatik tanıyacaq.

## Nə üçün bu qədər sadə?

- ✅ Yeni property-lər **nullable** və ya **default dəyərlərlə**dir
- ✅ Navigation property-lər **optional**-dır (web layihəsində istifadə etməyəcəksinizsə)
- ✅ Yeni cədvəllər web layihəsində istifadə olunmayacaq
- ✅ Mövcud kod **heç bir dəyişiklik olmadan** işləyəcək

## Test

Migration tətbiq edildikdən sonra web layihəsini işə salın və mövcud funksionallığın işlədiyini yoxlayın. Yeni property-lər mövcud kodda istifadə olunmadığı üçün heç bir problem olmayacaq.

