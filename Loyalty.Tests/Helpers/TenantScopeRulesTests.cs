using FluentAssertions;
using Loyalty.Business.Helpers;

namespace Loyalty.Tests.Helpers;

/// <summary>Tenant konteksti — işçi header/sorğu ilə başqa tenant-a keçə bilməz; super admin və müştəri axınları dəyişmir.</summary>
public class TenantScopeRulesTests
{
    [Theory]
    // superAdmin, staff, claim, header, cookie → expected
    [InlineData(false, true, 16, 56, null, 16)]   // tenant admin header ilə başqa tenant-a keçə bilməz
    [InlineData(false, true, 16, null, null, 16)]
    [InlineData(true, false, null, 56, null, 56)] // super admin header ilə seçir
    [InlineData(true, false, null, null, null, null)]
    [InlineData(false, false, 16, 39, null, 39)]  // müştəri (app) başqa restorana baxır — header bugünkü kimi
    [InlineData(false, false, null, null, 7, 7)]  // anonim — cookie
    [InlineData(false, true, null, 56, null, 56)] // claim-siz köhnə işçi tokeni — bugünkü davranış
    public void Resolve(bool superAdmin, bool staff, int? claim, int? header, int? cookie, int? expected)
        => TenantScopeRules.Resolve(superAdmin, staff, claim, header, cookie).Should().Be(expected);

    [Theory]
    // superAdmin, staff, query, current → expected
    [InlineData(false, true, 56, 16, 16)]     // ?tenantId=56 tenant admin üçün ignor
    [InlineData(true, false, 56, null, 56)]   // super admin sorğu ilə seçir
    [InlineData(false, false, 39, 16, 39)]    // müştəri axını dəyişmir
    [InlineData(false, true, null, 16, 16)]
    public void ResolveQueryOverride(bool superAdmin, bool staff, int? query, int? current, int? expected)
        => TenantScopeRules.ResolveQueryOverride(superAdmin, staff, query, current).Should().Be(expected);
}
