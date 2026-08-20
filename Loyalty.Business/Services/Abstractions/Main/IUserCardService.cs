using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IUserCardService
    {
        Task<ServiceResult> GetMyCardsAsync(int currentPage, int pageSize, TenantSearchDto model, double? userLat = null, double? userLng = null, CancellationToken ct = default);
        Task<ServiceResult> GetGeofencePointsAsync(double? lat, double? lng, double radiusKm, CancellationToken ct = default);
        Task<ServiceResult> Delete(int id);
    }
}
