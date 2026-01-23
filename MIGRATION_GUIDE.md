# Migration Təlimatı - Multi-Language Location Support

## ⚠️ PROBLEM: Invalid column name 'Name'

### Səbəb
Database-də hələ köhnə struktur mövcuddur. Yeni sütunlar (`AzName`, `EnName`, `RuName`) yaradılmayıb.

### ✅ Həll: Migration Tətbiq Et

---

## 🔧 Migration Tətbiqi (Addım-addım)

### Addım 1: Application-u dayandır

**Visual Studio:**
- Stop Debugging (Shift+F5)

**Terminal:**
- Ctrl+C (əgər `dotnet run` ilə işlədilirdisə)

---

### Addım 2: Migration-ları yoxla

```bash
cd Loyalty.DataAccess
dotnet ef migrations list --project . --startup-project ../Loyalty.WebAPI
```

**Gözlənilən output:**
```
20260108114627_posservicebarcode
20260123100000_TenantSearchOptimizationIndexes
20260123120000_AddMultiLanguageSupportToLocations (Pending)
```

`(Pending)` - hələ tətbiq edilməyib

---

### Addım 3: Migration tətbiq et

#### Variant 1: EF Core CLI (Tövsiyə edilir)

```bash
cd Loyalty.DataAccess
dotnet ef database update --project . --startup-project ../Loyalty.WebAPI
```

#### Variant 2: Package Manager Console (Visual Studio)

```powershell
Update-Database -Project Loyalty.DataAccess -StartupProject Loyalty.WebAPI
```

---

### Addım 4: Migration uğurlu oldu mu?

**Uğurlu output:**
```
Build succeeded.
Applying migration '20260123120000_AddMultiLanguageSupportToLocations'.
Done.
```

**Xəta varsa:**
```
❌ Connection refused -> Əlaqə:
❌ Login failed -> SQL Server açıqdır?
❌ Database does not exist -> Database yaradın
```

---

## 📊 Migration-un nə edəcəyi

### 1. Yeni Sütunlar Əlavə Edir

**Countries table:**
```sql
ALTER TABLE Countries ADD AzName nvarchar(200) NOT NULL DEFAULT '';
ALTER TABLE Countries ADD EnName nvarchar(200) NOT NULL DEFAULT '';
ALTER TABLE Countries ADD RuName nvarchar(200) NOT NULL DEFAULT '';
```

**Cities table:**
```sql
ALTER TABLE Cities ADD AzName nvarchar(200) NOT NULL DEFAULT '';
ALTER TABLE Cities ADD EnName nvarchar(200) NOT NULL DEFAULT '';
ALTER TABLE Cities ADD RuName nvarchar(200) NOT NULL DEFAULT '';
```

### 2. Mövcud Data-nı Köçürür

```sql
UPDATE Countries 
SET AzName = Name, EnName = Name, RuName = Name 
WHERE AzName = '' OR AzName IS NULL;

UPDATE Cities 
SET AzName = Name, EnName = Name, RuName = Name 
WHERE AzName = '' OR AzName IS NULL;
```

**⚠️ Əhəmiyyətli:** Köhnə `Name` sütunu saxlanılır (backward compatibility)

### 3. İndexlər Yaradır

```sql
CREATE INDEX IX_Countries_AzName ON Countries (AzName);
CREATE INDEX IX_Cities_AzName ON Cities (AzName);
CREATE INDEX IX_Cities_countryId_AzName ON Cities (countryId, AzName);
```

**Fayda:** Query performansı ~60-70% artır

---

## 🚀 Application Başlatma

### Addım 5: Application-u yenidən başlat

```bash
cd Loyalty.WebAPI
dotnet run
```

və ya Visual Studio-da **F5**

---

## ✅ Test Et

### Test 1: Countries with Cities
```bash
curl https://localhost:7285/api/location/countries
```

**Gözlənilən:**
```json
{
  "success": true,
  "data": [
    {
      "id": 1,
      "azName": "Azərbaycan",
      "enName": "Azerbaijan",
      "ruName": "Азербайджан",
      "cities": [
        {
          "id": 1,
          "azName": "Bakı",
          "enName": "Baku",
          "ruName": "Баку",
          "countryId": 1
        }
      ]
    }
  ]
}
```

### Test 2: Country with Cities (YENİ)
```bash
curl https://localhost:7285/api/location/countries/1/with-cities
```

**Gözlənilən:**
```json
{
  "success": true,
  "data": {
    "id": 1,
    "azName": "Azərbaycan",
    "enName": "Azerbaijan",
    "ruName": "Азербайджан",
    "cities": [
      {
        "id": 1,
        "azName": "Bakı",
        "enName": "Baku",
        "ruName": "Баку",
        "countryId": 1
      }
    ]
  }
}
```

---

## 🔄 Rollback (Lazım olarsa)

### Migration geri al

```bash
cd Loyalty.DataAccess
dotnet ef database update 20260123100000_TenantSearchOptimizationIndexes --project . --startup-project ../Loyalty.WebAPI
```

