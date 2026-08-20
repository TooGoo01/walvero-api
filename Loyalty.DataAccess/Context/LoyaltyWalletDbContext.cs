using Loyalty.DataAccess.Entities.Base;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Entities.Sequences;
using Loyalty.DataAccess.Entities.Users;
using Loyalty.DataAccess.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;
using System.Text.RegularExpressions;
using File = Loyalty.DataAccess.Entities.Main.File;

namespace Loyalty.DataAccess.Context
{
    public class LoyaltyWalletDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, int>
    {
        public LoyaltyWalletDbContext(DbContextOptions options) : base(options)
        {

        }
        #region Main
        public DbSet<Tenant> Tenants { get; set; }
        public DbSet<Contact> Contacts { get; set; }
        public DbSet<SalesRepVisit> SalesRepVisits { get; set; }
        public DbSet<DeviceToken> DeviceTokens { get; set; }
        public DbSet<RedeemRequest> RedeemRequests { get; set; }
        public DbSet<EkassaJsonAudit> EkassaJsonAudits { get; set; }
        public DbSet<PassDeviceRegistration> PassDeviceRegistrations { get; set; }
        public DbSet<TenantBranding> TenantBrandings { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<CardTemplate> CardTemplates { get; set; }
        public DbSet<LoyaltyProgram> LoyaltyPrograms { get; set; }
        public DbSet<LoyaltyCard> LoyaltyCards { get; set; }
        public DbSet<WalletPass> WalletPasses { get; set; }
        public DbSet<WalveroWalletPass> WalveroWalletPasses { get; set; }
        public DbSet<WalletCredential> WalletCredentials { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<Reward> Rewards { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<AppVersionConfig> AppVersionConfigs { get; set; }
        public DbSet<AppSetting> AppSettings { get; set; }
        public DbSet<StoreFeedback> StoreFeedbacks { get; set; }
        public DbSet<StoreFeedbackReply> StoreFeedbackReplies { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<DeviceSession> DeviceSessions { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<Otp> Otps { get; set; }
        public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }
        
        // Sequence tables for ID generation
        public DbSet<CountrySequence> CountrySequences { get; set; }
        public DbSet<TenantCustomerSequence> TenantCustomerSequences { get; set; }
        public DbSet<Offer> Offers { get; set; }
        public DbSet<Story> Stories { get; set; }
        public DbSet<CardPrefixSequence> CardPrefixSequences { get; set; }
        public DbSet<PosSession> PosSessions { get; set; }
        public DbSet<Country> Countries { get; set; }
        public DbSet<City> Cities { get; set; }
        public DbSet<Entities.Main.TimeZone> TimeZones { get; set; }
        public DbSet<TenantLocation> TenantLocations { get; set; }
        
        // Menu related tables
        public DbSet<MenuCategory> MenuCategories { get; set; }
        public DbSet<MenuItem> MenuItems { get; set; }
        public DbSet<MenuItemImage> MenuItemImages { get; set; }
        public DbSet<RestorantWifiInformation> RestorantWifiInformations { get; set; }
        public DbSet<TenantLink> TenantLinks { get; set; }
        public DbSet<IntegrationType> IntegrationTypes { get; set; }
        public DbSet<TenantIntegration> TenantIntegrations { get; set; }
        public DbSet<PosProductEarnRule> PosProductEarnRules { get; set; }
        public DbSet<PosLoyaltyDiscountType> PosLoyaltyDiscountTypes { get; set; }
        public DbSet<PosCustomerMapping> PosCustomerMappings { get; set; }

        // Branches & Rooms
        public DbSet<Branch> Branches { get; set; }
        public DbSet<Room> Rooms { get; set; }
        public DbSet<RoomFile> RoomFiles { get; set; }
        public DbSet<LotteryDraw> LotteryDraws { get; set; }
        public DbSet<LotteryTicket> LotteryTickets { get; set; }
        public DbSet<LotteryPrize> LotteryPrizes { get; set; }
        public DbSet<HappyHoursWindow> HappyHoursWindows { get; set; }
        public DbSet<TelegramAdminLink> TelegramAdminLinks { get; set; }
        public DbSet<TelegramLinkCode> TelegramLinkCodes { get; set; }
        #endregion
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<TelegramAdminLink>(entity =>
            {
                entity.ToTable("TelegramAdminLinks");
                entity.HasKey(l => l.Id);
                entity.Property(l => l.TelegramUsername).HasMaxLength(100);

                // Bir chat eyni anda yalnız bir aktiv linkə sahib ola bilər
                entity.HasIndex(l => l.ChatId)
                    .IsUnique()
                    .HasFilter("[IsActive] = 1")
                    .HasDatabaseName("IX_TelegramAdminLinks_ChatId_Active");

                entity.HasIndex(l => new { l.TenantId, l.ApplicationUserId })
                    .HasDatabaseName("IX_TelegramAdminLinks_Tenant_User");

                entity.HasOne(l => l.ApplicationUser)
                    .WithMany()
                    .HasForeignKey(l => l.ApplicationUserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
            modelBuilder.Entity<TelegramLinkCode>(entity =>
            {
                entity.ToTable("TelegramLinkCodes");
                entity.HasKey(c => c.Id);
                entity.Property(c => c.Code).IsRequired().HasMaxLength(16);
                entity.HasIndex(c => c.Code)
                    .IsUnique()
                    .HasDatabaseName("IX_TelegramLinkCodes_Code");
                entity.HasIndex(c => c.ApplicationUserId)
                    .HasDatabaseName("IX_TelegramLinkCodes_User");
            });
            modelBuilder.Entity<AppSetting>(entity =>
            {
                entity.ToTable("AppSettings");
                entity.HasKey(s => s.Id);
                entity.Property(s => s.Key).IsRequired().HasMaxLength(100);
                entity.Property(s => s.Value).IsRequired().HasMaxLength(500);
                entity.HasIndex(s => s.Key).IsUnique();
            });
            modelBuilder.Entity<Tenant>(entity =>
            {
                entity.ToTable("Tenants");
                entity.HasKey(t => t.Id);
                entity.Property(t => t.CompanyName).IsRequired().HasMaxLength(200);
                entity.Property(t => t.Subdomain).IsRequired().HasMaxLength(100);
                entity.HasIndex(t => t.Subdomain).IsUnique();
                entity.Property(t => t.ContactEmail).IsRequired().HasMaxLength(200);
                entity.Property(t => t.ContactPhone).IsRequired().HasMaxLength(50);

                // Koordinat dəqiqliyi — default decimal(18,2) yalnız ~1km; geofence üçün 6 onluq lazım.
                entity.Property(t => t.Latitude).HasPrecision(9, 6);
                entity.Property(t => t.Longitude).HasPrecision(9, 6);

                // Performans index-ləri:
                //  - GetActiveFiltered (Discover) (IsActive, IsHidden, categoryId) üzrə filterlə
                entity.HasIndex(t => new { t.IsActive, t.IsHidden })
                    .HasDatabaseName("IX_Tenants_IsActive_IsHidden");
                entity.HasIndex(t => new { t.categoryId, t.IsActive })
                    .HasDatabaseName("IX_Tenants_Category_IsActive");

                entity.HasOne(t => t.Branding)
                    .WithOne(b => b.Tenant)
                    .HasForeignKey<TenantBranding>(b => b.TenantId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(t => t.Admins)
                    .WithOne(a => a.Tenant)
                    .HasForeignKey(a => a.TenantId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(t => t.Customers)
                    .WithOne(c => c.Tenant)
                    .HasForeignKey(c => c.TenantId)
                    .OnDelete(DeleteBehavior.Restrict);


                entity.HasMany(t => t.LoyaltyPrograms)
                    .WithOne(lp => lp.Tenant)
                    .HasForeignKey(lp => lp.TenantId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(t => t.IsHidden)
                    .IsRequired()
                    .HasDefaultValue(false);

                entity.Property(t => t.IsHasMenu)
                    .IsRequired()
                    .HasDefaultValue(false);

                // Receipt OCR + suspicious-tx config (decimal precision)
                entity.Property(t => t.OcrToleranceAzn).HasPrecision(18, 2);
                entity.Property(t => t.SuspiciousAmountCap).HasPrecision(18, 2);
                entity.Property(t => t.AlertWhatsappPhone).HasMaxLength(20);

                  entity.HasOne(t => t.TenantLocation)
                   .WithOne(tl => tl.Tenant)
                   .HasForeignKey<Tenant>(t => t.tenantLocationId)
                   .OnDelete(DeleteBehavior.SetNull);
            });
            modelBuilder.Entity<TenantBranding>(entity =>
            {
                entity.ToTable("TenantBrandings");
                entity.HasKey(tb => tb.Id);
                entity.Property(tb => tb.LogoUrl).HasMaxLength(500);
                entity.Property(tb => tb.BackgroundImageUrl).HasMaxLength(500);
                entity.Property(tb => tb.PrimaryColor).HasMaxLength(20);
                entity.Property(tb => tb.SecondaryColor).HasMaxLength(20);
                entity.Property(tb => tb.Description).HasMaxLength(1000);
                entity.Property(tb => tb.Website).HasMaxLength(500);
            });
            modelBuilder.Entity<Customer>(entity =>
            {
                entity.ToTable("Customers");
                entity.HasKey(c => c.Id);
                entity.Property(c => c.FirstName).IsRequired().HasMaxLength(100);
                entity.Property(c => c.LastName).IsRequired().HasMaxLength(100);
                entity.Property(c => c.PhoneNumber).IsRequired().HasMaxLength(20);
                entity.Property(c => c.Email).HasMaxLength(200);
                entity.Property(x => x.OtpHash).HasMaxLength(200);
                entity.Property(x => x.OtpSalt).HasMaxLength(100);
                entity.Property(x => x.OtpChannel).HasMaxLength(20);
                entity.Property(x => x.OtpDestinationMasked).HasMaxLength(120);
                entity.Property(c => c.Gender).HasConversion<int>();
                entity.Property(c => c.CardNumber).HasMaxLength(50);

                entity.HasIndex(c => new { c.TenantId, c.PhoneNumber }).IsUnique();
                entity.HasIndex(c => c.CardNumber)
                    .IsUnique()
                    .HasFilter("[CardNumber] IS NOT NULL");

                // Performans index-ləri:
                //  - GetByTenant: (TenantId, IsActive) tez-tez filterlə
                //  - GetByUserId: applicationUserId tək sütun, IsActive composite ilə daha aydın
                entity.HasIndex(c => new { c.TenantId, c.IsActive })
                    .HasDatabaseName("IX_Customers_Tenant_IsActive");
                entity.HasIndex(c => new { c.applicationUserId, c.IsActive })
                    .HasDatabaseName("IX_Customers_AppUser_IsActive");

                entity.HasMany(c => c.LoyaltyCards)
                    .WithOne(lc => lc.Customer)
                    .HasForeignKey(lc => lc.CustomerId)
                    .OnDelete(DeleteBehavior.Cascade); // Changed to Cascade - delete cards when customer is deleted

                entity.HasOne(wc => wc.applicationUser)
                   .WithMany(t => t.Customers)
                   .HasForeignKey(wc => wc.applicationUserId)
                   .OnDelete(DeleteBehavior.Cascade);


            });
            // DeviceToken.UserId nullable — guest tokens (login olmamış istifadəçilər)
            // üçün UserId=null saxlanılır. User silinəndə token UserId=null olur, silinmir.
            modelBuilder.Entity<DeviceToken>()
                .HasOne(dt => dt.User)
                .WithMany(u => u.DeviceTokens)
                .HasForeignKey(dt => dt.UserId)
                .OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<CardTemplate>(entity =>
            {
                entity.ToTable("CardTemplates");
                entity.HasKey(ct => ct.Id);
                entity.Property(ct => ct.TemplateName).IsRequired().HasMaxLength(200);
                entity.Property(ct => ct.TemplateType).HasConversion<int>();
            
                entity.Property(ct => ct.StepLabel).HasMaxLength(50);
                entity.Property(ct => ct.RewardDescription).HasMaxLength(500);
                entity.Property(ct => ct.FreeRewardLabel).HasMaxLength(100);
                entity.Property(ct => ct.BronzeIconUrl).HasMaxLength(500);
                entity.Property(ct => ct.SilverIconUrl).HasMaxLength(500);
                entity.Property(ct => ct.GoldIconUrl).HasMaxLength(500);
                entity.Property(ct => ct.PlatinumIconUrl).HasMaxLength(500);
                entity.Property(ct => ct.BackgroundColor).HasMaxLength(20);
                entity.Property(ct => ct.ForegroundColor).HasMaxLength(20);
                entity.Property(ct => ct.LabelColor).HasMaxLength(20);
                entity.Property(ct => ct.ProgressBarColor).HasMaxLength(20);
                entity.Property(ct => ct.LogoUrl).HasMaxLength(500);
                entity.Property(ct => ct.BackgroundImageUrl).HasMaxLength(500);
                entity.Property(ct => ct.StripImageUrl).HasMaxLength(500);
                entity.Property(ct => ct.HeroImageUrl).HasMaxLength(500);
                entity.Property(ct => ct.IconLayout).HasConversion<int>();
                entity.Property(ct => ct.Currency).HasMaxLength(50).HasDefaultValue("AZN");
                entity.HasOne(lp => lp.LoyaltyProgram)
                  .WithOne(ct => ct.CardTemplate)
                  .HasForeignKey<CardTemplate>(lp => lp.LoyaltyProgramId)
                  .OnDelete(DeleteBehavior.Restrict);


            });
                    modelBuilder.Entity<Category>()
        .HasMany(dt => dt.Tenants)
        .WithOne(u => u.Category)
        .HasForeignKey(dt => dt.categoryId)
        .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<LoyaltyProgram>(entity =>
            {
                entity.ToTable("LoyaltyPrograms");
                entity.HasKey(lp => lp.Id);
                entity.Property(lp => lp.ProgramName).IsRequired().HasMaxLength(200);
                entity.Property(lp => lp.Description).HasMaxLength(1000);
                entity.Property(lp => lp.ProgramType).HasConversion<int>();
                entity.Property(lp => lp.PointsPerCurrency).HasColumnType("decimal(18,4)");
                entity.Property(lp => lp.CurrencyPerPoint).HasColumnType("decimal(18,4)");

                entity.HasOne(lp => lp.CardTemplate)
                    .WithOne(ct => ct.LoyaltyProgram)
                    .HasForeignKey<LoyaltyProgram>(lp => lp.CardTemplateId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(lp => lp.LoyaltyCards)
                    .WithOne(lc => lc.LoyaltyProgram)
                    .HasForeignKey(lc => lc.LoyaltyProgramId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(lp => lp.Rewards)
                    .WithOne(r => r.LoyaltyProgram)
                    .HasForeignKey(r => r.LoyaltyProgramId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(lp => lp.HappyHoursWindows)
                    .WithOne(w => w.LoyaltyProgram)
                    .HasForeignKey(w => w.LoyaltyProgramId)
                    .OnDelete(DeleteBehavior.Cascade);

            });

            modelBuilder.Entity<HappyHoursWindow>(entity =>
            {
                entity.ToTable("HappyHoursWindows");
                entity.HasKey(w => w.Id);
                entity.Property(w => w.StartTime).HasColumnType("time");
                entity.Property(w => w.EndTime).HasColumnType("time");
                entity.Property(w => w.Label).HasMaxLength(100);
                entity.HasIndex(w => new { w.LoyaltyProgramId, w.DayOfWeek, w.SpecificDate })
                    .HasDatabaseName("IX_HappyHoursWindows_Program_Day_Date");
            });
            modelBuilder.Entity<LoyaltyCard>(entity =>
            {
                entity.ToTable("LoyaltyCards");
                entity.HasKey(lc => lc.Id);
                entity.Property(lc => lc.CardNumber).IsRequired().HasMaxLength(50);
                entity.HasIndex(lc => lc.CardNumber).IsUnique();
                entity.Property(lc => lc.BarcodeValue).HasMaxLength(200);
                entity.Property(lc => lc.BarcodeType).HasConversion<int>();
                entity.Property(lc => lc.Status).HasConversion<int>();

                entity.Property(lc => lc.CurrentPoints).HasPrecision(18, 2);
                entity.Property(lc => lc.LifetimePoints).HasPrecision(18, 2);

                // Performans index-ləri (audit nəticəsində):
                //  - GetMyCardsPaginatedAsync (TenantId, CustomerId, IsActive) üzrə filterlə
                //  - ListByCustomerAsync (TenantId, CustomerId) + Order by Id
                //  - GetLastActive/Inactive (TenantId, CustomerId, LoyaltyProgramId, IsActive)
                entity.HasIndex(lc => new { lc.TenantId, lc.CustomerId, lc.IsActive })
                    .HasDatabaseName("IX_LoyaltyCards_Tenant_Customer_IsActive");
                entity.HasIndex(lc => new { lc.LoyaltyProgramId, lc.TenantId })
                    .HasDatabaseName("IX_LoyaltyCards_Program_Tenant");

                entity.HasMany(lc => lc.WalletPasses)
                    .WithOne(wp => wp.LoyaltyCard)
                    .HasForeignKey(wp => wp.LoyaltyCardId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(lc => lc.Transactions)
                    .WithOne(t => t.LoyaltyCard)
                    .HasForeignKey(t => t.LoyaltyCardId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(lc => lc.LotteryTickets)
                    .WithOne(lt => lt.LoyaltyCard)
                    .HasForeignKey(lt => lt.LoyaltyCardId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ==== LOTTERY ====
            modelBuilder.Entity<LotteryTicket>(entity =>
            {
                entity.ToTable("LotteryTickets");
                entity.HasKey(lt => lt.Id);
                entity.Property(lt => lt.TicketCode).IsRequired().HasMaxLength(20);
                entity.HasIndex(lt => lt.TicketCode).IsUnique();
                entity.Property(lt => lt.Status).HasConversion<int>();
                entity.HasIndex(lt => new { lt.TenantId, lt.Status });

                entity.HasOne(lt => lt.LotteryDraw)
                    .WithMany(ld => ld.WinningTickets)
                    .HasForeignKey(lt => lt.LotteryDrawId)
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ==== BRANCH ====
            modelBuilder.Entity<Branch>(entity =>
            {
                entity.ToTable("Branches");
                entity.HasKey(b => b.Id);
                entity.Property(b => b.Name).IsRequired().HasMaxLength(200);
                entity.Property(b => b.Slug).HasMaxLength(64);
                entity.Property(b => b.Address).HasMaxLength(500);
                entity.Property(b => b.Phone).HasMaxLength(50);
                entity.Property(b => b.Email).HasMaxLength(200);
                entity.Property(b => b.PhotoUrl).HasMaxLength(500);
                entity.Property(b => b.WorkingHoursJson).HasMaxLength(2000);

                // Suspicious-tx config (branch override, tenant fallback)
                entity.Property(b => b.OcrToleranceAzn).HasPrecision(18, 2);
                entity.Property(b => b.SuspiciousAmountCap).HasPrecision(18, 2);
                entity.Property(b => b.AlertWhatsappPhone).HasMaxLength(20);

                entity.HasIndex(b => new { b.TenantId, b.IsActive });
                entity.HasIndex(b => new { b.TenantId, b.Slug })
                    .IsUnique()
                    .HasFilter("[Slug] IS NOT NULL");

                entity.HasOne(b => b.Tenant)
                    .WithMany(t => t.Branches)
                    .HasForeignKey(b => b.TenantId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ApplicationUser → Branch (nullable FK)
            modelBuilder.Entity<ApplicationUser>(entity =>
            {
                entity.HasOne(u => u.Branch)
                    .WithMany(b => b.Staff)
                    .HasForeignKey(u => u.BranchId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Transaction → Branch (nullable FK)
            modelBuilder.Entity<Transaction>(entity =>
            {
                entity.HasOne(t => t.Branch)
                    .WithMany(b => b.Transactions)
                    .HasForeignKey(t => t.BranchId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(t => t.BranchId)
                    .HasDatabaseName("IX_Transactions_BranchId");
            });

            modelBuilder.Entity<LotteryDraw>(entity =>
            {
                entity.ToTable("LotteryDraws");
                entity.HasKey(ld => ld.Id);
                entity.Property(ld => ld.DrawName).IsRequired().HasMaxLength(200);
                entity.Property(ld => ld.PrizeName).HasMaxLength(200);
                entity.Property(ld => ld.PrizeDescription).HasMaxLength(1000);
                entity.Property(ld => ld.Status).HasConversion<int>();

                entity.HasOne(ld => ld.LoyaltyProgram)
                    .WithMany(lp => lp.LotteryDraws)
                    .HasForeignKey(ld => ld.LoyaltyProgramId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<LotteryPrize>(entity =>
            {
                entity.ToTable("LotteryPrizes");
                entity.HasKey(lp => lp.Id);
                entity.Property(lp => lp.PrizeName).IsRequired().HasMaxLength(200);
                entity.Property(lp => lp.PrizeDescription).HasMaxLength(1000);
            });
            modelBuilder.Entity<Transaction>(entity =>
            {
                entity.Property(t => t.Points).HasPrecision(18, 2);
                entity.Property(t => t.BalanceAfter).HasPrecision(18, 2);
                entity.Property(t => t.OcrExtractedAmount).HasPrecision(18, 2);
                entity.Property(t => t.ReceiptImageUrl).HasMaxLength(500);
                entity.Property(t => t.SuspiciousReason).HasMaxLength(100);
            });

            modelBuilder.Entity<RedeemRequest>(entity =>
            {
                entity.Property(t => t.OcrExtractedAmount).HasPrecision(18, 2);
                entity.Property(t => t.ReceiptImageUrl).HasMaxLength(500);

                entity.HasOne(t => t.Branch)
                    .WithMany()
                    .HasForeignKey(t => t.BranchId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<WalletPass>(entity =>
            {
                entity.ToTable("WalletPasses");
                entity.HasKey(wp => wp.Id);
                entity.Property(wp => wp.Platform).HasConversion<int>();
                entity.Property(wp => wp.SerialNumber).IsRequired().HasMaxLength(100);
                entity.HasIndex(wp => wp.SerialNumber).IsUnique();
                entity.Property(wp => wp.PassTypeIdentifier).IsRequired().HasMaxLength(200);
                entity.Property(wp => wp.PassUrl).HasMaxLength(500);
                entity.Property(wp => wp.Status).HasConversion<int>();

                entity.HasIndex(wp => new { wp.LoyaltyCardId, wp.Platform }).IsUnique();
            });
            // ==== WALVERO WALLET PASS ====
            modelBuilder.Entity<WalveroWalletPass>(entity =>
            {
                entity.ToTable("WalveroWalletPasses");
                entity.HasKey(wwp => wwp.Id);
                entity.Property(wwp => wwp.WalveroIdMasked).IsRequired().HasMaxLength(50);
                entity.Property(wwp => wwp.ApplePassUrl).HasMaxLength(500);
                entity.Property(wwp => wwp.GoogleSaveUrl).HasMaxLength(2000); // Google Wallet save links can be very long (JWT tokens)
                entity.Property(wwp => wwp.AppleSerialNumber).HasMaxLength(100);
                entity.Property(wwp => wwp.GoogleObjectId).HasMaxLength(200);

                entity.HasIndex(wwp => new { wwp.ApplicationUserId, wwp.TenantId }).IsUnique();
                entity.HasIndex(wwp => wwp.ApplicationUserId);
                entity.HasIndex(wwp => wwp.TenantId);

                entity.HasOne(wwp => wwp.ApplicationUser)
                    .WithMany()
                    .HasForeignKey(wwp => wwp.ApplicationUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(wwp => wwp.Tenant)
                    .WithMany()
                    .HasForeignKey(wwp => wwp.TenantId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
            // ==== WALLET CREDENTIAL ====
            modelBuilder.Entity<WalletCredential>(entity =>
            {
                entity.ToTable("WalletCredentials");
                entity.HasKey(wc => wc.Id);
                entity.Property(wc => wc.Platform).HasConversion<int>();
                entity.Property(wc => wc.TeamIdentifier).HasMaxLength(50);
                entity.Property(wc => wc.PassTypeIdentifier).HasMaxLength(200);
                entity.Property(wc => wc.AppleCertificateUrl).HasMaxLength(500);
                entity.Property(wc => wc.CertificatePassword).HasMaxLength(200);
                entity.Property(wc => wc.IssuerId).HasMaxLength(100);
                entity.Property(wc => wc.ServiceAccountEmail).HasMaxLength(200);
                entity.Property(wc => wc.GooglePrivateKeyUrl).HasMaxLength(500);

                entity.HasIndex(wc => new { wc.TenantId, wc.Platform }).IsUnique();

                entity.HasOne(wc => wc.Tenant)
                    .WithMany(t => t.WalletCredentials)
                    .HasForeignKey(wc => wc.TenantId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
            modelBuilder.Entity<Transaction>(entity =>
            {
                entity.ToTable("Transactions");
                entity.HasKey(t => t.Id);
                entity.Property(t => t.Type).HasConversion<int>();
                entity.Property(t => t.Amount).HasColumnType("decimal(18,2)");
                entity.Property(t => t.Description).HasMaxLength(500);
                entity.Property(t => t.TimeZoneIdUsed).IsRequired().HasMaxLength(100).HasDefaultValue("UTC");
                entity.Property(t => t.LocalOccurredAt).IsRequired();

                entity.HasIndex(t => t.RegDate);
                entity.HasIndex(t => t.LoyaltyCardId);
                entity.HasIndex(t => t.LocalOccurredAt);

                entity.Property(t => t.PhysicalReceiptId).HasMaxLength(64);
                entity.Property(t => t.QrDocId).HasMaxLength(128);
                entity.Property(t => t.ExtractedObjectCode).HasMaxLength(32);
                entity.HasIndex(t => t.PhysicalReceiptId)
                    .HasFilter("[PhysicalReceiptId] IS NOT NULL");
            });

            modelBuilder.Entity<EkassaJsonAudit>(entity =>
            {
                entity.ToTable("EkassaJsonAudits");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.QrDocId).HasMaxLength(128);
                entity.Property(e => e.ParsedObjectCode).HasMaxLength(32);
                entity.Property(e => e.ParsedDocumentNumber).HasMaxLength(64);
                entity.Property(e => e.ParsedAmount).HasColumnType("decimal(18,2)");
                entity.HasIndex(e => e.TenantId);
                entity.HasIndex(e => e.RegDate);
            });
            // ==== REWARD ====
            modelBuilder.Entity<Reward>(entity =>
            {
                entity.ToTable("Rewards");
                entity.HasKey(r => r.Id);
                entity.Property(r => r.Name).IsRequired().HasMaxLength(200);
                entity.Property(r => r.Description).HasMaxLength(1000);
                entity.Property(r => r.ImageUrl).HasMaxLength(500);

            });

            // ==== STORE FEEDBACK ====
            modelBuilder.Entity<StoreFeedback>(entity =>
            {
                entity.ToTable("StoreFeedbacks");
                entity.HasKey(sf => sf.Id);
                entity.Property(sf => sf.Message).IsRequired().HasMaxLength(2000);
                entity.Property(sf => sf.Rating).HasColumnType("decimal(3,2)");
                entity.Property(sf => sf.Type).HasConversion<int>();
                entity.Property(sf => sf.Status).HasConversion<int>();
                entity.Property(sf => sf.CreatedAt).IsRequired();
                entity.Property(sf => sf.UpdatedAt);

                entity.HasIndex(sf => sf.TenantId);
                entity.HasIndex(sf => sf.CustomerId);
                entity.HasIndex(sf => sf.Status);
                entity.HasIndex(sf => sf.CreatedAt);

                entity.HasOne(sf => sf.Tenant)
                    .WithMany()
                    .HasForeignKey(sf => sf.TenantId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(sf => sf.Customer)
                    .WithMany()
                    .HasForeignKey(sf => sf.CustomerId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(sf => sf.Replies)
                    .WithOne(sfr => sfr.StoreFeedback)
                    .HasForeignKey(sfr => sfr.StoreFeedbackId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ==== STORE FEEDBACK REPLY ====
            modelBuilder.Entity<StoreFeedbackReply>(entity =>
            {
                entity.ToTable("StoreFeedbackReplies");
                entity.HasKey(sfr => sfr.Id);
                entity.Property(sfr => sfr.Message).IsRequired().HasMaxLength(2000);
                entity.Property(sfr => sfr.SenderType).HasConversion<int>();
                entity.Property(sfr => sfr.CreatedAt).IsRequired();

                entity.HasIndex(sfr => sfr.StoreFeedbackId);
                entity.HasIndex(sfr => sfr.CreatedAt);
            });

            // ==== NOTIFICATION ====
            modelBuilder.Entity<Notification>(entity =>
            {
                entity.ToTable("Notifications");
                entity.HasKey(n => n.Id);
                entity.Property(n => n.Title).IsRequired().HasMaxLength(200);
                entity.Property(n => n.Body).IsRequired().HasMaxLength(1000);
                entity.Property(n => n.CreatedAt).IsRequired();

                entity.HasIndex(n => n.UserId);
                entity.HasIndex(n => n.IsRead);
                entity.HasIndex(n => n.CreatedAt);
                entity.HasIndex(n => n.RelatedFeedbackId);

                entity.HasOne(n => n.User)
                    .WithMany()
                    .HasForeignKey(n => n.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(n => n.RelatedFeedback)
                    .WithMany()
                    .HasForeignKey(n => n.RelatedFeedbackId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ==== DEVICE SESSION ====
            modelBuilder.Entity<DeviceSession>(entity =>
            {
                entity.ToTable("DeviceSessions");
                entity.HasKey(ds => ds.Id);
                entity.Property(ds => ds.DeviceId).IsRequired().HasMaxLength(200);
                entity.Property(ds => ds.CreatedAt).IsRequired();
                entity.Property(ds => ds.LastSeenAt).IsRequired();

                entity.HasIndex(ds => ds.DeviceId);
                entity.HasIndex(ds => ds.ApplicationUserId);
                entity.HasIndex(ds => ds.IsRevoked);
                entity.HasIndex(ds => new { ds.DeviceId, ds.IsRevoked });

                entity.HasOne(ds => ds.User)
                    .WithMany(u => u.DeviceSessions)
                    .HasForeignKey(ds => ds.ApplicationUserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ==== REFRESH TOKEN ====
            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.ToTable("RefreshTokens");
                entity.HasKey(rt => rt.Id);
                entity.Property(rt => rt.Token).IsRequired().HasMaxLength(500);
                entity.Property(rt => rt.ExpiresAt).IsRequired();
                entity.Property(rt => rt.CreatedAt).IsRequired();
                entity.Property(rt => rt.RevokedByIp).HasMaxLength(50);
                entity.Property(rt => rt.ReplacedByToken).HasMaxLength(500);
                entity.Property(rt => rt.ReasonRevoked).HasMaxLength(200);

                entity.HasIndex(rt => rt.Token).IsUnique();
                entity.HasIndex(rt => rt.ApplicationUserId);
                entity.HasIndex(rt => rt.ExpiresAt);

                entity.HasOne(rt => rt.User)
                    .WithMany(u => u.RefreshTokens)
                    .HasForeignKey(rt => rt.ApplicationUserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ==== REWARD ====
            modelBuilder.Entity<Reward>(entity =>
            {
                entity.ToTable("Rewards");
                entity.HasKey(r => r.Id);
                entity.Property(r => r.Name).IsRequired().HasMaxLength(200);
                entity.Property(r => r.Description).HasMaxLength(1000);
                entity.Property(r => r.ImageUrl).HasMaxLength(500);

            });

            // ==== OTP ====
            modelBuilder.Entity<Otp>(entity =>
            {
                entity.ToTable("Otps");
                entity.HasKey(o => o.Id);
                entity.Property(o => o.CodeHash).IsRequired().HasMaxLength(200);
                entity.Property(o => o.CodeSalt).IsRequired().HasMaxLength(100);
                entity.Property(o => o.ExpiresAt).IsRequired();
                entity.Property(o => o.CreatedAt).IsRequired();
                entity.Property(o => o.Purpose).HasConversion<int>();

                entity.HasIndex(o => o.ApplicationUserId);
                entity.HasIndex(o => new { o.ApplicationUserId, o.Purpose, o.IsUsed });
                entity.HasIndex(o => o.ExpiresAt);

                entity.HasOne(o => o.User)
                    .WithMany(u => u.Otps)
                    .HasForeignKey(o => o.ApplicationUserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ==== PASSWORD RESET TOKEN ====
            modelBuilder.Entity<PasswordResetToken>(entity =>
            {
                entity.ToTable("PasswordResetTokens");
                entity.HasKey(prt => prt.Id);
                entity.Property(prt => prt.Token).IsRequired().HasMaxLength(500);
                entity.Property(prt => prt.ExpiresAt).IsRequired();
                entity.Property(prt => prt.CreatedAt).IsRequired();

                entity.HasIndex(prt => prt.Token).IsUnique();
                entity.HasIndex(prt => prt.ApplicationUserId);
                entity.HasIndex(prt => prt.ExpiresAt);

                entity.HasOne(prt => prt.User)
                    .WithMany(u => u.PasswordResetTokens)
                    .HasForeignKey(prt => prt.ApplicationUserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
          

            // ==== APPLICATION USER ====
            modelBuilder.Entity<ApplicationUser>(entity =>
            {
                entity.Property(u => u.Status)
                    .HasConversion<int>();
                // Note: Default value is set in ApplicationUser entity property initializer (Status = UserStatus.Active)
                // Cannot use HasDefaultValue with enum conversion in EF Core
                
                entity.Property(u => u.IsPhoneVerified)
                    .HasDefaultValue(false);
                
                entity.Property(u => u.WalveroId)
                    .IsRequired(false);
                
                entity.HasIndex(u => u.WalveroId)
                    .IsUnique()
                    .HasFilter("[WalveroId] IS NOT NULL");
            });

            // ==== COUNTRY SEQUENCE ====
            modelBuilder.Entity<CountrySequence>(entity =>
            {
                entity.ToTable("CountrySequences");
                entity.HasKey(cs => cs.CountryCode);
                entity.Property(cs => cs.CountryCode).ValueGeneratedNever(); // Manual PK
                entity.Property(cs => cs.LastNumber).IsRequired();
                entity.Property(cs => cs.UpdatedAt).IsRequired();
                entity.Ignore(cs => cs.Id);
                entity.Ignore(cs => cs.RegUserId);
                // IsActive is now mapped so we can use it in LINQ queries
                entity.Ignore(cs => cs.RegDate);
                entity.Ignore(cs => cs.EditUserId);
                entity.Ignore(cs => cs.EditDate);
            });

            // ==== TENANT CUSTOMER SEQUENCE ====
            modelBuilder.Entity<TenantCustomerSequence>(entity =>
            {
                entity.ToTable("TenantCustomerSequences");
                entity.HasKey(tcs => tcs.TenantId);
                entity.Property(tcs => tcs.TenantId).ValueGeneratedNever(); // Manual PK
                entity.Property(tcs => tcs.LastNumber).IsRequired();
                entity.Property(tcs => tcs.Prefix).IsRequired().HasMaxLength(5).HasDefaultValue("789");
                entity.Property(tcs => tcs.UpdatedAt).IsRequired();
                entity.Ignore(tcs => tcs.Id);
                entity.Ignore(tcs => tcs.RegUserId);
                entity.Ignore(tcs => tcs.IsActive);
                entity.Ignore(tcs => tcs.RegDate);
                entity.Ignore(tcs => tcs.EditUserId);
                entity.Ignore(tcs => tcs.EditDate);
            });

            // ==== CARD PREFIX SEQUENCE ====
            modelBuilder.Entity<CardPrefixSequence>(entity =>
            {
                entity.ToTable("CardPrefixSequences");
                entity.HasKey(cps => cps.Id);
                entity.Property(cps => cps.Id).ValueGeneratedNever(); // Always 1
                entity.Property(cps => cps.LastPrefix).IsRequired();
            });

            // ==== POS SESSION ====
            modelBuilder.Entity<PosSession>(entity =>
            {
                entity.ToTable("PosSessions");
                entity.HasKey(ps => ps.Id);
                entity.Property(ps => ps.scannerDeviceId).IsRequired().HasMaxLength(200);
                entity.Property(ps => ps.SessionToken).HasMaxLength(500);
                entity.Property(ps => ps.Barcode).HasMaxLength(500);
                entity.HasIndex(ps => ps.SessionToken);
                
                entity.HasOne(ps => ps.Tenant)
                    .WithMany()
                    .HasForeignKey(ps => ps.TenantId)
                    .OnDelete(DeleteBehavior.Restrict);
                
                entity.HasOne(ps => ps.applicationUser)
                    .WithMany()
                    .HasForeignKey(ps => ps.userId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<Country>(entity =>
            {
                entity.ToTable("Countries");
                entity.HasKey(c => c.Id);
              
                
                // Multi-language support
                entity.Property(c => c.AzName).IsRequired().HasMaxLength(200);
                entity.Property(c => c.EnName).IsRequired().HasMaxLength(200);
                entity.Property(c => c.RuName).IsRequired().HasMaxLength(200);

                entity.HasMany(c => c.Cities)
                    .WithOne(city => city.Country)
                    .HasForeignKey(city => city.countryId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ==== CITY ====
            modelBuilder.Entity<City>(entity =>
            {
                entity.ToTable("Cities");
                entity.HasKey(c => c.Id);
              
                
                // Multi-language support
                entity.Property(c => c.AzName).IsRequired().HasMaxLength(200);
                entity.Property(c => c.EnName).IsRequired().HasMaxLength(200);
                entity.Property(c => c.RuName).IsRequired().HasMaxLength(200);

                entity.HasOne(c => c.Country)
                    .WithMany(country => country.Cities)
                    .HasForeignKey(c => c.countryId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ==== TIMEZONE ====
            modelBuilder.Entity<Entities.Main.TimeZone>(entity =>
            {
                entity.ToTable("TimeZones");
                entity.HasKey(tz => tz.Id);
                entity.Property(tz => tz.Name).IsRequired().HasMaxLength(200);
            });

            // ==== TENANT LOCATION ====
            modelBuilder.Entity<TenantLocation>(entity =>
            {
                entity.ToTable("TenantLocations");
                entity.HasKey(tl => tl.Id);

                entity.HasOne(tl => tl.Tenant)
                    .WithOne(t => t.TenantLocation)
                    .HasForeignKey<TenantLocation>(tl => tl.tenantId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(tl => tl.Country)
                    .WithMany()
                    .HasForeignKey(tl => tl.countryId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(tl => tl.City)
                    .WithMany()
                    .HasForeignKey(tl => tl.cityId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(tl => tl.TimeZone)
                    .WithMany()
                    .HasForeignKey(tl => tl.timeZoneId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ==== MENU CATEGORY ====
            modelBuilder.Entity<MenuCategory>(entity =>
            {
                entity.ToTable("MenuCategories");
                entity.HasKey(mc => mc.Id);
                entity.Property(mc => mc.AzName).IsRequired().HasMaxLength(200);
                entity.Property(mc => mc.EnName).IsRequired().HasMaxLength(200);
                entity.Property(mc => mc.RuName).IsRequired().HasMaxLength(200);
                entity.Property(mc => mc.AzDescription).HasMaxLength(1000);
                entity.Property(mc => mc.EnDescription).HasMaxLength(1000);
                entity.Property(mc => mc.RuDescription).HasMaxLength(1000);

                entity.HasOne(mc => mc.Tenant)
                    .WithMany()
                    .HasForeignKey(mc => mc.TenantId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(mc => mc.Branch)
                    .WithMany()
                    .HasForeignKey(mc => mc.BranchId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasIndex(mc => new { mc.TenantId, mc.BranchId });

                entity.HasMany(mc => mc.MenuItems)
                    .WithOne(mi => mi.MenuCategory)
                    .HasForeignKey(mi => mi.MenuCategoryId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ==== MENU ITEM ====
            modelBuilder.Entity<MenuItem>(entity =>
            {
                entity.ToTable("MenuItems");
                entity.HasKey(mi => mi.Id);
                entity.Property(mi => mi.Currency).IsRequired().HasMaxLength(10);
                entity.Property(mi => mi.AzName).IsRequired().HasMaxLength(200);
                entity.Property(mi => mi.EnName).IsRequired().HasMaxLength(200);
                entity.Property(mi => mi.RuName).IsRequired().HasMaxLength(200);
                entity.Property(mi => mi.AzDescription).HasMaxLength(1000);
                entity.Property(mi => mi.EnDescription).HasMaxLength(1000);
                entity.Property(mi => mi.RuDescription).HasMaxLength(1000);
                entity.Property(mi => mi.PrepTimeMinutes).HasMaxLength(50);
                entity.Property(mi => mi.CurrencySign).IsRequired().HasMaxLength(10);
                entity.Property(mi => mi.Price).IsRequired();
                entity.Property(mi => mi.DiscountPrice);

                entity.HasOne(mi => mi.MenuCategory)
                    .WithMany(mc => mc.MenuItems)
                    .HasForeignKey(mi => mi.MenuCategoryId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(mi => mi.Tenant)
                    .WithMany()
                    .HasForeignKey(mi => mi.TenantId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(mi => mi.Branch)
                    .WithMany()
                    .HasForeignKey(mi => mi.BranchId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasIndex(mi => new { mi.TenantId, mi.BranchId, mi.MenuCategoryId });

                entity.HasMany(mi => mi.MenuItemImages)
                    .WithOne(mii => mii.MenuItem)
                    .HasForeignKey(mii => mii.MenuItemId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ==== MENU ITEM IMAGE ====
            modelBuilder.Entity<MenuItemImage>(entity =>
            {
                entity.ToTable("MenuItemImages");
                entity.HasKey(mii => mii.Id);
                entity.Property(mii => mii.FileName).IsRequired().HasMaxLength(500);
                entity.Property(mii => mii.Path).IsRequired().HasMaxLength(1000);

                entity.HasOne(mii => mii.MenuItem)
                    .WithMany(mi => mi.MenuItemImages)
                    .HasForeignKey(mii => mii.MenuItemId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ==== RESTORANT WIFI INFORMATION ====
            modelBuilder.Entity<RestorantWifiInformation>(entity =>
            {
                entity.ToTable("RestorantWifiInformations");
                entity.HasKey(rwi => rwi.Id);
                entity.Property(rwi => rwi.Password).IsRequired().HasMaxLength(200);
                entity.Property(rwi => rwi.SSID).IsRequired().HasMaxLength(200);

                entity.HasOne(rwi => rwi.Tenant)
                    .WithMany(t => t.RestorantWifiInformation)
                    .HasForeignKey(rwi => rwi.TenantId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(rwi => rwi.Branch)
                    .WithMany()
                    .HasForeignKey(rwi => rwi.BranchId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasIndex(rwi => new { rwi.TenantId, rwi.BranchId });
            });

            modelBuilder.Entity<TenantLink>(entity =>
            {
                entity.ToTable("TenantLinks");
                entity.HasKey(tl => tl.Id);
                entity.Property(tl => tl.AzTitle).IsRequired().HasMaxLength(200);
                entity.Property(tl => tl.EnTitle).HasMaxLength(200);
                entity.Property(tl => tl.RuTitle).HasMaxLength(200);
                entity.Property(tl => tl.Url).IsRequired().HasMaxLength(1000);
                entity.Property(tl => tl.IconKey).HasMaxLength(50);

                entity.HasIndex(tl => new { tl.TenantId, tl.SortOrder });

                entity.HasOne(tl => tl.Tenant)
                    .WithMany(t => t.Links)
                    .HasForeignKey(tl => tl.TenantId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(tl => tl.Branch)
                    .WithMany()
                    .HasForeignKey(tl => tl.BranchId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasIndex(tl => new { tl.TenantId, tl.BranchId, tl.SortOrder });
            });

            // ==== INTEGRATION TYPE (reference/seed) ====
            modelBuilder.Entity<IntegrationType>(entity =>
            {
                entity.ToTable("IntegrationTypes");
                entity.HasKey(it => it.Id);
                entity.Property(it => it.Name).IsRequired().HasMaxLength(100);
                entity.Property(it => it.Source).IsRequired().HasMaxLength(50);
                entity.HasIndex(it => it.Source).IsUnique();
                entity.Property(it => it.DefaultBaseUrl).HasMaxLength(500);
                entity.Property(it => it.Description).HasMaxLength(500);

                entity.HasMany(it => it.TenantIntegrations)
                    .WithOne(ti => ti.IntegrationType)
                    .HasForeignKey(ti => ti.IntegrationTypeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ==== TENANT INTEGRATION ====
            modelBuilder.Entity<TenantIntegration>(entity =>
            {
                entity.ToTable("TenantIntegrations");
                entity.HasKey(ti => ti.Id);
                entity.Property(ti => ti.DisplayName).HasMaxLength(200);
                entity.Property(ti => ti.ExternalVenueId).HasMaxLength(100);
                entity.Property(ti => ti.ExternalProgramId).HasMaxLength(100);
                entity.Property(ti => ti.ExternalTerminalGroupId).HasMaxLength(100);
                entity.Property(ti => ti.ApiBaseUrl).HasMaxLength(500);
                entity.Property(ti => ti.ApiTokenEncrypted).HasMaxLength(2000);
                entity.Property(ti => ti.WalletTokenHash).HasMaxLength(128);
                entity.Property(ti => ti.WalletTokenEncrypted).HasMaxLength(2000);

                entity.HasIndex(ti => new { ti.TenantId, ti.IntegrationTypeId }).IsUnique();
                entity.HasIndex(ti => ti.WalletTokenHash);
                entity.HasIndex(ti => ti.ExternalTerminalGroupId);

                entity.HasOne(ti => ti.Tenant)
                    .WithMany(t => t.TenantIntegrations)
                    .HasForeignKey(ti => ti.TenantId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(ti => ti.IntegrationType)
                    .WithMany(it => it.TenantIntegrations)
                    .HasForeignKey(ti => ti.IntegrationTypeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(ti => ti.LoyaltyProgram)
                    .WithMany()
                    .HasForeignKey(ti => ti.LoyaltyProgramId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ==== POS PRODUCT EARN RULE ====
            modelBuilder.Entity<PosProductEarnRule>(entity =>
            {
                entity.ToTable("PosProductEarnRules");
                entity.HasKey(pr => pr.Id);
                entity.Property(pr => pr.ExternalProductId).IsRequired().HasMaxLength(100);
                entity.Property(pr => pr.ProductName).HasMaxLength(200);

                entity.HasOne(pr => pr.TenantIntegration)
                    .WithMany()
                    .HasForeignKey(pr => pr.TenantIntegrationId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ==== POS LOYALTY DISCOUNT TYPE ====
            modelBuilder.Entity<PosLoyaltyDiscountType>(entity =>
            {
                entity.ToTable("PosLoyaltyDiscountTypes");
                entity.HasKey(pd => pd.Id);
                entity.Property(pd => pd.ExternalDiscountTypeId).IsRequired().HasMaxLength(100);
                entity.Property(pd => pd.DiscountName).HasMaxLength(200);
                entity.HasIndex(pd => new { pd.TenantIntegrationId, pd.ExternalDiscountTypeId }).IsUnique();

                entity.HasOne(pd => pd.TenantIntegration)
                    .WithMany()
                    .HasForeignKey(pd => pd.TenantIntegrationId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ==== POS CUSTOMER MAPPING ====
            modelBuilder.Entity<PosCustomerMapping>(entity =>
            {
                entity.ToTable("PosCustomerMappings");
                entity.HasKey(pcm => pcm.Id);
                entity.Property(pcm => pcm.ExternalCustomerId).IsRequired().HasMaxLength(100);
                entity.HasIndex(pcm => new { pcm.TenantIntegrationId, pcm.ExternalCustomerId }).IsUnique();

                entity.HasOne(pcm => pcm.TenantIntegration)
                    .WithMany()
                    .HasForeignKey(pcm => pcm.TenantIntegrationId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(pcm => pcm.Customer)
                    .WithMany()
                    .HasForeignKey(pcm => pcm.CustomerId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ==== BRANCH ====
            modelBuilder.Entity<Branch>(entity =>
            {
                entity.ToTable("Branches");
                entity.HasKey(b => b.Id);
                entity.Property(b => b.Name).IsRequired().HasMaxLength(200);
                entity.Property(b => b.Address).HasMaxLength(500);
                entity.Property(b => b.Phone).HasMaxLength(50);
                entity.Property(b => b.Email).HasMaxLength(200);
                entity.Property(b => b.PhotoUrl).HasMaxLength(500);
                entity.Property(b => b.LogoUrl).HasMaxLength(500);
                entity.Property(b => b.Latitude).HasColumnType("decimal(9,6)");
                entity.Property(b => b.Longitude).HasColumnType("decimal(9,6)");
                entity.Property(b => b.RadiusMeters).HasDefaultValue(100);
                entity.Property(b => b.IsMainBranch).HasDefaultValue(false);

                entity.Property(b => b.LocationUrl).HasMaxLength(500);
                entity.Property(b => b.WazeLocationUrl).HasMaxLength(500);
                entity.Property(b => b.MenuUrl).HasMaxLength(500);
                entity.Property(b => b.InstagramUrl).HasMaxLength(500);
                entity.Property(b => b.FacebookUrl).HasMaxLength(500);
                entity.Property(b => b.WhatsAppUrl).HasMaxLength(500);
                entity.Property(b => b.TelegramUrl).HasMaxLength(500);
                entity.Property(b => b.LinkedInUrl).HasMaxLength(500);

                entity.HasIndex(b => b.TenantId);
                entity.HasIndex(b => new { b.TenantId, b.IsActive });

                entity.HasOne(b => b.Tenant)
                    .WithMany(t => t.Branches)
                    .HasForeignKey(b => b.TenantId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ==== ROOM ====
            modelBuilder.Entity<Room>(entity =>
            {
                entity.ToTable("Rooms");
                entity.HasKey(r => r.Id);
                entity.Property(r => r.Name).IsRequired().HasMaxLength(200);
                entity.Property(r => r.Type).HasMaxLength(100);
                entity.Property(r => r.Description).HasMaxLength(2000);
                entity.Property(r => r.OrderIndex).HasDefaultValue(0);
                entity.Property(r => r.MinDeposit).HasColumnType("decimal(18,2)");

                entity.HasIndex(r => r.BranchId);
                entity.HasIndex(r => new { r.BranchId, r.IsActive });

                entity.HasOne(r => r.Branch)
                    .WithMany(b => b.Rooms)
                    .HasForeignKey(r => r.BranchId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ==== ROOM FILE ====
            modelBuilder.Entity<RoomFile>(entity =>
            {
                entity.ToTable("RoomFiles");
                entity.HasKey(rf => rf.Id);
                entity.Property(rf => rf.FileName).IsRequired().HasMaxLength(500);
                entity.Property(rf => rf.Path).IsRequired().HasMaxLength(1000);
                entity.Property(rf => rf.MediaType).IsRequired().HasMaxLength(20).HasDefaultValue("image");
                entity.Property(rf => rf.ThumbnailPath).HasMaxLength(1000);
                entity.Property(rf => rf.OrderIndex).HasDefaultValue(0);

                entity.HasIndex(rf => rf.RoomId);
                entity.HasIndex(rf => new { rf.RoomId, rf.MediaType });

                entity.HasOne(rf => rf.Room)
                    .WithMany(r => r.Files)
                    .HasForeignKey(rf => rf.RoomId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // Process EntityBase timestamps first
            var entries = ChangeTracker.Entries<EntityBase>();

            foreach (var entry in entries)
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.RegDate = DateTime.UtcNow;
                    if (entry.Entity.RegUserId == 0)
                    {
                        entry.Entity.RegUserId = 1; // Default user ID
                    }
                }
                else if (entry.State == EntityState.Modified)
                {
                    entry.Entity.EditDate = DateTime.UtcNow;
                    if (entry.Entity.EditUserId == null || entry.Entity.EditUserId == 0)
                    {
                        entry.Entity.EditUserId = 1; // Default user ID
                    }
                }
            }

            // NOTE: WalveroId and CardNumber generation has been moved to services (ICardNumberGenerator, IWalveroIdGenerator)
            // They are generated before SaveChanges is called to avoid N+1 queries and improve performance

            // Generate CardNumber and BarcodeValue for new LoyaltyCards
            // This runs after Customer.CardNumber is generated
            var newLoyaltyCards = ChangeTracker.Entries<LoyaltyCard>()
                .Where(e => e.State == EntityState.Added)
                .Select(e => e.Entity)
                .ToList();

            foreach (var loyaltyCard in newLoyaltyCards)
            {
                // Always ensure CardNumber is set - never leave it empty or whitespace
                // Check both IsNullOrEmpty and IsNullOrWhiteSpace to be safe
                if (string.IsNullOrEmpty(loyaltyCard.CardNumber) || string.IsNullOrWhiteSpace(loyaltyCard.CardNumber))
                {
                    // Load the customer if not already loaded
                    if (loyaltyCard.Customer == null && loyaltyCard.CustomerId > 0)
                    {
                        await Entry(loyaltyCard)
                            .Reference(lc => lc.Customer)
                            .LoadAsync(cancellationToken);
                    }

                    // Generate CardNumber from Customer's CardNumber or create unique one
                    if (loyaltyCard.Customer != null && !string.IsNullOrEmpty(loyaltyCard.Customer.CardNumber) && !string.IsNullOrWhiteSpace(loyaltyCard.Customer.CardNumber))
                    {
                        var customerCardNumber = loyaltyCard.Customer.CardNumber.Trim();
                        
                        // Check if this CardNumber already exists in local tracking
                        var existingCardNumber = LoyaltyCards
                            .Local
                            .FirstOrDefault(lc => !string.IsNullOrEmpty(lc.CardNumber) && 
                                                 !string.IsNullOrWhiteSpace(lc.CardNumber) &&
                                                 lc.CardNumber.Trim() == customerCardNumber && 
                                                 lc.Id != loyaltyCard.Id && 
                                                 lc.Id != 0);
                        
                        if (existingCardNumber == null)
                        {
                            // Use Customer.CardNumber as CardNumber (truncate if too long)
                            loyaltyCard.CardNumber = customerCardNumber.Length > 50 
                                ? customerCardNumber.Substring(0, 50) 
                                : customerCardNumber;
                        }
                        else
                        {
                            // Generate unique CardNumber with suffix
                            var suffix = 1;
                            string uniqueCardNumber;
                            do
                            {
                                uniqueCardNumber = $"{customerCardNumber}-{suffix}";
                                if (uniqueCardNumber.Length > 50)
                                {
                                    var maxBaseLength = 50 - suffix.ToString().Length - 1;
                                    uniqueCardNumber = $"{customerCardNumber.Substring(0, Math.Min(maxBaseLength, customerCardNumber.Length))}-{suffix}";
                                }
                                suffix++;
                            } while (suffix < 1000 && LoyaltyCards.Local.Any(lc => !string.IsNullOrEmpty(lc.CardNumber) && 
                                                                                    !string.IsNullOrWhiteSpace(lc.CardNumber) &&
                                                                                    lc.CardNumber.Trim() == uniqueCardNumber && 
                                                                                    lc.Id != loyaltyCard.Id && 
                                                                                    lc.Id != 0));
                            
                            loyaltyCard.CardNumber = uniqueCardNumber;
                        }
                    }
                    else
                    {
                        // If Customer or CardNumber is null, generate a unique CardNumber
                        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                        var guid = Guid.NewGuid().ToString("N").Substring(0, 8);
                        loyaltyCard.CardNumber = $"LC-{timestamp}-{guid}";
                    }
                    
                    // Final safety check - ensure CardNumber is never empty
                    if (string.IsNullOrEmpty(loyaltyCard.CardNumber) || string.IsNullOrWhiteSpace(loyaltyCard.CardNumber))
                    {
                        // Last resort - generate a unique CardNumber
                        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                        var guid = Guid.NewGuid().ToString("N").Substring(0, 8);
                        loyaltyCard.CardNumber = $"LC-{timestamp}-{guid}";
                    }
                }
                
                // Set BarcodeValue from Customer's CardNumber if empty
                if (string.IsNullOrEmpty(loyaltyCard.BarcodeValue) || string.IsNullOrWhiteSpace(loyaltyCard.BarcodeValue))
                {
                    // Load the customer if not already loaded
                    if (loyaltyCard.Customer == null && loyaltyCard.CustomerId > 0)
                    {
                        await Entry(loyaltyCard)
                            .Reference(lc => lc.Customer)
                            .LoadAsync(cancellationToken);
                    }
                    
                    if (loyaltyCard.Customer != null && !string.IsNullOrEmpty(loyaltyCard.Customer.CardNumber) && !string.IsNullOrWhiteSpace(loyaltyCard.Customer.CardNumber))
                    {
                        loyaltyCard.BarcodeValue = loyaltyCard.Customer.CardNumber.Trim();
                    }
                }
            }


            return await base.SaveChangesAsync(cancellationToken);
        }

        public override int SaveChanges()
        {
            // Process EntityBase timestamps
            var entries = ChangeTracker.Entries<EntityBase>();
            foreach (var entry in entries)
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.RegDate = DateTime.UtcNow;
                    if (entry.Entity.RegUserId == 0)
                    {
                        entry.Entity.RegUserId = 1; // Default user ID
                    }
                }
                else if (entry.State == EntityState.Modified)
                {
                    entry.Entity.EditDate = DateTime.UtcNow;
                    if (entry.Entity.EditUserId == null || entry.Entity.EditUserId == 0)
                    {
                        entry.Entity.EditUserId = 1; // Default user ID
                    }
                }
            }

            // NOTE: WalveroId and CardNumber generation has been moved to services (ICardNumberGenerator, IWalveroIdGenerator)
            // They are generated before SaveChanges is called to avoid N+1 queries and improve performance

            // Generate CardNumber and BarcodeValue for new LoyaltyCards
            var newLoyaltyCards = ChangeTracker.Entries<LoyaltyCard>()
                .Where(e => e.State == EntityState.Added)
                .Select(e => e.Entity)
                .ToList();

            foreach (var loyaltyCard in newLoyaltyCards)
            {
                // Always ensure CardNumber is set - never leave it empty or whitespace
                if (string.IsNullOrEmpty(loyaltyCard.CardNumber) || string.IsNullOrWhiteSpace(loyaltyCard.CardNumber))
                {
                    // Load the customer if not already loaded
                    if (loyaltyCard.Customer == null && loyaltyCard.CustomerId > 0)
                    {
                        Entry(loyaltyCard)
                            .Reference(lc => lc.Customer)
                            .Load();
                    }

                    // Generate CardNumber from Customer's CardNumber or create unique one
                    if (loyaltyCard.Customer != null && !string.IsNullOrEmpty(loyaltyCard.Customer.CardNumber) && !string.IsNullOrWhiteSpace(loyaltyCard.Customer.CardNumber))
                    {
                        var customerCardNumber = loyaltyCard.Customer.CardNumber.Trim();
                        
                        // Check if this CardNumber already exists in local tracking
                        var existingCardNumber = LoyaltyCards
                            .Local
                            .FirstOrDefault(lc => !string.IsNullOrEmpty(lc.CardNumber) && 
                                                 !string.IsNullOrWhiteSpace(lc.CardNumber) &&
                                                 lc.CardNumber.Trim() == customerCardNumber && 
                                                 lc.Id != loyaltyCard.Id && 
                                                 lc.Id != 0);
                        
                        if (existingCardNumber == null)
                        {
                            // Use Customer.CardNumber as CardNumber (truncate if too long)
                            loyaltyCard.CardNumber = customerCardNumber.Length > 50 
                                ? customerCardNumber.Substring(0, 50) 
                                : customerCardNumber;
                        }
                        else
                        {
                            // Generate unique CardNumber with suffix
                            var suffix = 1;
                            string uniqueCardNumber;
                            do
                            {
                                uniqueCardNumber = $"{customerCardNumber}-{suffix}";
                                if (uniqueCardNumber.Length > 50)
                                {
                                    var maxBaseLength = 50 - suffix.ToString().Length - 1;
                                    uniqueCardNumber = $"{customerCardNumber.Substring(0, Math.Min(maxBaseLength, customerCardNumber.Length))}-{suffix}";
                                }
                                suffix++;
                            } while (suffix < 1000 && LoyaltyCards.Local.Any(lc => !string.IsNullOrEmpty(lc.CardNumber) && 
                                                                                    !string.IsNullOrWhiteSpace(lc.CardNumber) &&
                                                                                    lc.CardNumber.Trim() == uniqueCardNumber && 
                                                                                    lc.Id != loyaltyCard.Id && 
                                                                                    lc.Id != 0));
                            
                            loyaltyCard.CardNumber = uniqueCardNumber;
                        }
                    }
                    else
                    {
                        // If Customer or CardNumber is null, generate a unique CardNumber
                        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                        var guid = Guid.NewGuid().ToString("N").Substring(0, 8);
                        loyaltyCard.CardNumber = $"LC-{timestamp}-{guid}";
                    }
                    
                    // Final safety check - ensure CardNumber is never empty
                    if (string.IsNullOrEmpty(loyaltyCard.CardNumber) || string.IsNullOrWhiteSpace(loyaltyCard.CardNumber))
                    {
                        // Last resort - generate a unique CardNumber
                        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                        var guid = Guid.NewGuid().ToString("N").Substring(0, 8);
                        loyaltyCard.CardNumber = $"LC-{timestamp}-{guid}";
                    }
                }
                
                // Set BarcodeValue from Customer's CardNumber if empty
                if (string.IsNullOrEmpty(loyaltyCard.BarcodeValue) || string.IsNullOrWhiteSpace(loyaltyCard.BarcodeValue))
                {
                    // Load the customer if not already loaded
                    if (loyaltyCard.Customer == null && loyaltyCard.CustomerId > 0)
                    {
                        Entry(loyaltyCard)
                            .Reference(lc => lc.Customer)
                            .Load();
                    }
                    
                    if (loyaltyCard.Customer != null && !string.IsNullOrEmpty(loyaltyCard.Customer.CardNumber) && !string.IsNullOrWhiteSpace(loyaltyCard.Customer.CardNumber))
                    {
                        loyaltyCard.BarcodeValue = loyaltyCard.Customer.CardNumber.Trim();
                    }
                }
            }

            return base.SaveChanges();
        }

        // OLD GENERATION METHODS REMOVED - Now using ICardNumberGenerator and IWalveroIdGenerator services
        // These methods scanned Customers table and were slow (O(N)) and not concurrency-safe
        // New implementation uses atomic SQL Server UPDATE...OUTPUT in SequenceRepository
        //
        // Removed methods:
        // - GenerateWalveroIdSync(int countryCode)
        // - GenerateCardNumberSync(int tenantId)  
        // - GenerateWalveroIdAsync(int countryCode, CancellationToken)
        // - GenerateCardNumberAsync(int tenantId, CancellationToken)
        // - ExtractCountryCode(string phoneNumber) - use IWalveroIdGenerator.ExtractCountryCode instead

        /// <summary>
        /// Extracts numeric part from barcode string (e.g., "78917890001" -> 7890001)
        /// Extracts digits from the end of the string
        /// </summary>
        private long? ExtractNumericPartFromBarcode(string? barcode)
        {
            if (string.IsNullOrEmpty(barcode))
                return null;

            // Extract all digits from the end of the string
            var digits = new System.Text.StringBuilder();
            for (int i = barcode.Length - 1; i >= 0; i--)
            {
                if (char.IsDigit(barcode[i]))
                {
                    digits.Insert(0, barcode[i]);
                }
                else
                {
                    break; // Stop when we hit a non-digit
                }
            }

            if (digits.Length > 0 && long.TryParse(digits.ToString(), out var numeric))
            {
                return numeric;
            }

            return null;
        }
        //protected override void OnModelCreating(ModelBuilder modelBuilder)
        //{
        //    base.OnModelCreating(modelBuilder);
        //}






    }
}
