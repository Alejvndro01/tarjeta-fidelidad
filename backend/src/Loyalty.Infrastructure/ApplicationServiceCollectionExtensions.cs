using Loyalty.Application.Admin;
using Loyalty.Application.Coupons;
using Loyalty.Application.Loyalty;
using Loyalty.Application.Members;
using Loyalty.Application.Wallets;
using Loyalty.Infrastructure.Admin;
using Loyalty.Infrastructure.Coupons;
using Loyalty.Infrastructure.Loyalty;
using Loyalty.Infrastructure.Members;
using Loyalty.Infrastructure.Wallets;
using Microsoft.Extensions.DependencyInjection;

namespace Loyalty.Infrastructure;

/// <summary>Registra los servicios de aplicación (scoped: dependen del DbContext por request).</summary>
public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddLoyaltyApplication(this IServiceCollection services)
    {
        // Scoped por request: cada servicio usa el DbContext y Redis del request.
        services.AddScoped<ISaleProcessingService, SaleProcessingService>();
        services.AddScoped<IProgramCacheService, ProgramCacheService>();
        services.AddScoped<IMemberQrCacheService, MemberQrCacheService>();
        services.AddScoped<IMemberService, MemberService>();
        services.AddScoped<IWalletPassService, WalletPassService>();
        services.AddSingleton<DevelopmentSigningCertificate>();
        services.AddScoped<ICouponService, CouponService>();
        services.AddScoped<IAdminService, AdminService>();
        return services;
    }
}