# Frontend - Menu Category Update Səhifəsi

## Tapşırıq
Menu Category üçün update səhifəsi yaradın. Səhifə ID-yə görə menu category-ni yükləyib, formu doldurmalı və update endpoint-inə göndərməlidir.

## API Endpoint-lər

### 1. Menu Category-ni ID-yə görə yükləmək
```
GET /api/MenuCategory/GetById/{id}
Authorization: Bearer {token}
```

**Response:**
```typescript
{
  success: boolean;
  data: {
    id: number;
    azName: string;
    enName: string;
    ruName: string;
    azDescription?: string;
    enDescription?: string;
    ruDescription?: string;
    tenantId: number;
    menuItems?: Array<{
      id: number;
      currency: string;
      azName: string;
      enName: string;
      ruName: string;
      // ... digər menu item field-ləri
    }>;
  };
  message?: string;
}
```

**Nümunə:**
```json
{
  "success": true,
  "data": {
    "id": 1,
    "azName": "İçkilər",
    "enName": "Beverages",
    "ruName": "Напитки",
    "azDescription": "Müxtəlif içkilər",
    "enDescription": "Various beverages",
    "ruDescription": "Различные напитки",
    "tenantId": 5,
    "menuItems": []
  }
}
```

### 2. Menu Category-ni Update etmək
```
PUT /api/MenuCategory/Update
Authorization: Bearer {token}
Content-Type: application/json
```

**Request Body:**
```typescript
{
  id: number;                    // Required
  azName: string;                // Required
  enName: string;                // Required
  ruName: string;                // Required
  azDescription?: string;
  enDescription?: string;
  ruDescription?: string;
}
```

**Response:**
```typescript
{
  success: boolean;
  data: {
    id: number;
    azName: string;
    enName: string;
    ruName: string;
    azDescription?: string;
    enDescription?: string;
    ruDescription?: string;
    tenantId: number;
    menuItems?: Array<GetMenuItemDto>;
  };
  message: string; // "Menyu kateqoriyası uğurla yeniləndi"
}
```

**Nümunə:**
```json
{
  "success": true,
  "data": {
    "id": 1,
    "azName": "İçkilər",
    "enName": "Beverages",
    "ruName": "Напитки",
    "azDescription": "Müxtəlif içkilər",
    "enDescription": "Various beverages",
    "ruDescription": "Различные напитки",
    "tenantId": 5
  },
  "message": "Menyu kateqoriyası uğurla yeniləndi"
}
```

## React Component Strukturu

### 1. TypeScript Interface-lər

```typescript
interface MenuCategory {
  id: number;
  azName: string;
  enName: string;
  ruName: string;
  azDescription?: string;
  enDescription?: string;
  ruDescription?: string;
  tenantId: number;
  menuItems?: Array<any>;
}

interface UpdateMenuCategoryFormData {
  id: number;
  azName: string;
  enName: string;
  ruName: string;
  azDescription?: string;
  enDescription?: string;
  ruDescription?: string;
}

interface ServiceResult<T = any> {
  success: boolean;
  data?: T;
  message?: string;
}
```

### 2. Component Strukturu

