# Flutter Integration Guide - Location & Tenant Filtering

## 📱 Flutter üçün API Endpointləri

### 1. Location API Endpointləri

| Endpoint | Method | Təsvir | Response |
|----------|--------|--------|----------|
| `/api/location/countries` | GET | Bütün ölkələr + şəhərlər | Countries with cities |
| `/api/location/countries/{id}/with-cities` | GET | Bir ölkə + şəhərlər | Country with cities |
| `/api/location/countries/{countryId}/cities` | GET | Ölkəyə görə şəhərlər | Cities only |

### 2. Tenant Filtering API

| Endpoint | Method | Təsvir | Query Parameters |
|----------|--------|--------|------------------|
| `/api/tenant/gettenants` | GET | Tenant filtrasiya | `countryId`, `cityId`, `categoryId`, `name`, `currentPage`, `pageSize` |
| `/api/public/home/tenants` | GET | Discover/Home tenants | `countryId`, `cityId` |

---

## 📦 Flutter Models

### 1. Country Model

```dart
// lib/models/country.dart
class Country {
  final int id;
  final String azName;
  final String enName;
  final String ruName;
  final List<City> cities;

  Country({
    required this.id,
    required this.azName,
    required this.enName,
    required this.ruName,
    required this.cities,
  });

  // Helper to get localized name
  String getName(String language) {
    switch (language) {
      case 'az':
        return azName;
      case 'en':
        return enName;
      case 'ru':
        return ruName;
      default:
        return azName;
    }
  }

  factory Country.fromJson(Map<String, dynamic> json) {
    return Country(
      id: json['id'] as int,
      azName: json['azName'] as String? ?? '',
      enName: json['enName'] as String? ?? '',
      ruName: json['ruName'] as String? ?? '',
      cities: (json['cities'] as List<dynamic>?)
              ?.map((city) => City.fromJson(city as Map<String, dynamic>))
              .toList() ??
          [],
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'azName': azName,
      'enName': enName,
      'ruName': ruName,
      'cities': cities.map((city) => city.toJson()).toList(),
    };
  }
}
```

### 2. City Model

```dart
// lib/models/city.dart
class City {
  final int id;
  final String azName;
  final String enName;
  final String ruName;
  final int countryId;

  City({
    required this.id,
    required this.azName,
    required this.enName,
    required this.ruName,
    required this.countryId,
  });

  String getName(String language) {
    switch (language) {
      case 'az':
        return azName;
      case 'en':
        return enName;
      case 'ru':
        return ruName;
      default:
        return azName;
    }
  }

  factory City.fromJson(Map<String, dynamic> json) {
    return City(
      id: json['id'] as int,
      azName: json['azName'] as String? ?? '',
      enName: json['enName'] as String? ?? '',
      ruName: json['ruName'] as String? ?? '',
      countryId: json['countryId'] as int,
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'azName': azName,
      'enName': enName,
      'ruName': ruName,
      'countryId': countryId,
    };
  }
}
```

### 3. Tenant Model (with location)

```dart
// lib/models/tenant.dart
class Tenant {
  final int id;
  final String companyName;
  final int? categoryId;
  final int? countryId;
  final int? cityId;
  final TenantLocation? tenantLocation;

  Tenant({
    required this.id,
    required this.companyName,
    this.categoryId,
    this.countryId,
    this.cityId,
    this.tenantLocation,
  });

  factory Tenant.fromJson(Map<String, dynamic> json) {
    return Tenant(
      id: json['id'] as int,
      companyName: json['companyName'] as String? ?? '',
      categoryId: json['categoryId'] as int?,
      countryId: json['countryId'] as int?,
      cityId: json['cityId'] as int?,
      tenantLocation: json['tenantLocation'] != null
          ? TenantLocation.fromJson(json['tenantLocation'])
          : null,
    );
  }
}

class TenantLocation {
  final int id;
  final int? countryId;
  final String? countryName;
  final int? cityId;
  final String? cityName;

  TenantLocation({
    required this.id,
    this.countryId,
    this.countryName,
    this.cityId,
    this.cityName,
  });

  factory TenantLocation.fromJson(Map<String, dynamic> json) {
    return TenantLocation(
      id: json['id'] as int,
      countryId: json['countryId'] as int?,
      countryName: json['countryName'] as String?,
      cityId: json['cityId'] as int?,
      cityName: json['cityName'] as String?,
    );
  }
}
```

