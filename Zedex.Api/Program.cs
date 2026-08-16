using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Zedex.Application.Common;
using Zedex.Infrastructure.Identity;
using Zedex.Infrastructure.Persistence;
using Zedex.Api.Services;

// Single-timezone business app: store & display local time (consistent with Zedex.Web).
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// ── Database ──────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── ASP.NET Identity (user validation only — no cookie sign-in for API) ───────
// We use AddIdentityCore instead of AddIdentity to avoid registering cookie auth.
builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.Password.RequiredLength = 6;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// ── JWT Bearer Authentication ─────────────────────────────────────────────────
var jwtSection = builder.Configuration.GetSection("Jwt");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer           = true,
        ValidateAudience         = true,
        ValidateLifetime         = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer              = jwtSection["Issuer"],
        ValidAudience            = jwtSection["Audience"],
        IssuerSigningKey         = new SymmetricSecurityKey(
                                       Encoding.UTF8.GetBytes(jwtSection["Secret"]!)),
        ClockSkew = TimeSpan.Zero   // Tokens expire exactly at the stated time
    };
});

builder.Services.AddAuthorization();

// ── CORS — allow mobile app (any origin in dev; tighten in production) ────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("MobileApp", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// ── Application services ──────────────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, ApiCurrentUserService>();
builder.Services.AddScoped<ITokenService, TokenService>();

// ── Controllers (no AntiForgery — REST API, not MVC form app) ─────────────────
builder.Services.AddControllers();

// ── Swagger / OpenAPI with JWT support ────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title   = "Zedex Business API",
        Version = "v1",
        Description = "Mobile API for ZedexBusiness — Stock, Bills, Customer Ledger"
    });

    // Adds the "Authorize" button in Swagger UI so you can test protected endpoints.
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description  = "Enter your JWT token below. Format: Bearer {token}",
        Name         = "Authorization",
        In           = ParameterLocation.Header,
        Type         = SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                    { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// ── Request pipeline ──────────────────────────────────────────────────────────
// Pin culture (consistent with Zedex.Web)
var culture = new System.Globalization.CultureInfo("en-US");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture  = new Microsoft.AspNetCore.Localization.RequestCulture(culture),
    SupportedCultures      = new[] { culture },
    SupportedUICultures    = new[] { culture }
});

// Always show Swagger (useful while the mobile app is being built).
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Zedex API v1");
    c.RoutePrefix = string.Empty;   // Swagger UI at root /
});

app.UseCors("MobileApp");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ── Run EF migrations on startup ──────────────────────────────────────────────
// Safe to run alongside Zedex.Web because EF migrations are idempotent.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

app.Run();
