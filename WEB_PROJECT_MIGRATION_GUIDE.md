# Web Layihəsi üçün Migration Təlimatları

Eyni verilənlər bazasını istifadə edən web layihəsində minimum dəyişikliklər üçün təlimatlar.

## Əlavə edilən dəyişikliklər

### 1. ApplicationUser entity-də yeni property-lər:
- `IsGuest` (bool, default: false)
- `IsProfileCompleted` (bool, default: false)
- `GuestCreatedAt` (DateTime?, nullable)
- `ProfileCompletedAt` (DateTime?, nullable)
- `GuestDeviceId` (string?, nullable)
- `LastProfileReminderAt` (DateTime?, nullable)
- `DeviceSessions` (ICollection<DeviceSession>?, navigation property)
- `RefreshTokens` (ICollection<RefreshToken>?, navigation property)

### 2. Yeni entity-lər:
- `DeviceSession`
- `RefreshToken`

### 3. DbContext-də yeni DbSet-lər:
- `DeviceSessions`
- `RefreshTokens`

## Web layihəsində ediləcək dəyişikliklər

### Seçim 1: Minimal dəyişiklik (tövsiyə olunur)

Web layihəsində yalnız **ApplicationUser entity**-ni yeniləyin. Yeni property-lər nullable və default dəyərlərlə olduğu üçün mövcud kod pozulmayacaq.

#### 1. ApplicationUser.cs yeniləyin:

```csharp
public class ApplicationUser : IdentityUser<int>
{
    // ... mövcud property-lər ...
    
    // Guest user properties (nullable və default dəyərlərlə)
    public bool IsGuest { get; set; } = false;
    public bool IsProfileCompleted { get; set; } = false;
    public DateTime? GuestCreatedAt { get; set; }
    public DateTime? ProfileCompletedAt { get; set; }
    public string? GuestDeviceId { get; set; }
    public DateTime? LastProfileReminderAt { get; set; }
    
    // Navigation properties (nullable)
    public ICollection<DeviceSession>? DeviceSessions { get; set; }
    public ICollection<RefreshToken>? RefreshTokens { get; set; }
}
```

#### 2. DbContext-də yeni DbSet-ləri əlavə edin (və ya ignore edin):

**Variant A: DbSet əlavə edin (tövsiyə olunur):**
```csharp
public DbSet<DeviceSession> DeviceSessions { get; set; }
public DbSet<RefreshToken> RefreshTokens { get; set; }
```

**Variant B: Ignore edin (web layihəsində istifadə etməyəcəksinizsə):**
```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    
    // Yeni entity-ləri ignore edin
    modelBuilder.Ignore<DeviceSession>();
    modelBuilder.Ignore<RefreshToken>();
    
    // ... digər konfiqurasiyalar ...
}
```

#### 3. Migration tətbiq edin:

```powershell
# Web layihəsində
dotnet ef database update
```

### Seçim 2: Tam dəstək (yeni funksionallığı istifadə etmək istəyirsinizsə)

Əgər web layihəsində də guest user funksionallığını istifadə etmək istəyirsinizsə:

1. **ApplicationUser.cs** - yuxarıdakı kimi yeniləyin
2. **DeviceSession.cs** və **RefreshToken.cs** entity-lərini əlavə edin
3. **DbContext**-də DbSet-ləri və konfiqurasiyaları əlavə edin
4. Migration tətbiq edin

## Əhəmiyyətli qeydlər

### ✅ Təhlükəsiz dəyişikliklər:
- Bütün yeni property-lər **nullable** və ya **default dəyərlərlə**dir
- Mövcud kod **pozulmayacaq**
- Yalnız yeni column-lar əlavə olunacaq, mövcud column-lar dəyişməyəcək

### ⚠️ Diqqət:
- Migration zamanı `AspNetUsers` cədvəlinə yeni column-lar əlavə olunacaq
- Yeni cədvəllər (`DeviceSessions`, `RefreshTokens`) yaradılacaq
- Web layihəsi bu yeni cədvəlləri istifadə etmirsə, problem olmayacaq

### 🔄 Migration sırası:
1. **Əvvəlcə API layihəsində** migration yaradın və tətbiq edin
2. **Sonra web layihəsində** yalnız ApplicationUser property-lərini əlavə edin
3. Web layihəsində migration yaratmayın (artıq tətbiq olunub)

## Minimum dəyişiklik nümunəsi

Web layihəsində yalnız bu dəyişiklikləri edin:

```csharp
// ApplicationUser.cs
public class ApplicationUser : IdentityUser<int>
{
    // ... mövcud kod ...
    
    // Yeni property-lər (nullable və default dəyərlərlə)
    public bool IsGuest { get; set; } = false;
    public bool IsProfileCompleted { get; set; } = false;
    public DateTime? GuestCreatedAt { get; set; }
    public DateTime? ProfileCompletedAt { get; set; }
    public string? GuestDeviceId { get; set; }
    public DateTime? LastProfileReminderAt { get; set; }
    
    // Navigation properties (nullable - web layihəsində istifadə etməyəcəksinizsə)
    public ICollection<DeviceSession>? DeviceSessions { get; set; }
    public ICollection<RefreshToken>? RefreshTokens { get; set; }
}
```

**DbContext-də:**
```csharp
// Yalnız ignore edin (və ya DbSet əlavə edin, amma istifadə etməyin)
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    
    // Yeni entity-ləri ignore edin (web layihəsində istifadə etməyəcəksinizsə)
    modelBuilder.Ignore<DeviceSession>();
    modelBuilder.Ignore<RefreshToken>();
}
```

Bu qədər! Mövcud kodunuz işləməyə davam edəcək.

