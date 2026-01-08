# Migration Skriptini Çalışdırma Təlimatları

## 📋 SQL Server-də Migration Skriptini Çalışdırma

### Metod 1: SQL Server Management Studio (SSMS)

1. **SSMS-i açın** və verilənlər bazasına qoşulun
2. **Yeni Query** pəncərəsi açın (Ctrl+N)
3. **Migration skriptini açın:**
   ```
   Loyalty.DataAccess/Migrations/20251219140448_optimize_sequence_generation.sql
   ```
4. **Skripti seçin və Execute** edin (F5)

### Metod 2: Azure Data Studio / VS Code

1. **Azure Data Studio** və ya **VS Code**-u açın
2. **SQL Server connection** yaradın
3. **Migration skriptini açın** və **Run** edin

### Metod 3: Command Line (sqlcmd)

```powershell
# Windows PowerShell
sqlcmd -S "ServerName" -d "DatabaseName" -U "Username" -P "Password" -i "Loyalty.DataAccess\Migrations\20251219140448_optimize_sequence_generation.sql"
```

**Nümunə:**
```powershell
sqlcmd -S "localhost" -d "LoyaltyWalletDb" -U "sa" -P "YourPassword" -i "Loyalty.DataAccess\Migrations\20251219140448_optimize_sequence_generation.sql"
```

### Metod 4: .NET EF Core Migration (Tövsiyə olunur)

Əgər EF Core migration istifadə edirsinizsə:

```powershell
# Migration yaradın
dotnet ef migrations add OptimizeSequenceGeneration --project Loyalty.DataAccess --startup-project Loyalty.WebAPI

# Migration-ı tətbiq edin
dotnet ef database update --project Loyalty.DataAccess --startup-project Loyalty.WebAPI
```

---

## ✅ Skript Nə Edir?

1. **TenantCustomerSequences** cədvəlinə `Prefix` column əlavə edir (əgər yoxdursa)
2. **TenantCustomerSequences** cədvəlinə `UpdatedAt` column əlavə edir (əgər yoxdursa)
3. **CountrySequences** cədvəlinə `UpdatedAt` column əlavə edir (əgər yoxdursa)
4. **Customers.CardNumber** üçün unique index yaradır (filtered: `WHERE CardNumber IS NOT NULL`)
5. **AspNetUsers.WalveroId** üçün unique index yaradır (filtered: `WHERE WalveroId IS NOT NULL`)
6. Mövcud `TenantCustomerSequences` qeydlərində `Prefix` dəyərlərini initialize edir

---

## ⚠️ Əhəmiyyətli Qeydlər

### 1. Skript Idempotentdir
- Skript **təhlükəsizdir** - bir neçə dəfə çalışdıra bilərsiniz
- `IF NOT EXISTS` yoxlamaları istifadə olunur
- Artıq mövcud olan column və index-lər **yenidən yaradılmayacaq**

### 2. Verilənlər Bazası Backup
**Mühüm:** Production mühitində skripti çalışdırmazdan əvvəl **backup** alın!

```sql
-- Backup nümunəsi
BACKUP DATABASE [LoyaltyWalletDb] 
TO DISK = 'C:\Backups\LoyaltyWalletDb_BeforeSequenceOptimization.bak'
WITH FORMAT, COMPRESSION;
```

### 3. Skript Çalışdıqdan Sonra Yoxlama

```sql
-- Column-ların mövcudluğunu yoxlayın
SELECT 
    TABLE_NAME,
    COLUMN_NAME,
    DATA_TYPE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('TenantCustomerSequences', 'CountrySequences')
AND COLUMN_NAME IN ('Prefix', 'UpdatedAt');

-- Index-lərin mövcudluğunu yoxlayın
SELECT 
    i.name AS IndexName,
    t.name AS TableName
FROM sys.indexes i
INNER JOIN sys.tables t ON i.object_id = t.object_id
WHERE i.name IN ('IX_Customers_CardNumber', 'IX_AspNetUsers_WalveroId');
```

---

## 🔍 Skript Çalışdıqdan Sonra Test

### 1. Yeni Customer Yaradın
```csharp
// CustomerEnrollmentService-də yeni customer yaradın
// CardNumber avtomatik generate olunmalıdır
```

### 2. Yeni ApplicationUser Yaradın
```csharp
// UserService-də yeni user yaradın
// WalveroId avtomatik generate olunmalıdır
```

### 3. Concurrent Request-ləri Test Edin
- Eyni anda bir neçə customer/user yaradın
- Duplicate error olmamalıdır
- Hər biri unikal CardNumber/WalveroId almalıdır

---

## ❌ Problem Həlləri

### Problem 1: "Column already exists" xətası
**Həll:** Normaldır - skript idempotentdir, xəta verməyəcək.

### Problem 2: "Index already exists" xətası
**Həll:** Normaldır - skript idempotentdir, xəta verməyəcək.

### Problem 3: "Table does not exist" xətası
**Həll:** Əvvəlcə əsas migration-ları çalışdırın:
```powershell
dotnet ef database update --project Loyalty.DataAccess --startup-project Loyalty.WebAPI
```

### Problem 4: Permission xətası
**Həll:** Verilənlər bazası üçün **ALTER TABLE** və **CREATE INDEX** icazələri lazımdır.

---

## 📞 Dəstək

Əgər problem yaşayırsınızsa:
1. Skriptin çıxışını yoxlayın (PRINT mesajları)
2. SQL Server error log-larını yoxlayın
3. Verilənlər bazası connection string-ini yoxlayın

---

## ✅ Uğurlu Çalışdırma Nümunəsi

```
Added Prefix column to TenantCustomerSequences
Added UpdatedAt column to TenantCustomerSequences
Added UpdatedAt column to CountrySequences
Created unique index IX_Customers_CardNumber
Created unique index IX_AspNetUsers_WalveroId
Initialized Prefix values in TenantCustomerSequences
Migration completed successfully!
Sequence generation is now optimized with atomic SQL Server UPDATE...OUTPUT
```