---

## 🌐 API Service

```dart
// lib/services/api_service.dart
import 'dart:convert';
import 'package:http/http.dart' as http;
import '../models/country.dart';
import '../models/tenant.dart';

class ApiService {
  final String baseUrl;
  
  ApiService({required this.baseUrl});

  // Get all countries with cities (SINGLE CALL)
  Future<List<Country>> getCountriesWithCities() async {
    final response = await http.get(
      Uri.parse('$baseUrl/api/location/countries'),
    );

    if (response.statusCode == 200) {
      final data = json.decode(response.body);
      if (data['success'] == true) {
        final List<dynamic> countries = data['data'] as List<dynamic>;
        return countries
            .map((json) => Country.fromJson(json as Map<String, dynamic>))
            .toList();
      }
    }
    throw Exception('Failed to load countries');
  }

  // Get tenants with filters
  Future<TenantSearchResult> getTenants({
    int? countryId,
    int? cityId,
    int? categoryId,
    String? name,
    int currentPage = 1,
    int pageSize = 10,
  }) async {
    final queryParams = <String, String>{
      'currentPage': currentPage.toString(),
      'pageSize': pageSize.toString(),
    };

    if (countryId != null) {
      queryParams['model.countryId'] = countryId.toString();
    }
    if (cityId != null) {
      queryParams['model.cityId'] = cityId.toString();
    }
    if (categoryId != null) {
      queryParams['model.categoryId'] = categoryId.toString();
    }
    if (name != null && name.isNotEmpty) {
      queryParams['model.name'] = name;
    }

    final uri = Uri.parse('$baseUrl/api/tenant/gettenants')
        .replace(queryParameters: queryParams);

    final response = await http.get(uri);

    if (response.statusCode == 200) {
      final data = json.decode(response.body);
      if (data['success'] == true) {
        return TenantSearchResult.fromJson(data['data']);
      }
    }
    throw Exception('Failed to load tenants');
  }

  // Get discover/home tenants with location filter
  Future<List<Tenant>> getHomeTenants({
    int? countryId,
    int? cityId,
  }) async {
    final queryParams = <String, String>{};

    if (countryId != null) {
      queryParams['countryId'] = countryId.toString();
    }
    if (cityId != null) {
      queryParams['cityId'] = cityId.toString();
    }

    final uri = Uri.parse('$baseUrl/api/public/home/tenants')
        .replace(queryParameters: queryParams);

    final response = await http.get(uri);

    if (response.statusCode == 200) {
      final data = json.decode(response.body);
      if (data['success'] == true) {
        final List<dynamic> tenants = data['data'] as List<dynamic>;
        return tenants
            .map((json) => Tenant.fromJson(json as Map<String, dynamic>))
            .toList();
      }
    }
    throw Exception('Failed to load home tenants');
  }
}

class TenantSearchResult {
  final List<Tenant> tenants;
  final int totalCount;
  final int currentPage;
  final int pageSize;

  TenantSearchResult({
    required this.tenants,
    required this.totalCount,
    required this.currentPage,
    required this.pageSize,
  });

  int get totalPages => (totalCount / pageSize).ceil();

  factory TenantSearchResult.fromJson(Map<String, dynamic> json) {
    return TenantSearchResult(
      tenants: (json['value'] as List<dynamic>)
          .map((t) => Tenant.fromJson(t as Map<String, dynamic>))
          .toList(),
      totalCount: json['dataCount'] as int,
      currentPage: json['currentPage'] as int,
      pageSize: json['pageSize'] as int,
    );
  }
}
```

---

## 🎨 UI Components

### 1. Location Filter Widget