Bu, son migration-u (`AddMultiLanguageSupportToLocations`) geri alacaq.

---

## 🐛 Xəta Həlli (Troubleshooting)

### Xəta 1: `Invalid column name 'Name'`

**Səbəb:** Migration tətbiq edilməyib

**Həll:** Yuxarıdakı addımları izləyin

---

### Xəta 2: `Login failed for user`

**Səbəb:** SQL Server əlaqə problemi

**Həll:**

1. SQL Server işləyirmi?
```bash
# Services yoxla
services.msc
```

2. Connection string doğrudur?
```json
// appsettings.json
"ConnectionStrings": {
  "LoyaltyDbConnection": "Server=.;Database=LoyaltyWallet;..."
}
```

3. SQL Server Authentication aktivdir?

---

### Xəta 3: `Cannot insert duplicate key`

**Səbəb:** Database-də artıq data var və conflict yaranır

**Həll:**

**Variant 1: Migration-u düzəlt (Tövsiyə edilir)**
```bash
# Migration-u sil
dotnet ef migrations remove --project Loyalty.DataAccess --startup-project Loyalty.WebAPI

# Yenidən yarat
dotnet ef migrations add AddMultiLanguageSupportToLocations --project Loyalty.DataAccess --startup-project Loyalty.WebAPI
```

**Variant 2: Manual SQL**
```sql
-- Əvvəlcə yoxla
SELECT COUNT(*) FROM Countries WHERE AzName IS NULL OR AzName = '';

-- Manual update
UPDATE Countries SET AzName = Name WHERE AzName = '';
UPDATE Cities SET AzName = Name WHERE AzName = '';
```

---

### Xəta 4: `Build failed`

**Səbəb:** Kod xətası

**Həll:**
```bash
cd Loyalty.DataAccess
dotnet build
```

Xətaları oxuyun və düzəldin.

---

## 📝 Migration Faylları

### Mövcud Migration-lar:

1. ✅ `20260123100000_TenantSearchOptimizationIndexes.cs`
   - Tenant search indexes
   - Status: Tətbiq edilib

2. 🆕 `20260123120000_AddMultiLanguageSupportToLocations.cs`
   - Multi-language support
   - Status: **Tətbiq edilməlidir**

---

## 🎯 Migration Sonrası Yoxlama

### Database-də yoxla:

```sql
-- Countries table struktur
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE 
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'Countries';

-- Gözlənilən sütunlar:
-- Id, Name, AzName, EnName, RuName, IsActive, CreatedAt, UpdatedAt

-- Cities table struktur
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE 
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'Cities';

-- Gözlənilən sütunlar:
-- Id, Name, AzName, EnName, RuName, countryId, IsActive, CreatedAt, UpdatedAt
```

### İndexləri yoxla:

```sql
-- Countries indexes
SELECT name, type_desc 
FROM sys.indexes 
WHERE object_id = OBJECT_ID('Countries');

-- Gözlənilən:
-- PK_Countries (CLUSTERED)
-- IX_Countries_AzName (NONCLUSTERED)

-- Cities indexes
SELECT name, type_desc 
FROM sys.indexes 
WHERE object_id = OBJECT_ID('Cities');

-- Gözlənilən:
-- PK_Cities (CLUSTERED)
-- IX_Cities_AzName (NONCLUSTERED)
-- IX_Cities_countryId_AzName (NONCLUSTERED)
```

---

## 🆕 Yeni Endpointlər

Migration tətbiq edildikdən sonra istifadə edə bilərsiniz:

| Method | Endpoint | Təsvir | Cities? |
|--------|----------|--------|---------|
| GET | `/api/location/countries` | **Bütün ölkələr + cities** | ✅ |
| GET | `/api/location/countries/{id}` | Bir ölkə | ❌ |
| GET | `/api/location/countries/{id}/with-cities` | Ölkə + şəhərlər | ✅ |
| GET | `/api/location/countries/{countryId}/cities` | Ölkəyə görə şəhərlər | ✅ |
| GET | `/api/location/cities` | Bütün şəhərlər | ❌ |
| GET | `/api/location/cities/{id}` | Bir şəhər | ❌ |

---

## 📞 Kömək

Problemlər davam edərsə:

1. **Log-lara bax:**
   - Application logs
   - SQL Server logs

2. **Database bağlantısını test et:**
```bash
sqlcmd -S . -d LoyaltyWallet -E
```

3. **Development team ilə əlaqə:**
   - Migration faylını göstər
   - Xəta mesajını paylaş
   - Database version göstər

---

## ✅ Xülasə

1. ⚠️ Application-u dayandır
2. 🔧 Migration tətbiq et: `dotnet ef database update`
3. ✅ Yoxla: Database-də yeni sütunlar var
4. 🚀 Application-u başlat
5. 🧪 Test et: `/api/location/countries/1/with-cities`

**Migration uğurlu olduqdan sonra xəta aradan qalxacaq!** ✨