```typescript
import React, { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import axios from 'axios';

const MenuCategoryUpdatePage: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [menuCategory, setMenuCategory] = useState<MenuCategory | null>(null);
  const [formData, setFormData] = useState<UpdateMenuCategoryFormData>({
    id: 0,
    azName: '',
    enName: '',
    ruName: '',
    azDescription: '',
    enDescription: '',
    ruDescription: ''
  });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [message, setMessage] = useState('');

  const API_BASE_URL = process.env.REACT_APP_API_BASE_URL || 'https://your-api-url.com';

  // Menu Category-ni yüklə
  useEffect(() => {
    const fetchMenuCategory = async () => {
      try {
        setLoading(true);
        const token = localStorage.getItem('token'); // və ya auth context-dən götür
        
        const response = await axios.get(
          `${API_BASE_URL}/api/MenuCategory/GetById/${id}`,
          {
            headers: {
              Authorization: `Bearer ${token}`
            }
          }
        );

        if (response.data.success && response.data.data) {
          const category = response.data.data;
          setMenuCategory(category);
          
          // Formu doldur
          setFormData({
            id: category.id,
            azName: category.azName || '',
            enName: category.enName || '',
            ruName: category.ruName || '',
            azDescription: category.azDescription || '',
            enDescription: category.enDescription || '',
            ruDescription: category.ruDescription || ''
          });
        }
      } catch (error) {
        console.error('Menu category yüklənərkən xəta:', error);
        setMessage('Menu category yüklənə bilmədi');
      } finally {
        setLoading(false);
      }
    };

    if (id) {
      fetchMenuCategory();
    }
  }, [id]);

  // Form input dəyişiklikləri
  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
    const { name, value } = e.target;
    setFormData(prev => ({
      ...prev,
      [name]: value
    }));
    
    // Xəta mesajını sil
    if (errors[name]) {
      setErrors(prev => {
        const newErrors = { ...prev };
        delete newErrors[name];
        return newErrors;
      });
    }
  };

  // Form göndər
  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitting(true);
    setErrors({});
    setMessage('');

    try {
      // Validation
      const validationErrors: Record<string, string> = {};
      
      if (!formData.azName.trim()) validationErrors.azName = 'Azərbaycan dilində ad məcburidir';
      if (!formData.enName.trim()) validationErrors.enName = 'İngilis dilində ad məcburidir';
      if (!formData.ruName.trim()) validationErrors.ruName = 'Rus dilində ad məcburidir';

      if (Object.keys(validationErrors).length > 0) {
        setErrors(validationErrors);
        setSubmitting(false);
        return;
      }

      const token = localStorage.getItem('token');
      const response = await axios.put(
        `${API_BASE_URL}/api/MenuCategory/Update`,
        formData,
        {
          headers: {
            Authorization: `Bearer ${token}`,
            'Content-Type': 'application/json'
          }
        }
      );

      if (response.data.success) {
        setMessage(response.data.message || 'Menu category uğurla yeniləndi');
        
        // 2 saniyə sonra menu categories siyahısına yönləndir
        setTimeout(() => {
          navigate('/menu-categories');
        }, 2000);
      } else {
        setMessage(response.data.message || 'Xəta baş verdi');
      }
    } catch (error: any) {
      console.error('Update xətası:', error);
      
      if (error.response?.data?.errors) {
        // Backend validation xətaları
        const backendErrors: Record<string, string> = {};
        Object.keys(error.response.data.errors).forEach(key => {
          backendErrors[key] = error.response.data.errors[key][0];
        });
        setErrors(backendErrors);
      } else {
        setMessage(error.response?.data?.message || 'Update zamanı xəta baş verdi');
      }
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) {
    return (
      <div className="loading-container">
        <div className="spinner"></div>
        <p>Yüklənir...</p>
      </div>
    );
  }

  if (!menuCategory) {
    return (
      <div className="error-container">
        <h2>Menu category tapılmadı</h2>
        <button onClick={() => navigate('/menu-categories')}>
          Geri qayıt
        </button>
      </div>
    );
  }

  return (
    <div className="menu-category-update-page">
      <div className="page-header">
        <h1>Menu Category Yenilə</h1>
        <button 
          onClick={() => navigate('/menu-categories')}
          className="back-button"
        >
          Geri
        </button>
      </div>
      
      <form onSubmit={handleSubmit} className="update-form">
        {/* Azərbaycan adı */}
        <div className="form-group">
          <label htmlFor="azName">
            Azərbaycan dilində ad *
          </label>
          <input
            type="text"
            id="azName"
            name="azName"
            value={formData.azName}
            onChange={handleInputChange}
            required
            disabled={submitting}
            className={errors.azName ? 'error' : ''}
          />
          {errors.azName && (
            <span className="error-message">{errors.azName}</span>
          )}
        </div>

        {/* İngilis adı */}
        <div className="form-group">
          <label htmlFor="enName">
            İngilis dilində ad *
          </label>
          <input
            type="text"
            id="enName"
            name="enName"
            value={formData.enName}
            onChange={handleInputChange}
            required
            disabled={submitting}
            className={errors.enName ? 'error' : ''}
          />
          {errors.enName && (
            <span className="error-message">{errors.enName}</span>
          )}
        </div>

        {/* Rus adı */}
        <div className="form-group">
          <label htmlFor="ruName">
            Rus dilində ad *
          </label>
          <input
            type="text"
            id="ruName"
            name="ruName"
            value={formData.ruName}
            onChange={handleInputChange}
            required
            disabled={submitting}
            className={errors.ruName ? 'error' : ''}
          />
          {errors.ruName && (
            <span className="error-message">{errors.ruName}</span>
          )}
        </div>

        {/* Azərbaycan təsviri */}
        <div className="form-group">
          <label htmlFor="azDescription">
            Azərbaycan dilində təsvir
          </label>
          <textarea
            id="azDescription"
            name="azDescription"
            value={formData.azDescription}
            onChange={handleInputChange}
            rows={4}
            disabled={submitting}
            className={errors.azDescription ? 'error' : ''}
          />
          {errors.azDescription && (
            <span className="error-message">{errors.azDescription}</span>
          )}
        </div>

        {/* İngilis təsviri */}
        <div className="form-group">
          <label htmlFor="enDescription">
            İngilis dilində təsvir
          </label>
          <textarea
            id="enDescription"
            name="enDescription"
            value={formData.enDescription}
            onChange={handleInputChange}
            rows={4}
            disabled={submitting}
            className={errors.enDescription ? 'error' : ''}
          />
          {errors.enDescription && (
            <span className="error-message">{errors.enDescription}</span>
          )}
        </div>

        {/* Rus təsviri */}
        <div className="form-group">
          <label htmlFor="ruDescription">
            Rus dilində təsvir
          </label>
          <textarea
            id="ruDescription"
            name="ruDescription"
            value={formData.ruDescription}
            onChange={handleInputChange}
            rows={4}
            disabled={submitting}
            className={errors.ruDescription ? 'error' : ''}
          />
          {errors.ruDescription && (
            <span className="error-message">{errors.ruDescription}</span>
          )}
        </div>

        {/* Mesaj */}
        {message && (
          <div className={`message ${message.includes('uğurla') ? 'success' : 'error'}`}>
            {message}
          </div>
        )}

        {/* Submit button */}
        <div className="form-actions">
          <button 
            type="submit" 
            disabled={submitting}
            className="submit-button"
          >
            {submitting ? 'Yenilənir...' : 'Yenilə'}
          </button>
          <button 
            type="button" 
            onClick={() => navigate('/menu-categories')}
            disabled={submitting}
            className="cancel-button"
          >
            Ləğv et
          </button>
        </div>
      </form>
    </div>
  );
};

export default MenuCategoryUpdatePage;
```

