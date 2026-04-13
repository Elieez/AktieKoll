using System.Text;
using AktieKoll.Data;
using AktieKoll.Extensions;
using AktieKoll.Interfaces;
using AktieKoll.Models;
using AktieKoll.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigin")
    .Get<string[]>() ?? ["http://localhost:3000"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowNextApp", policy =>
    {
        policy
          .WithOrigins(allowedOrigins)
          .AllowCredentials()
          .AllowAnyMethod()
          .AllowAnyHeader();
    });
});

builder.Services.AddControllers();
builder.Services.AddCustomRateLimiting(builder.Configuration);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AktieKoll API",
        Version = "v1",
        Description = "Swedish insider trading data API",
        Contact = new OpenApiContact
        {
            Name = "AktieKoll",
            Url = new Uri("https://github.com/elieez/aktiekoll")
        }
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});


if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("PostgresConnection")));
}

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

// Token lifespan: password-reset / email-verification tokens valid 24 h by default.
// Account-deletion tokens are short-lived (1 h) and stored hashed on the user row.
builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
    options.TokenLifespan = TimeSpan.FromHours(24));

// JWT config
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrEmpty(jwtKey))
    throw new InvalidOperationException("JWT Key missing from configuration.");

var requireHttps = builder.Configuration.GetValue<bool>("CookieSettings:Secure");
var keyBytes = Encoding.UTF8.GetBytes(jwtKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
    // External sign-in (Google) needs a cookie scheme as intermediate
    options.DefaultSignInScheme       = IdentityConstants.ExternalScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = requireHttps;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer           = true,
        ValidateAudience         = true,
        ValidateIssuerSigningKey = true,
        ValidateLifetime         = true,
        ValidIssuer              = builder.Configuration["Jwt:Issuer"],
        ValidAudience            = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey         = new SymmetricSecurityKey(keyBytes),
    };
})
.AddGoogle(options =>
{
    // Required env vars: Google__ClientId  Google__ClientSecret
    options.ClientId     = builder.Configuration["Google:ClientId"]     ?? throw new InvalidOperationException("Google:ClientId missing.");
    options.ClientSecret = builder.Configuration["Google:ClientSecret"] ?? throw new InvalidOperationException("Google:ClientSecret missing.");
    options.CallbackPath = "/api/auth/google/callback";   // handled by the Google middleware
    options.SignInScheme = IdentityConstants.ExternalScheme;
    options.Scope.Add("profile");
    options.Scope.Add("email");
    options.SaveTokens = true;
});

// Cookie config for Identity external scheme
builder.Services.Configure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, options =>
{
    options.Cookie.Name       = "AktieKollAuthCookie";
    options.Cookie.SecurePolicy = requireHttps ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
    options.Cookie.HttpOnly   = true;
    options.Cookie.SameSite   = requireHttps ? SameSiteMode.None : SameSiteMode.Lax;
    options.ExpireTimeSpan    = TimeSpan.FromMinutes(30);
    options.SlidingExpiration = true;
});

builder.Services.Configure<FormOptions>(options =>
    options.MultipartBodyLengthLimit = 10485760);


builder.WebHost.ConfigureKestrel(options =>
    options.Limits.MaxRequestBodySize = 10485760);

builder.Services.AddScoped<ITokenService,    TokenService>();
builder.Services.AddScoped<IAuthService,     AuthService>();
builder.Services.AddScoped<IEmailService,    EmailService>();

builder.Services.AddHttpClient<CsvFetchService>();
builder.Services.AddHttpClient<IDiscordService, DiscordService>();

// Suppress HttpClient request/response URL logging to prevent Discord webhook URLs from appearing in logs
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddTransient<ISymbolService, SymbolService>();
builder.Services.AddHostedService<RefreshTokenCleanupService>();
builder.Services.AddScoped<IInsiderTradeService, InsiderTradeService>();
builder.Services.AddScoped<ICompanyService, CompanyService>();
builder.Services.AddScoped<IFollowService, FollowService>();
builder.Services.AddScoped<INotificationPreferencesService, NotificationPreferencesService>();

builder.Services.AddResponseCaching();
builder.Services.AddMemoryCache();

var app = builder.Build();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Staging"))
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "AktieKoll API v1");
        options.RoutePrefix = "swagger";
    });
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.StatusCode  = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new
            {
                type   = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
                title  = "An unexpected error occurred.",
                status = 500
            });
        });
    });
    app.UseHsts();
}

app.UseCors("AllowNextApp");
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseResponseCaching();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
