using System.Text;
using Loyalty.Api.Infrastructure;
using Loyalty.Application.Auth;
using Loyalty.Core.Domain.Security;
using Loyalty.Infrastructure;
using Loyalty.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Persistencia PostgreSQL multi-tenant (filtro global + retry en failover).
builder.Services.AddPersistence(builder.Configuration);

// Servicios de aplicación (modelo híbrido + caché QR Redis).
builder.Services.AddLoyaltyApplication();

// Redis para caché distribuida de alta concurrencia y validación QR instantánea (IDistributedCache).
builder.Services.AddStackExchangeRedisCache(options =>
{
    var redisConfig = builder.Configuration.GetSection("Redis");
    options.Configuration = redisConfig["Configuration"];
    options.InstanceName = redisConfig["InstanceName"];
});

// Multiplexer Redis para operaciones atómicas anti-fuerza-bruta (INCR/EXPIRE).
// POR QUÉ no reutilizar IDistributedCache para throttling: no expone INCR atómico; el
// multiplexer da acceso a comandos crudos y comparte la misma conexión que el cache.
var redisConnectionString = builder.Configuration.GetSection("Redis")["Configuration"] ?? "127.0.0.1:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnectionString));

// --- Servicios de Auth ---
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddScoped<IPasswordHasherService, PasswordHasherService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<ILoginThrottleService, LoginThrottleService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICurrentUser, CurrentUserClaims>();
builder.Services.AddHttpContextAccessor();

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Sección Jwt no configurada.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

// RBAC por rol (policies). El cajero solo procesa ventas; TenantAdmin gestiona su tenant;
// SuperAdmin gestiona en todo el sistema.
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SuperAdminOnly", p => p.RequireRole("SuperAdmin"));
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS: la PWA (Next.js en :3000) consume la API desde otro origen. Política nombrada y
// restrictiva (solo métodos/headers necesarios); en producción se limita a los dominios de marca.
builder.Services.AddCors(options =>
{
    options.AddPolicy("PwaClient", policy => policy
        .WithOrigins(
            builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? new[] { "http://localhost:3000", "http://127.0.0.1:3000" })
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

// Manejo centralizado de excepciones (lo más temprano posible para capturar todo).
app.UseMiddleware<Loyalty.Api.Infrastructure.ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseMiddleware<ProfileMiddleware>(); // mide tiempo real de procesamiento (solo Dev)
}

// CORS antes de autenticación: las preflight OPTIONS no llevan token y deben responder.
app.UseCors("PwaClient");

// Autenticación (puebla ClaimsPrincipal) ANTES del tenant-context middleware.
app.UseAuthentication();
app.UseMiddleware<Loyalty.Api.Infrastructure.TenantContextMiddleware>();
app.UseAuthorization();

app.MapControllers();

// Seed de usuarios STAFF iniciales (idempotente) al arrancar.
using (var scope = app.Services.CreateScope())
{
    await Loyalty.Infrastructure.Auth.AuthSeeder.SeedAsync(scope);
}

app.Run();

/// <summary>Punto de entrada; expuesto para tests de integración.</summary>
public partial class Program { }