### 3. CSS Stil

```css
.menu-category-update-page {
  max-width: 800px;
  margin: 0 auto;
  padding: 20px;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 30px;
}

.page-header h1 {
  margin: 0;
  color: #333;
  font-size: 28px;
}

.back-button {
  padding: 10px 20px;
  background: #6c757d;
  color: white;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  font-size: 14px;
  transition: background 0.3s;
}

.back-button:hover {
  background: #5a6268;
}

.update-form {
  background: white;
  padding: 30px;
  border-radius: 8px;
  box-shadow: 0 2px 10px rgba(0, 0, 0, 0.1);
}

.form-group {
  margin-bottom: 20px;
}

.form-group label {
  display: block;
  margin-bottom: 8px;
  font-weight: 600;
  color: #555;
  font-size: 14px;
}

.form-group input,
.form-group textarea {
  width: 100%;
  padding: 12px;
  border: 2px solid #e0e0e0;
  border-radius: 6px;
  font-size: 16px;
  font-family: inherit;
  transition: border-color 0.3s;
  box-sizing: border-box;
}

.form-group input:focus,
.form-group textarea:focus {
  outline: none;
  border-color: #667eea;
}

.form-group input.error,
.form-group textarea.error {
  border-color: #e74c3c;
}

.form-group input:disabled,
.form-group textarea:disabled {
  background-color: #f5f5f5;
  cursor: not-allowed;
}

.form-group textarea {
  resize: vertical;
  min-height: 100px;
}

.error-message {
  display: block;
  color: #e74c3c;
  font-size: 12px;
  margin-top: 5px;
}

.message {
  padding: 15px;
  border-radius: 6px;
  margin-bottom: 20px;
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

.form-actions {
  display: flex;
  gap: 15px;
  margin-top: 30px;
  justify-content: flex-end;
}

.submit-button {
  padding: 12px 30px;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
  border: none;
  border-radius: 6px;
  font-size: 16px;
  font-weight: 600;
  cursor: pointer;
  transition: transform 0.2s, box-shadow 0.2s;
}

.submit-button:hover:not(:disabled) {
  transform: translateY(-2px);
  box-shadow: 0 5px 15px rgba(102, 126, 234, 0.4);
}

.submit-button:disabled {
  background: #ccc;
  cursor: not-allowed;
  transform: none;
}

.cancel-button {
  padding: 12px 30px;
  background: #f5f5f5;
  color: #333;
  border: 2px solid #e0e0e0;
  border-radius: 6px;
  font-size: 16px;
  font-weight: 600;
  cursor: pointer;
  transition: background 0.3s;
}

.cancel-button:hover:not(:disabled) {
  background: #e0e0e0;
}

.cancel-button:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.loading-container,
.error-container {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  min-height: 400px;
  text-align: center;
}

.spinner {
  border: 4px solid #f3f3f3;
  border-top: 4px solid #667eea;
  border-radius: 50%;
  width: 50px;
  height: 50px;
  animation: spin 1s linear infinite;
  margin-bottom: 20px;
}

@keyframes spin {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
}

.error-container h2 {
  color: #e74c3c;
  margin-bottom: 20px;
}

.error-container button {
  padding: 10px 20px;
  background: #667eea;
  color: white;
  border: none;
  border-radius: 6px;
  cursor: pointer;
}

/* Responsive */
@media (max-width: 768px) {
  .menu-category-update-page {
    padding: 15px;
  }

  .page-header {
    flex-direction: column;
    align-items: flex-start;
    gap: 15px;
  }

  .update-form {
    padding: 20px;
  }

  .form-actions {
    flex-direction: column;
  }

  .submit-button,
  .cancel-button {
    width: 100%;
  }
}
```

