// Program.cs
using Microsoft.EntityFrameworkCore;
using Amethral.Api.Data;
using Amethral.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    // 1. Définir le schéma de sécurité (Bearer)
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. \r\n\r\n Enter 'Bearer' [space] and then your token in the text input below.\r\n\r\nExample: \"Bearer 12345abcdef\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    // 2. Appliquer ce schéma à tous les endpoints
    c.AddSecurityRequirement(new OpenApiSecurityRequirement()
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "oauth2",
                Name = "Bearer",
                In = ParameterLocation.Header,
            },
            new List<string>()
        }
    });
});

// DB Context
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Service Métier
builder.Services.AddScoped<AuthService>();

// CORS (Important pour que Vue.js puisse appeler l'API)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWeb",
        policy => policy.WithOrigins(builder.Configuration["FrontendUrl"] ?? "http://localhost:4200") // Ton URL Vue.js
                        .AllowAnyMethod()
                        .AllowAnyHeader());
});

var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey is not configured.");
var key = Encoding.UTF8.GetBytes(secretKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    // Cookie configuration for OAuth sign-in
    options.Cookie.Name = "Amethral.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.None; // TODO: Set to Always in production
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.SlidingExpiration = true;
})
.AddJwtBearer(options =>
{
    options.MapInboundClaims = false; // Disable automatic claim mapping
    options.RequireHttpsMetadata = false; //TODO: Mettre true en prod
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtSettings["Audience"],
        ClockSkew = TimeSpan.Zero // Pas de délai de grâce pour l'expiration
    };
})
.AddGoogle(googleOptions =>
{
    googleOptions.ClientId = builder.Configuration["OAuth:Google:ClientId"] ?? "";
    googleOptions.ClientSecret = builder.Configuration["OAuth:Google:ClientSecret"] ?? "";
    googleOptions.SaveTokens = true;
    googleOptions.SignInScheme = Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddDiscord(discordOptions =>
{
    discordOptions.ClientId = builder.Configuration["OAuth:Discord:ClientId"] ?? "";
    discordOptions.ClientSecret = builder.Configuration["OAuth:Discord:ClientSecret"] ?? "";
    discordOptions.CallbackPath = "/api/auth/oauth/discord/callback";
    discordOptions.Scope.Add("identify");
    discordOptions.Scope.Add("email");
    discordOptions.SaveTokens = true;
    discordOptions.SignInScheme = Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme;
});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowWeb");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();