using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using Vendify.Application.Services.Interfaces;
using Vendify.Infrastructure.Data;
using Vendify.Infrastructure.Helpers;
using Vendify.Infrastructure.Services.Implementations;

var builder = WebApplication.CreateBuilder(args);

// ── PORT (Render only) ───────────────────────────────────
var isProduction = builder.Environment.IsProduction();
if (isProduction)
{
    var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// ── Database ─────────────────────────────────────────────
builder.Services.AddDbContext<VendifyDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// ── JWT ──────────────────────────────────────────────────
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secret = jwtSettings["Secret"]
    ?? Environment.GetEnvironmentVariable("JWT_SECRET")
    ?? throw new Exception("JWT Secret not configured");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme =
        JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme =
        JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(secret)),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtSettings["Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine(
                $"❌ JWT Auth Failed: {context.Exception.Message}");
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            Console.WriteLine("✅ JWT Token Validated");
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// ── CORS ─────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("VendifyPolicy", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

// ── HttpClient ───────────────────────────────────────────
builder.Services.AddHttpClient();

// ── Stripe ───────────────────────────────────────────────
Stripe.StripeConfiguration.ApiKey =
    builder.Configuration["Stripe:SecretKey"]
    ?? Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY");

// ── File Upload Limits ───────────────────────────────────
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 5 * 1024 * 1024; // 5MB
});

// ── Dependency Injection ─────────────────────────────────
builder.Services.AddScoped<JwtHelper>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IStoreService, StoreService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IShippingService, ShippingService>();
builder.Services.AddScoped<ICouponService, CouponService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IUploadService, UploadService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IThemeService, ThemeService>();

// ── Controllers ──────────────────────────────────────────
builder.Services.AddControllers();

// ── Swagger ──────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Vendify API",
        Version = "v1",
        Description = "Vendify — Easy Online Store Builder API"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Enter: Bearer {your token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// ── Migrations ───────────────────────────────────────────
using (var migrationScope = app.Services.CreateScope())
{
    try
    {
        var db = migrationScope.ServiceProvider
            .GetRequiredService<VendifyDbContext>();
        db.Database.Migrate();
        Console.WriteLine("✅ Database migrations applied");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️ Migration warning: {ex.Message}");
    }
}

// ── Middleware ───────────────────────────────────────────
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Vendify API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors("VendifyPolicy");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();