```dart
// lib/widgets/location_filter.dart
import 'package:flutter/material.dart';
import '../models/country.dart';
import '../models/city.dart';

class LocationFilter extends StatefulWidget {
  final List<Country> countries;
  final String language;
  final Function(int? countryId, int? cityId) onFilterChanged;

  const LocationFilter({
    Key? key,
    required this.countries,
    required this.language,
    required this.onFilterChanged,
  }) : super(key: key);

  @override
  State<LocationFilter> createState() => _LocationFilterState();
}

class _LocationFilterState extends State<LocationFilter> {
  Country? selectedCountry;
  City? selectedCity;
  List<City> availableCities = [];

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        // Country Dropdown
        DropdownButtonFormField<Country>(
          value: selectedCountry,
          decoration: InputDecoration(
            labelText: _getLabel('country'),
            border: OutlineInputBorder(),
            prefixIcon: Icon(Icons.public),
          ),
          hint: Text(_getLabel('selectCountry')),
          items: widget.countries.map((country) {
            return DropdownMenuItem<Country>(
              value: country,
              child: Text(
                '${country.getName(widget.language)} (${country.cities.length})',
              ),
            );
          }).toList(),
          onChanged: (Country? country) {
            setState(() {
              selectedCountry = country;
              selectedCity = null;
              availableCities = country?.cities ?? [];
            });
            widget.onFilterChanged(
              selectedCountry?.id,
              null,
            );
          },
        ),
        
        const SizedBox(height: 16),
        
        // City Dropdown
        DropdownButtonFormField<City>(
          value: selectedCity,
          decoration: InputDecoration(
            labelText: _getLabel('city'),
            border: OutlineInputBorder(),
            prefixIcon: Icon(Icons.location_city),
          ),
          hint: Text(_getLabel('selectCity')),
          items: availableCities.map((city) {
            return DropdownMenuItem<City>(
              value: city,
              child: Text(city.getName(widget.language)),
            );
          }).toList(),
          onChanged: selectedCountry == null
              ? null
              : (City? city) {
                  setState(() {
                    selectedCity = city;
                  });
                  widget.onFilterChanged(
                    selectedCountry?.id,
                    selectedCity?.id,
                  );
                },
        ),
        
        // Clear Filter Button
        if (selectedCountry != null || selectedCity != null)
          Padding(
            padding: const EdgeInsets.only(top: 8.0),
            child: TextButton.icon(
              onPressed: () {
                setState(() {
                  selectedCountry = null;
                  selectedCity = null;
                  availableCities = [];
                });
                widget.onFilterChanged(null, null);
              },
              icon: Icon(Icons.clear),
              label: Text(_getLabel('clearFilter')),
            ),
          ),
      ],
    );
  }

  String _getLabel(String key) {
    final labels = {
      'az': {
        'country': 'Ölkə',
        'selectCountry': 'Ölkə seçin',
        'city': 'Şəhər',
        'selectCity': 'Şəhər seçin',
        'clearFilter': 'Filtri təmizlə',
      },
      'en': {
        'country': 'Country',
        'selectCountry': 'Select country',
        'city': 'City',
        'selectCity': 'Select city',
        'clearFilter': 'Clear filter',
      },
      'ru': {
        'country': 'Страна',
        'selectCountry': 'Выберите страну',
        'city': 'Город',
        'selectCity': 'Выберите город',
        'clearFilter': 'Очистить фильтр',
      },
    };

    return labels[widget.language]?[key] ?? labels['az']![key]!;
  }
}
```

### 2. Tenant List Screen (with Filter)

