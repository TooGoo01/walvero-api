// ============================================
// WEB LAYİHƏSİ ÜÇÜN MİNİMAL DƏYİŞİKLİKLƏR
// ============================================

// 1. ApplicationUser.cs faylına əlavə edin:

using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using Microsoft.AspNetCore.Identity;

namespace Loyalty.DataAccess.Entities.Users
{
    public class ApplicationUser : IdentityUser<int>
    {
        // ... mövcud property-lər ...
        public string Name { get; set; }
        public string Surname { get; set; }
        public string Position { get; set; }
        public Gender? Gender { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public int? TenantId { get; set; }
        public Tenant? Tenant { get; set; }
        public ICollection<DeviceToken>? DeviceTokens { get; set; } = new List<DeviceToken>();
        public ICollection<Customer>? Customers { get; set; }
        
        // ========== YENİ PROPERTY-LƏR (ƏLAVƏ EDİN) ==========
        // Guest user properties - nullable və default dəyərlərlə
        public bool IsGuest { get; set; } = false;
        public bool IsProfileCompleted { get; set; } = false;
        public DateTime? GuestCreatedAt { get; set; }
        public DateTime? ProfileCompletedAt { get; set; }
        public string? GuestDeviceId { get; set; }
        public DateTime? LastProfileReminderAt { get; set; }
        
        // Navigation properties - nullable (web layihəsində istifadə etməyəcəksinizsə)
        // QEYD: DeviceSession və RefreshToken entity-lərini web layihəsində yaratmağa ehtiyac yoxdur
        // Yalnız property-ləri nullable olaraq bura əlavə edin
        // public ICollection<DeviceSession>? DeviceSessions { get; set; }
        // public ICollection<RefreshToken>? RefreshTokens { get; set; }
    }
}


// ============================================
// 2. DbContext-də (əgər lazımdırsa):
// ============================================

// Variant A: Yeni entity-ləri ignore edin (tövsiyə olunur)
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    
    // Yeni entity-ləri ignore edin (web layihəsində istifadə etməyəcəksinizsə)
    // QEYD: DeviceSession və RefreshToken class-larını web layihəsində yaratmağa ehtiyac yoxdur
    // modelBuilder.Ignore<DeviceSession>();
    // modelBuilder.Ignore<RefreshToken>();
    
    // ... digər mövcud konfiqurasiyalar ...
}

// Variant B: DbSet əlavə edin (lakin istifadə etməyin)
// public DbSet<DeviceSession> DeviceSessions { get; set; }
// public DbSet<RefreshToken> RefreshTokens { get; set; }


// ============================================
// 3. Migration (web layihəsində):
// ============================================

// QEYD: Migration yaratmayın! 
// API layihəsində migration artıq tətbiq olunacaq.
// Web layihəsində yalnız ApplicationUser property-lərini əlavə edin və
// verilənlər bazasında mövcud column-ları tanıyacaq.

// Əgər web layihəsində də migration yaratmaq istəyirsinizsə:
// dotnet ef migrations add AddGuestUserProperties --context YourDbContext
// dotnet ef database update --context YourDbContext

// Amma ən yaxşısı: API layihəsində migration tətbiq edin, 
// sonra web layihəsində yalnız property-ləri əlavə edin.


// ============================================
// 4. AutoMapper (əgər istifadə edirsinizsə):
// ============================================

// Yeni property-lər AutoMapper tərəfindən avtomatik map olunacaq.
// Əlavə konfiqurasiya lazım deyil, çünki property adları eynidir.


// ============================================
// 5. DTO-lar (əgər istifadə edirsinizsə):
// ============================================

// ApplicationUserDto-ya yeni property-ləri əlavə etməyə ehtiyac yoxdur
// (web layihəsində istifadə etməyəcəksinizsə).
// Amma əgər istifadə edəcəksinizsə:

public class ApplicationUserDto
{
    // ... mövcud property-lər ...
    
    // Yeni property-lər (optional)
    public bool IsGuest { get; set; }
    public bool IsProfileCompleted { get; set; }
    // ... digər property-lər ...
}

