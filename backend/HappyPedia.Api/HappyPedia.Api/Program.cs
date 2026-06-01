using System.Text;
using System.Text.Json.Serialization;
using HappyPedia.Api.Data;
using HappyPedia.Api.Models;
using HappyPedia.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Connection String prüfen
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Connection string 'DefaultConnection' fehlt in appsettings.json.");
}

// JWT prüfen
var jwtKey = builder.Configuration["Jwt:Key"];

if (string.IsNullOrWhiteSpace(jwtKey))
{
    throw new InvalidOperationException("Jwt:Key fehlt in appsettings.json.");
}

if (Encoding.UTF8.GetBytes(jwtKey).Length < 32)
{
    throw new InvalidOperationException("Jwt:Key muss mindestens 32 Zeichen lang sein.");
}

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "HappyPedia.Api";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "HappyPedia.Frontend";

// Controller + JSON Einstellungen
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

// Swagger mit JWT-Unterstützung
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Token eingeben. Beispiel: eyJhbGciOiJIUzI1NiIs..."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
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

// Datenbank
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// Cache
builder.Services.AddMemoryCache();

// Eigene Services
builder.Services.AddScoped<KeywordService>();
builder.Services.AddScoped<JwtTokenService>();

builder.Services.AddHttpClient<RssImportService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services.AddHttpClient<OpenAiScoringService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});

// Passwort-Hasher
builder.Services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();

// JWT Authentication
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,

            ValidateAudience = true,
            ValidAudience = jwtAudience,

            ValidateLifetime = true,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),

            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

// Authorization mit Admin-Regel
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
    {
        policy.RequireAuthenticatedUser();

        policy.RequireAssertion(context =>
            string.Equals(
                context.User.Identity?.Name,
                "admin",
                StringComparison.OrdinalIgnoreCase
            )
            ||
            context.User.Claims.Any(claim =>
                claim.Type == "username" &&
                string.Equals(
                    claim.Value,
                    "admin",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            ||
            context.User.Claims.Any(claim =>
                claim.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name" &&
                string.Equals(
                    claim.Value,
                    "admin",
                    StringComparison.OrdinalIgnoreCase
                )
            )
        );
    });
});

// Health Check
builder.Services.AddHealthChecks();

// Saubere Fehlerantworten
builder.Services.AddProblemDetails();

// CORS für Vue-Frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithOrigins(
                "http://localhost:5173",
                "http://localhost:5174",
                "https://localhost:5173",
                "https://localhost:5174"
            );
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    app.MapGet("/", () => Results.Redirect("/swagger"));
}
else
{
    app.UseExceptionHandler("/error");
}

app.Map("/error", () =>
    Results.Problem("Ein interner Serverfehler ist aufgetreten."));

app.UseHttpsRedirection();

app.UseCors("Frontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health");

app.Run();