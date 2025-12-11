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
        public DbSet<DeviceToken> DeviceTokens { get; set; }
        public DbSet<RedeemRequest> RedeemRequests { get; set; }
        public DbSet<PassDeviceRegistration> PassDeviceRegistrations { get; set; }
        public DbSet<TenantBranding> TenantBrandings { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<CardTemplate> CardTemplates { get; set; }
        public DbSet<LoyaltyProgram> LoyaltyPrograms { get; set; }
        public DbSet<LoyaltyCard> LoyaltyCards { get; set; }
        public DbSet<WalletPass> WalletPasses { get; set; }
        public DbSet<WalletCredential> WalletCredentials { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<Reward> Rewards { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<AppVersionConfig> AppVersionConfigs { get; set; }
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
        public DbSet<CardPrefixSequence> CardPrefixSequences { get; set; }
        #endregion
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Tenant>(entity =>
            {
                entity.ToTable("Tenants");
                entity.HasKey(t => t.Id);
                entity.Property(t => t.CompanyName).IsRequired().HasMaxLength(200);
                entity.Property(t => t.Subdomain).IsRequired().HasMaxLength(100);
                entity.HasIndex(t => t.Subdomain).IsUnique();
                entity.Property(t => t.ContactEmail).IsRequired().HasMaxLength(200);
                entity.Property(t => t.ContactPhone).IsRequired().HasMaxLength(50);

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

                entity.HasMany(c => c.LoyaltyCards)
                    .WithOne(lc => lc.Customer)
                    .HasForeignKey(lc => lc.CustomerId)
                    .OnDelete(DeleteBehavior.Cascade); // Changed to Cascade - delete cards when customer is deleted

                entity.HasOne(wc => wc.applicationUser)
                   .WithMany(t => t.Customers)
                   .HasForeignKey(wc => wc.applicationUserId)
                   .OnDelete(DeleteBehavior.Cascade);


            });
            modelBuilder.Entity<DeviceToken>()
            .HasOne(dt => dt.User)
            .WithMany(u => u.DeviceTokens)
            .HasForeignKey(dt => dt.UserId)
            .OnDelete(DeleteBehavior.Cascade); // optional
            modelBuilder.Entity<CardTemplate>(entity =>
            {
                entity.ToTable("CardTemplates");
                entity.HasKey(ct => ct.Id);
                entity.Property(ct => ct.TemplateName).IsRequired().HasMaxLength(200);
                entity.Property(ct => ct.TemplateType).HasConversion<int>();
            
                entity.Property(ct => ct.StepLabel).HasMaxLength(50);
                entity.Property(ct => ct.RewardDescription).HasMaxLength(500);
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

                entity.HasMany(lc => lc.WalletPasses)
                    .WithOne(wp => wp.LoyaltyCard)
                    .HasForeignKey(wp => wp.LoyaltyCardId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(lc => lc.Transactions)
                    .WithOne(t => t.LoyaltyCard)
                    .HasForeignKey(t => t.LoyaltyCardId)
                    .OnDelete(DeleteBehavior.Restrict);

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
                entity.Property(t => t.ReferenceNumber).HasMaxLength(100);

                entity.HasIndex(t => t.RegDate);
                entity.HasIndex(t => t.LoyaltyCardId);

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
                entity.Ignore(cs => cs.Id);
                entity.Ignore(cs => cs.RegUserId);
                entity.Ignore(cs => cs.IsActive);
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
                }

                if (entry.State == EntityState.Modified)
                {
                    entry.Entity.EditDate = DateTime.UtcNow;
                }
            }

            // Generate WalveroId for new ApplicationUsers
            var newUsers = ChangeTracker.Entries<ApplicationUser>()
                .Where(e => e.State == EntityState.Added && e.Entity.WalveroId == null)
                .Select(e => e.Entity)
                .ToList();

            foreach (var user in newUsers)
            {
                if (string.IsNullOrEmpty(user.PhoneNumber))
                    continue; // Skip if no phone number

                var countryCode = ExtractCountryCode(user.PhoneNumber);
                if (countryCode.HasValue)
                {
                    user.WalveroId = await GenerateWalveroIdAsync(countryCode.Value, cancellationToken);
                }
            }

            // Generate CardNumber for new Customers
            var newCustomers = ChangeTracker.Entries<Customer>()
                .Where(e => e.State == EntityState.Added && string.IsNullOrEmpty(e.Entity.CardNumber))
                .Select(e => e.Entity)
                .ToList();

            foreach (var customer in newCustomers)
            {
                customer.CardNumber = await GenerateCardNumberAsync(customer.TenantId, cancellationToken);
            }

            // Set BarcodeValue for new LoyaltyCards from Customer's CardNumber
            var newLoyaltyCards = ChangeTracker.Entries<LoyaltyCard>()
                .Where(e => e.State == EntityState.Added && string.IsNullOrEmpty(e.Entity.BarcodeValue))
                .Select(e => e.Entity)
                .ToList();

            foreach (var loyaltyCard in newLoyaltyCards)
            {
                // Load the customer if not already loaded
                if (loyaltyCard.Customer == null && loyaltyCard.CustomerId > 0)
                {
                    await Entry(loyaltyCard)
                        .Reference(lc => lc.Customer)
                        .LoadAsync(cancellationToken);
                }

                // Set BarcodeValue from Customer's CardNumber
                if (loyaltyCard.Customer != null && !string.IsNullOrEmpty(loyaltyCard.Customer.CardNumber))
                {
                    loyaltyCard.BarcodeValue = loyaltyCard.Customer.CardNumber;
                }
            }

            return await base.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Extracts country code from phone number (supports +994, 994, 0 prefixes)
        /// </summary>
        private int? ExtractCountryCode(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                return null;

            // Remove all non-digit characters
            var digits = Regex.Replace(phoneNumber, @"[^\d]", "");
            
            if (string.IsNullOrEmpty(digits))
                return null;

            // Try to extract country code
            // Common formats: +994501234567, 994501234567, 0501234567
            
            // If starts with country code (3 digits: 994, 336, etc.)
            if (digits.Length >= 3)
            {
                var possibleCode = int.Parse(digits.Substring(0, 3));
                
                // Common country codes (you can extend this list)
                if (possibleCode >= 100 && possibleCode <= 999)
                {
                    // Check if it's a valid country code length
                    // For Azerbaijan (994), if next digit is 0, it's likely country code
                    // For France (336), similar logic
                    if (digits.Length > 3)
                    {
                        return possibleCode;
                    }
                }
            }

            // If starts with 0 (local format), try to infer from length
            // This is a fallback - you may want to add a CountryCode field to ApplicationUser
            if (digits.StartsWith("0") && digits.Length >= 9)
            {
                // Default to 994 (Azerbaijan) if local format - adjust based on your needs
                // TODO: Consider adding CountryCode field to ApplicationUser for explicit country
                return 994; // Default country code - you should make this configurable
            }

            return null;
        }

        /// <summary>
        /// Generates WalveroId for a user based on country code
        /// Pattern: {countryCode}{1}{seq:00000}
        /// Example: 994100001, 994100002, ...
        /// </summary>
        private async Task<long> GenerateWalveroIdAsync(int countryCode, CancellationToken cancellationToken)
        {
            // Use database transaction to ensure atomicity
            var sequence = await CountrySequences
                .FirstOrDefaultAsync(cs => cs.CountryCode == countryCode, cancellationToken);

            if (sequence == null)
            {
                sequence = new CountrySequence
                {
                    CountryCode = countryCode,
                    LastNumber = 0
                };
                await CountrySequences.AddAsync(sequence, cancellationToken);
            }

            sequence.LastNumber++;
            var walveroId = long.Parse($"{countryCode}1{sequence.LastNumber:D5}");

            return walveroId;
        }

        /// <summary>
        /// Generates CardNumber for a customer
        /// Pattern: {prefix}{tenantId}{prefix}{seq:0000}
        /// Example for tenant 1: "7891 7890001", "7891 7890002"
        /// Example for tenant 2: "7892 7890001", "7892 7890002" (if prefix hasn't changed)
        /// </summary>
        private async Task<string> GenerateCardNumberAsync(int tenantId, CancellationToken cancellationToken)
        {
            // Get or create tenant sequence
            var tenantSequence = await TenantCustomerSequences
                .FirstOrDefaultAsync(tcs => tcs.TenantId == tenantId, cancellationToken);

            if (tenantSequence == null)
            {
                tenantSequence = new TenantCustomerSequence
                {
                    TenantId = tenantId,
                    LastNumber = 0
                };
                await TenantCustomerSequences.AddAsync(tenantSequence, cancellationToken);
            }

            // Get or create global prefix sequence
            var prefixSequence = await CardPrefixSequences
                .FirstOrDefaultAsync(cps => cps.Id == 1, cancellationToken);

            if (prefixSequence == null)
            {
                prefixSequence = new CardPrefixSequence
                {
                    Id = 1,
                    LastPrefix = 789 // Starting prefix
                };
                await CardPrefixSequences.AddAsync(prefixSequence, cancellationToken);
            }

            // Increment tenant sequence
            tenantSequence.LastNumber++;

            // Check if we need to increment prefix (every 10000 customers per tenant, or your business logic)
            // For now, we'll increment prefix when tenant sequence wraps around
            // You can adjust this logic based on your requirements
            if (tenantSequence.LastNumber > 9999)
            {
                prefixSequence.LastPrefix++;
                tenantSequence.LastNumber = 1; // Reset to 1 for new prefix
            }

            var prefix = prefixSequence.LastPrefix;
            var seq = tenantSequence.LastNumber;

            // Format: {prefix}{tenantId}{prefix}{seq:0000}
            // Example: 78917890001, 78917890002, ...
            var cardNumber = $"{prefix}{tenantId}{prefix}{seq:D4}";

            return cardNumber;
        }
        //protected override void OnModelCreating(ModelBuilder modelBuilder)
        //{
        //    base.OnModelCreating(modelBuilder);
        //}






    }
}