```dart
// lib/screens/tenant_list_screen.dart
import 'package:flutter/material.dart';
import '../models/country.dart';
import '../models/tenant.dart';
import '../services/api_service.dart';
import '../widgets/location_filter.dart';

class TenantListScreen extends StatefulWidget {
  const TenantListScreen({Key? key}) : super(key: key);

  @override
  State<TenantListScreen> createState() => _TenantListScreenState();
}

class _TenantListScreenState extends State<TenantListScreen> {
  final ApiService _apiService = ApiService(
    baseUrl: 'https://your-api-url.com',
  );

  List<Country> countries = [];
  List<Tenant> tenants = [];
  bool isLoadingCountries = true;
  bool isLoadingTenants = false;
  
  int? selectedCountryId;
  int? selectedCityId;
  int currentPage = 1;
  int totalPages = 0;
  
  String language = 'az'; // az, en, ru

  @override
  void initState() {
    super.initState();
    _loadCountries();
    _loadTenants();
  }

  Future<void> _loadCountries() async {
    try {
      setState(() => isLoadingCountries = true);
      final loadedCountries = await _apiService.getCountriesWithCities();
      setState(() {
        countries = loadedCountries;
        isLoadingCountries = false;
      });
    } catch (e) {
      setState(() => isLoadingCountries = false);
      _showError('Failed to load countries: $e');
    }
  }

  Future<void> _loadTenants() async {
    try {
      setState(() => isLoadingTenants = true);
      
      final result = await _apiService.getTenants(
        countryId: selectedCountryId,
        cityId: selectedCityId,
        currentPage: currentPage,
        pageSize: 20,
      );
      
      setState(() {
        tenants = result.tenants;
        totalPages = result.totalPages;
        isLoadingTenants = false;
      });
    } catch (e) {
      setState(() => isLoadingTenants = false);
      _showError('Failed to load tenants: $e');
    }
  }

  void _onFilterChanged(int? countryId, int? cityId) {
    setState(() {
      selectedCountryId = countryId;
      selectedCityId = cityId;
      currentPage = 1;
    });
    _loadTenants();
  }

  void _showError(String message) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(message)),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text('Tenants'),
        actions: [
          // Language Selector
          PopupMenuButton<String>(
            initialValue: language,
            onSelected: (String lang) {
              setState(() => language = lang);
            },
            itemBuilder: (context) => [
              PopupMenuItem(value: 'az', child: Text('🇦🇿 Azərbaycan')),
              PopupMenuItem(value: 'en', child: Text('🇬🇧 English')),
              PopupMenuItem(value: 'ru', child: Text('🇷🇺 Русский')),
            ],
          ),
        ],
      ),
      body: Column(
        children: [
          // Filter Section
          Card(
            margin: EdgeInsets.all(16),
            child: Padding(
              padding: EdgeInsets.all(16),
              child: isLoadingCountries
                  ? Center(child: CircularProgressIndicator())
                  : LocationFilter(
                      countries: countries,
                      language: language,
                      onFilterChanged: _onFilterChanged,
                    ),
            ),
          ),
          
          // Filter Info
          if (selectedCountryId != null || selectedCityId != null)
            Padding(
              padding: EdgeInsets.symmetric(horizontal: 16),
              child: Row(
                children: [
                  Icon(Icons.filter_list, size: 16),
                  SizedBox(width: 8),
                  Text(
                    'Filtered by: ${_getFilterText()}',
                    style: TextStyle(fontSize: 12, color: Colors.grey[600]),
                  ),
                ],
              ),
            ),
          
          // Tenant List
          Expanded(
            child: isLoadingTenants
                ? Center(child: CircularProgressIndicator())
                : tenants.isEmpty
                    ? Center(
                        child: Column(
                          mainAxisAlignment: MainAxisAlignment.center,
                          children: [
                            Icon(Icons.store_outlined, size: 64, color: Colors.grey),
                            SizedBox(height: 16),
                            Text('No tenants found'),
                          ],
                        ),
                      )
                    : RefreshIndicator(
                        onRefresh: _loadTenants,
                        child: ListView.builder(
                          itemCount: tenants.length,
                          itemBuilder: (context, index) {
                            final tenant = tenants[index];
                            return ListTile(
                              leading: CircleAvatar(
                                child: Text(tenant.companyName[0]),
                              ),
                              title: Text(tenant.companyName),
                              subtitle: tenant.tenantLocation != null
                                  ? Text(
                                      '${tenant.tenantLocation!.cityName}, ${tenant.tenantLocation!.countryName}',
                                    )
                                  : null,
                              trailing: Icon(Icons.arrow_forward_ios, size: 16),
                              onTap: () {
                                // Navigate to tenant details
                              },
                            );
                          },
                        ),
                      ),
          ),
          
          // Pagination
          if (totalPages > 1)
            Container(
              padding: EdgeInsets.symmetric(vertical: 8),
              decoration: BoxDecoration(
                color: Colors.grey[100],
                border: Border(top: BorderSide(color: Colors.grey[300]!)),
              ),
              child: Row(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  IconButton(
                    icon: Icon(Icons.chevron_left),
                    onPressed: currentPage > 1
                        ? () {
                            setState(() => currentPage--);
                            _loadTenants();
                          }
                        : null,
                  ),
                  Text('Page $currentPage of $totalPages'),
                  IconButton(
                    icon: Icon(Icons.chevron_right),
                    onPressed: currentPage < totalPages
                        ? () {
                            setState(() => currentPage++);
                            _loadTenants();
                          }
                        : null,
                  ),
                ],
              ),
            ),
        ],
      ),
    );
  }

  String _getFilterText() {
    final country = countries.firstWhere(
      (c) => c.id == selectedCountryId,
      orElse: () => countries.first,
    );
    
    if (selectedCityId != null) {
      final city = country.cities.firstWhere(
        (c) => c.id == selectedCityId,
        orElse: () => country.cities.first,
      );
      return '${city.getName(language)}, ${country.getName(language)}';
    }
    
    return country.getName(language);
  }
}
```

