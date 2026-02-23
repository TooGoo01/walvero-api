  # Frontend - Menu Item Update Səhifəsi

  ## Tapşırıq
  Menu Item üçün update səhifəsi yaradın. Səhifə ID-yə görə menu item-ı yükləyib, formu doldurmalı və update endpoint-inə göndərməlidir.

  ## API Endpoint-lər

  ### 1. Menu Item-ı ID-yə görə yükləmək
  ```
  GET /api/MenuItem/GetByIdWithDetails/{id}
  Authorization: Bearer {token}
  ```

  **Response:**
  ```typescript
  {
    success: boolean;
    data: {
      id: number;
      currency: string;
      azName: string;
      enName: string;
      ruName: string;
      azDescription?: string;
      ruDescription?: string;
      enDescription?: string;
      prepTimeMinutes?: string;
      currencySign: string;
      price: number;
      discountPrice: number;
      menuCategoryId: number;
      menuCategory?: {
        id: number;
        azName: string;
        enName: string;
        ruName: string;
      };
      menuItemImages?: Array<{
        id: number;
        fileName: string;
        path: string;
      }>;
    };
    message?: string;
  }
  ```

  ### 2. Menu Item-ı Update etmək
  ```
  PUT /api/MenuItem/Update/{id}
  Authorization: Bearer {token}
  Content-Type: multipart/form-data
  ```

  **Request Body (FormData):**
  ```typescript
  {
    currency: string;              // Required
    azName: string;                // Required
    enName: string;                // Required
    ruName: string;                // Required
    azDescription?: string;
    ruDescription?: string;
    enDescription?: string;
    prepTimeMinutes?: string;
    currencySign: string;           // Required
    price: number;                 // Required
    discountPrice: number;
    menuCategoryId: number;        // Required
    files?: File[];                // Şəkillər (optional)
  }
  ```

  **Response:**
  ```typescript
  {
    success: boolean;
    data: number; // Updated item ID
    message: string;
  }
  ```

  ## React Component Strukturu

  ### 1. TypeScript Interface-lər

  ```typescript
  interface MenuItem {
    id: number;
    currency: string;
    azName: string;
    enName: string;
    ruName: string;
    azDescription?: string;
    ruDescription?: string;
    enDescription?: string;
    prepTimeMinutes?: string;
    currencySign: string;
    price: number;
    discountPrice: number;
    menuCategoryId: number;
    menuCategory?: {
      id: number;
      azName: string;
      enName: string;
      ruName: string;
    };
    menuItemImages?: Array<{
      id: number;
      fileName: string;
      path: string;
    }>;
  }

  interface MenuCategory {
    id: number;
    azName: string;
    enName: string;
    ruName: string;
  }

  interface UpdateMenuItemFormData {
    currency: string;
    azName: string;
    enName: string;
    ruName: string;
    azDescription?: string;
    ruDescription?: string;
    enDescription?: string;
    prepTimeMinutes?: string;
    currencySign: string;
    price: number;
    discountPrice: number;
    menuCategoryId: number;
    files?: File[];
  }
  ```

  ### 2. Component Strukturu

  ```typescript
  import React, { useState, useEffect } from 'react';
  import { useParams, useNavigate } from 'react-router-dom';
  import axios from 'axios';

  const MenuItemUpdatePage: React.FC = () => {
    const { id } = useParams<{ id: string }>();
    const navigate = useNavigate();
    
    const [loading, setLoading] = useState(true);
    const [submitting, setSubmitting] = useState(false);
    const [menuItem, setMenuItem] = useState<MenuItem | null>(null);
    const [categories, setCategories] = useState<MenuCategory[]>([]);
    const [formData, setFormData] = useState<UpdateMenuItemFormData>({
      currency: '',
      azName: '',
      enName: '',
      ruName: '',
      azDescription: '',
      ruDescription: '',
      enDescription: '',
      prepTimeMinutes: '',
      currencySign: '',
      price: 0,
      discountPrice: 0,
      menuCategoryId: 0,
      files: []
    });
    const [existingImages, setExistingImages] = useState<Array<{id: number, path: string}>>([]);
    const [newImages, setNewImages] = useState<File[]>([]);
    const [errors, setErrors] = useState<Record<string, string>>({});

    // Menu Item-ı yüklə
    useEffect(() => {
      const fetchMenuItem = async () => {
        try {
          setLoading(true);
          const token = localStorage.getItem('token'); // və ya auth context-dən götür
          
          const response = await axios.get(
            `${API_BASE_URL}/api/MenuItem/GetByIdWithDetails/${id}`,
            {
              headers: {
                Authorization: `Bearer ${token}`
              }
            }
          );

          if (response.data.success && response.data.data) {
            const item = response.data.data;
            setMenuItem(item);
            
            // Formu doldur
            setFormData({
              currency: item.currency || '',
              azName: item.azName || '',
              enName: item.enName || '',
              ruName: item.ruName || '',
              azDescription: item.azDescription || '',
              ruDescription: item.ruDescription || '',
              enDescription: item.enDescription || '',
              prepTimeMinutes: item.prepTimeMinutes || '',
              currencySign: item.currencySign || '',
              price: item.price || 0,
              discountPrice: item.discountPrice || 0,
              menuCategoryId: item.menuCategoryId || 0,
              files: []
            });

            // Mövcud şəkilləri saxla
            if (item.menuItemImages) {
              setExistingImages(item.menuItemImages.map(img => ({
                id: img.id,
                path: img.path
              })));
            }
          }
        } catch (error) {
          console.error('Menu item yüklənərkən xəta:', error);
          alert('Menu item yüklənə bilmədi');
        } finally {
          setLoading(false);
        }
      };

      // Menu kateqoriyalarını yüklə
      const fetchCategories = async () => {
        try {
          const token = localStorage.getItem('token');
          const response = await axios.get(
            `${API_BASE_URL}/api/MenuCategory/GetAll`,
            {
              headers: {
                Authorization: `Bearer ${token}`
              }
            }
          );

          if (response.data.success) {
            setCategories(response.data.data || []);
          }
        } catch (error) {
          console.error('Kateqoriyalar yüklənərkən xəta:', error);
        }
      };

      if (id) {
        fetchMenuItem();
        fetchCategories();
      }
    }, [id]);

    // Form input dəyişiklikləri
    const handleInputChange = (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>) => {
      const { name, value } = e.target;
      setFormData(prev => ({
        ...prev,
        [name]: name === 'price' || name === 'discountPrice' || name === 'menuCategoryId' 
          ? (value === '' ? 0 : parseFloat(value) || parseInt(value)) 
          : value
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

    // Yeni şəkil əlavə et
    const handleImageChange = (e: React.ChangeEvent<HTMLInputElement>) => {
      if (e.target.files) {
        const files = Array.from(e.target.files);
        setNewImages(prev => [...prev, ...files]);
      }
    };

    // Şəkli sil (yeni əlavə edilmiş)
    const handleRemoveNewImage = (index: number) => {
      setNewImages(prev => prev.filter((_, i) => i !== index));
    };

    // Mövcud şəkli sil
    const handleRemoveExistingImage = (imageId: number) => {
      setExistingImages(prev => prev.filter(img => img.id !== imageId));
      // Qeyd: Backend-də şəkilləri silmək üçün ayrı endpoint lazımdır
      // Bu yalnız UI-dan silir
    };

    // Form göndər
    const handleSubmit = async (e: React.FormEvent) => {
      e.preventDefault();
      setSubmitting(true);
      setErrors({});

      try {
        // Validation
        const validationErrors: Record<string, string> = {};
        
        if (!formData.currency.trim()) validationErrors.currency = 'Valyuta məcburidir';
        if (!formData.azName.trim()) validationErrors.azName = 'Azərbaycan dilində ad məcburidir';
        if (!formData.enName.trim()) validationErrors.enName = 'İngilis dilində ad məcburidir';
        if (!formData.ruName.trim()) validationErrors.ruName = 'Rus dilində ad məcburidir';
        if (!formData.currencySign.trim()) validationErrors.currencySign = 'Valyuta işarəsi məcburidir';
        if (formData.price <= 0) validationErrors.price = 'Qiymət məcburidir';
        if (formData.menuCategoryId <= 0) validationErrors.menuCategoryId = 'Menyu kateqoriyası məcburidir';

        if (Object.keys(validationErrors).length > 0) {
          setErrors(validationErrors);
          setSubmitting(false);
          return;
        }

        const token = localStorage.getItem('token');
        const formDataToSend = new FormData();

        // Form məlumatlarını əlavə et
        formDataToSend.append('currency', formData.currency);
        formDataToSend.append('azName', formData.azName);
        formDataToSend.append('enName', formData.enName);
        formDataToSend.append('ruName', formData.ruName);
        if (formData.azDescription) formDataToSend.append('azDescription', formData.azDescription);
        if (formData.ruDescription) formDataToSend.append('ruDescription', formData.ruDescription);
        if (formData.enDescription) formDataToSend.append('enDescription', formData.enDescription);
        if (formData.prepTimeMinutes) formDataToSend.append('prepTimeMinutes', formData.prepTimeMinutes);
        formDataToSend.append('currencySign', formData.currencySign);
        formDataToSend.append('price', formData.price.toString());
        formDataToSend.append('discountPrice', formData.discountPrice.toString());
        formDataToSend.append('menuCategoryId', formData.menuCategoryId.toString());

        // Yeni şəkilləri əlavə et
        if (newImages.length > 0) {
          newImages.forEach((file) => {
            formDataToSend.append('files', file);
          });
        }

        const response = await axios.put(
          `${API_BASE_URL}/api/MenuItem/Update/${id}`,
          formDataToSend,
          {
            headers: {
              Authorization: `Bearer ${token}`,
              'Content-Type': 'multipart/form-data'
            }
          }
        );

        if (response.data.success) {
          alert('Menu item uğurla yeniləndi');
          navigate('/menu-items'); // və ya başqa route
        } else {
          alert(response.data.message || 'Xəta baş verdi');
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
          alert(error.response?.data?.message || 'Update zamanı xəta baş verdi');
        }
      } finally {
        setSubmitting(false);
      }
    };

    if (loading) {
      return <div>Yüklənir...</div>;
    }

    if (!menuItem) {
      return <div>Menu item tapılmadı</div>;
    }

    return (
      <div className="menu-item-update-page">
        <h1>Menu Item Yenilə</h1>
        
        <form onSubmit={handleSubmit}>
          {/* Valyuta */}
          <div className="form-group">
            <label>Valyuta *</label>
            <input
              type="text"
              name="currency"
              value={formData.currency}
              onChange={handleInputChange}
              required
            />
            {errors.currency && <span className="error">{errors.currency}</span>}
          </div>

          {/* Azərbaycan adı */}
          <div className="form-group">
            <label>Azərbaycan dilində ad *</label>
            <input
              type="text"
              name="azName"
              value={formData.azName}
              onChange={handleInputChange}
              required
            />
            {errors.azName && <span className="error">{errors.azName}</span>}
          </div>

          {/* İngilis adı */}
          <div className="form-group">
            <label>İngilis dilində ad *</label>
            <input
              type="text"
              name="enName"
              value={formData.enName}
              onChange={handleInputChange}
              required
            />
            {errors.enName && <span className="error">{errors.enName}</span>}
          </div>

          {/* Rus adı */}
          <div className="form-group">
            <label>Rus dilində ad *</label>
            <input
              type="text"
              name="ruName"
              value={formData.ruName}
              onChange={handleInputChange}
              required
            />
            {errors.ruName && <span className="error">{errors.ruName}</span>}
          </div>

          {/* Təsvirlər */}
          <div className="form-group">
            <label>Azərbaycan dilində təsvir</label>
            <textarea
              name="azDescription"
              value={formData.azDescription}
              onChange={handleInputChange}
              rows={3}
            />
          </div>

          <div className="form-group">
            <label>İngilis dilində təsvir</label>
            <textarea
              name="enDescription"
              value={formData.enDescription}
              onChange={handleInputChange}
              rows={3}
            />
          </div>

          <div className="form-group">
            <label>Rus dilində təsvir</label>
            <textarea
              name="ruDescription"
              value={formData.ruDescription}
              onChange={handleInputChange}
              rows={3}
            />
          </div>

          {/* Hazırlanma müddəti */}
          <div className="form-group">
            <label>Hazırlanma müddəti (dəqiqə)</label>
            <input
              type="text"
              name="prepTimeMinutes"
              value={formData.prepTimeMinutes}
              onChange={handleInputChange}
            />
          </div>

          {/* Valyuta işarəsi */}
          <div className="form-group">
            <label>Valyuta işarəsi *</label>
            <input
              type="text"
              name="currencySign"
              value={formData.currencySign}
              onChange={handleInputChange}
              required
            />
            {errors.currencySign && <span className="error">{errors.currencySign}</span>}
          </div>

          {/* Qiymət */}
          <div className="form-group">
            <label>Qiymət *</label>
            <input
              type="number"
              name="price"
              value={formData.price}
              onChange={handleInputChange}
              step="0.01"
              min="0"
              required
            />
            {errors.price && <span className="error">{errors.price}</span>}
          </div>

          {/* Endirim qiyməti */}
          <div className="form-group">
            <label>Endirim qiyməti</label>
            <input
              type="number"
              name="discountPrice"
              value={formData.discountPrice}
              onChange={handleInputChange}
              step="0.01"
              min="0"
            />
          </div>

          {/* Menu kateqoriyası */}
          <div className="form-group">
            <label>Menu kateqoriyası *</label>
            <select
              name="menuCategoryId"
              value={formData.menuCategoryId}
              onChange={handleInputChange}
              required
            >
              <option value={0}>Seçin...</option>
              {categories.map(cat => (
                <option key={cat.id} value={cat.id}>
                  {cat.azName}
                </option>
              ))}
            </select>
            {errors.menuCategoryId && <span className="error">{errors.menuCategoryId}</span>}
          </div>

          {/* Mövcud şəkillər */}
          {existingImages.length > 0 && (
            <div className="form-group">
              <label>Mövcud şəkillər</label>
              <div className="image-preview-grid">
                {existingImages.map(img => (
                  <div key={img.id} className="image-preview-item">
                    <img src={img.path} alt="Menu item" />
                    <button
                      type="button"
                      onClick={() => handleRemoveExistingImage(img.id)}
                      className="remove-image-btn"
                    >
                      Sil
                    </button>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Yeni şəkil əlavə et */}
          <div className="form-group">
            <label>Yeni şəkillər əlavə et</label>
            <input
              type="file"
              accept="image/*"
              multiple
              onChange={handleImageChange}
            />
            {newImages.length > 0 && (
              <div className="new-images-preview">
                {newImages.map((file, index) => (
                  <div key={index} className="image-preview-item">
                    <img src={URL.createObjectURL(file)} alt={`Preview ${index}`} />
                    <button
                      type="button"
                      onClick={() => handleRemoveNewImage(index)}
                      className="remove-image-btn"
                    >
                      Sil
                    </button>
                  </div>
                ))}
              </div>
            )}
          </div>

          {/* Submit button */}
          <div className="form-actions">
            <button type="submit" disabled={submitting}>
              {submitting ? 'Yenilənir...' : 'Yenilə'}
            </button>
            <button type="button" onClick={() => navigate(-1)}>
              Ləğv et
            </button>
          </div>
        </form>
      </div>
    );
  };

  export default MenuItemUpdatePage;
  ```

  ## Routing (React Router)

  ```typescript
  // App.tsx və ya routing faylında
  import { Route, Routes } from 'react-router-dom';
  import MenuItemUpdatePage from './pages/MenuItemUpdatePage';

  <Routes>
    <Route path="/menu-items/update/:id" element={<MenuItemUpdatePage />} />
  </Routes>
  ```

  ## CSS Stil (opsional)

  ```css
  .menu-item-update-page {
    max-width: 800px;
    margin: 0 auto;
    padding: 20px;
  }

  .form-group {
    margin-bottom: 20px;
  }

  .form-group label {
    display: block;
    margin-bottom: 5px;
    font-weight: bold;
  }

  .form-group input,
  .form-group textarea,
  .form-group select {
    width: 100%;
    padding: 8px;
    border: 1px solid #ddd;
    border-radius: 4px;
  }

  .error {
    color: red;
    font-size: 12px;
    display: block;
    margin-top: 5px;
  }

  .image-preview-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(150px, 1fr));
    gap: 15px;
    margin-top: 10px;
  }

  .image-preview-item {
    position: relative;
    border: 1px solid #ddd;
    border-radius: 4px;
    overflow: hidden;
  }

  .image-preview-item img {
    width: 100%;
    height: 150px;
    object-fit: cover;
  }

  .remove-image-btn {
    position: absolute;
    top: 5px;
    right: 5px;
    background: red;
    color: white;
    border: none;
    border-radius: 50%;
    width: 25px;
    height: 25px;
    cursor: pointer;
  }

  .form-actions {
    display: flex;
    gap: 10px;
    margin-top: 20px;
  }

  .form-actions button {
    padding: 10px 20px;
    border: none;
    border-radius: 4px;
    cursor: pointer;
  }

  .form-actions button[type="submit"] {
    background: #007bff;
    color: white;
  }

  .form-actions button[type="submit"]:disabled {
    background: #ccc;
    cursor: not-allowed;
  }
  ```

  ## İstifadə

  1. Component-i import edin və route əlavə edin
  2. Menu items siyahısında "Yenilə" düyməsinə klik edəndə `/menu-items/update/{id}` route-na yönləndirin
  3. Component avtomatik olaraq menu item-ı yükləyib formu doldurur
  4. İstifadəçi dəyişiklik edib "Yenilə" düyməsinə basır
  5. Update endpoint çağırılır və uğurlu olarsa, istifadəçi menu items siyahısına yönləndirilir

  ## Qeydlər

  - `API_BASE_URL` environment variable-dan götürülməlidir
  - Token management üçün auth context və ya Redux istifadə edin
  - Şəkil yükləmə üçün preview funksionallığı əlavə edilmişdir
  - Validation həm frontend, həm də backend tərəfində aparılır
  - Mövcud şəkilləri silmək üçün ayrıca endpoint lazımdır (backend-də)
