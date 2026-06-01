using HappyPedia.Api.Data;
using HappyPedia.Api.Models;
using HappyPedia.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace HappyPedia.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<AppUser> _passwordHasher;
    private readonly JwtTokenService _jwtTokenService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        AppDbContext db,
        IPasswordHasher<AppUser> passwordHasher,
        JwtTokenService jwtTokenService,
        ILogger<AuthController> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var username = NormalizeUsername(request.Username);

        if (string.IsNullOrWhiteSpace(username))
        {
            return BadRequest(new { message = "Username ist Pflicht." });
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Passwort ist Pflicht." });
        }

        if (request.Password.Length < 8)
        {
            return BadRequest(new { message = "Das Passwort muss mindestens 8 Zeichen lang sein." });
        }

        var usernameLower = username.ToLower();

        var exists = await _db.Users
            .AsNoTracking()
            .AnyAsync(u => u.Username.ToLower() == usernameLower, cancellationToken);

        if (exists)
        {
            return BadRequest(new { message = "Username existiert bereits." });
        }

        var user = new AppUser
        {
            Username = username
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        var token = _jwtTokenService.GenerateToken(user);

        _logger.LogInformation("Neuer User registriert: {Username}", user.Username);

        return Ok(new AuthResponse(
            user.Id,
            user.Username,
            token
        ));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var username = NormalizeUsername(request.Username);

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Username und Passwort sind Pflicht." });
        }

        var usernameLower = username.ToLower();

        var user = await _db.Users
            .FirstOrDefaultAsync(
                u => u.Username.ToLower() == usernameLower,
                cancellationToken);

        if (user == null)
        {
            return Unauthorized(new { message = "Username oder Passwort falsch." });
        }

        var result = _passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            request.Password);

        if (result == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new { message = "Username oder Passwort falsch." });
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var token = _jwtTokenService.GenerateToken(user);

        _logger.LogInformation("User eingeloggt: {Username}", user.Username);

        return Ok(new AuthResponse(
            user.Id,
            user.Username,
            token
        ));
    }

    private static string NormalizeUsername(string? username)
    {
        return username?.Trim() ?? string.Empty;
    }
}

public record RegisterRequest(
    [Required] string Username,
    [Required] string Password
);

public record LoginRequest(
    [Required] string Username,
    [Required] string Password
);

public record AuthResponse(
    int Id,
    string Username,
    string Token
);