---

## 🔗 API URL Examples

### Base URL
```dart
const String API_BASE_URL = 'https://localhost:7285';
// və ya
const String API_BASE_URL = 'https://your-production-url.com';
```

### Full Endpoint URLs

```dart
// Countries with cities
GET https://localhost:7285/api/location/countries

// Tenants with location filter
GET https://localhost:7285/api/tenant/gettenants?model.countryId=1&model.cityId=5&currentPage=1&pageSize=20

// Home tenants with location filter
GET https://localhost:7285/api/public/home/tenants?countryId=1&cityId=5
```

---

## 📋 pubspec.yaml Dependencies

```yaml
dependencies:
  flutter:
    sdk: flutter
  http: ^1.1.0  # API calls üçün
  
  # Optional (state management)
  provider: ^6.1.0
  # və ya
  riverpod: ^2.4.0
  # və ya
  bloc: ^8.1.0
```

---

## 🧪 Test Kodu

```dart
// test/api_service_test.dart
import 'package:flutter_test/flutter_test.dart';
import 'package:your_app/services/api_service.dart';

void main() {
  group('API Service Tests', () {
    final apiService = ApiService(
      baseUrl: 'https://localhost:7285',
    );

    test('Load countries with cities', () async {
      final countries = await apiService.getCountriesWithCities();
      
      expect(countries, isNotEmpty);
      expect(countries.first.cities, isNotEmpty);
      expect(countries.first.azName, isNotEmpty);
    });

    test('Filter tenants by country', () async {
      final result = await apiService.getTenants(
        countryId: 1,
        currentPage: 1,
        pageSize: 10,
      );
      
      expect(result.tenants, isNotEmpty);
      expect(result.totalCount, greaterThan(0));
    });

    test('Filter tenants by country and city', () async {
      final result = await apiService.getTenants(
        countryId: 1,
        cityId: 5,
        currentPage: 1,
        pageSize: 10,
      );
      
      expect(result.tenants, isNotEmpty);
    });
  });
}
```

---

## ✅ Yoxlama Siyahısı

Flutter-də tətbiq etmək üçün:

- [ ] Models yaratdınız (Country, City, Tenant)
- [ ] API Service yaratdınız
- [ ] LocationFilter widget-i əlavə etdiniz
- [ ] API base URL-i konfiqurasiya etdiniz
- [ ] HTTP package yüklədiniz (`flutter pub get`)
- [ ] API-yə çatırsınız (network permissions)
- [ ] Migration tətbiq edilib (Backend)
- [ ] Test etdiniz

---

## 🚀 Quick Start

```bash
# 1. Create new Flutter project (or use existing)
flutter create your_app
cd your_app

# 2. Add dependencies
flutter pub add http

# 3. Copy models from above
# lib/models/country.dart
# lib/models/city.dart
# lib/models/tenant.dart

# 4. Copy services
# lib/services/api_service.dart

# 5. Copy widgets
# lib/widgets/location_filter.dart

# 6. Copy screen
# lib/screens/tenant_list_screen.dart

# 7. Run
flutter run
```

---

Bu kod Flutter-də işləməyə hazırdır! 🎉

Sualınız varsa, soruşun! 😊