### 4. Routing (React Router)

```typescript
// App.tsx və ya routing faylında
import { Route, Routes } from 'react-router-dom';
import MenuCategoryUpdatePage from './pages/MenuCategoryUpdatePage';

<Routes>
  <Route path="/menu-categories/update/:id" element={<MenuCategoryUpdatePage />} />
</Routes>
```

### 5. Menu Categories siyahısında "Yenilə" düyməsi

```typescript
// MenuCategoriesList component-də
import { Link } from 'react-router-dom';

<Link to={`/menu-categories/update/${category.id}`}>
  <button className="update-button">Yenilə</button>
</Link>
```

## İstifadə Flow-u

1. **Menu categories siyahısında "Yenilə" düyməsinə klik edilir**
   - `/menu-categories/update/{id}` route-na yönləndirilir

2. **Component mount olur**
   - `useEffect` hook-u işə düşür
   - `GET /api/MenuCategory/GetById/{id}` endpoint-inə request göndərilir
   - Menu category məlumatları yüklənir

3. **Form doldurulur**
   - Mövcud məlumatlar form field-lərinə doldurulur
   - İstifadəçi dəyişiklik edə bilər

4. **"Yenilə" düyməsinə basılır**
   - Form validation yoxlanılır
   - `PUT /api/MenuCategory/Update` endpoint-inə request göndərilir
   - Backend menu category-ni yeniləyir

5. **Uğurlu cavab**
   - Uğur mesajı göstərilir
   - 2 saniyə sonra menu categories siyahısına yönləndirilir

## Xüsusiyyətlər

✅ **ID-yə görə yükləmə** - Menu category avtomatik yüklənir  
✅ **Form auto-fill** - Mövcud məlumatlar formu doldurur  
✅ **Real-time validation** - Form validation  
✅ **Error handling** - Backend xətalarını göstərir  
✅ **Loading states** - Yükləmə və göndərmə zamanı loading göstərir  
✅ **Responsive design** - Mobil uyğun  
✅ **User-friendly messages** - Aydın mesajlar  
✅ **Auto-navigation** - Uğurlu update-dən sonra siyahıya yönləndirir  
✅ **Multi-language support** - Azərbaycan, İngilis və Rus dilləri üçün field-lər  

## Qeydlər

- **Authentication**: Bütün endpoint-lər JWT token tələb edir
- **Required fields**: `azName`, `enName`, `ruName` məcburidir
- **Optional fields**: `azDescription`, `enDescription`, `ruDescription` opsionaldır
- **TenantId**: Update zamanı TenantId dəyişdirilə bilməz (yalnız read-only)
- **API_BASE_URL**: Environment variable-dan götürülməlidir
- **Token management**: Token localStorage-dan və ya auth context-dən götürülməlidir

## Test Senaryoları

1. ✅ Düzgün ID ilə menu category yüklə
2. ✅ Yanlış ID ilə menu category yüklə (404 xətası)
3. ✅ Formu doldur və update et
4. ✅ Məcburi field-ləri boş burax (validation xətası)
5. ✅ Uğurlu update-dən sonra yönləndirmə
6. ✅ Token olmadan request göndər (401 xətası)
7. ✅ Müddəti bitmiş token ilə request göndər (401 xətası)
