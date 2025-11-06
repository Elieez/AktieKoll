using AktieKoll.Data;
using AktieKoll.Interfaces;
using AktieKoll.Models;
using AktieKoll.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowNextApp", policy =>
    {
        policy
          .WithOrigins("http://localhost:3000")
          .AllowCredentials()
          .AllowAnyMethod()
          .AllowAnyHeader();
    });
});

builder.Services.AddControllers();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PostgresConnection")));

// Identity 
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = false;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// JWT config
var jwtKey = builder.Configuration["Jwt:Key"];
var keyBytes = Encoding.UTF8.GetBytes(jwtKey ?? throw new Exception("JWT Key missing"));
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = true;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
    };
});

// Cookie config
builder.Services.Configure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, options =>
{
    options.Cookie.Name = "AktieKollAuthCookie";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.Domain = ".dev.local";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.SlidingExpiration = true;
});

builder.Services.AddScoped<ITokenService, TokenService>();

builder.Services.AddHttpClient<CsvFetchService>();
builder.Services.AddHttpClient<IOpenFigiService, OpenFigiService>(client =>
    client.BaseAddress = new Uri("https://api.openfigi.com/v3/"));
builder.Services.AddTransient<ISymbolService, SymbolService>();

builder.Services.AddScoped<IInsiderTradeService, InsiderTradeService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseCors("AllowNextApp");

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllers();

app.Run();