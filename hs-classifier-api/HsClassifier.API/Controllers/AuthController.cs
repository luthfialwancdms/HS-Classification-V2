using HsClassifier.Application.Interfaces;
using HsClassifier.Domain.Entities;
using HsClassifier.Domain.Enums;
using HsClassifier.Infrastructure.Persistence;
using HsClassifier.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HsClassifier.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(HsClassifierDbContext db, IJwtService jwt) : ControllerBase
{
    /// <summary>Login; returns a JWT to send as <c>Authorization: Bearer</c>.</summary>
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { message = "Email and password required." });

        var user = await db.AppUsers.FirstOrDefaultAsync(u => u.Email == req.Email && u.IsActive);
        if (user is null || !PasswordHashing.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new { message = "Email atau password salah." });

        // Transparently upgrade legacy hashes on successful login
        if (!user.PasswordHash.StartsWith("pbkdf2$"))
        {
            user.PasswordHash = PasswordHashing.Hash(req.Password);
            await db.SaveChangesAsync();
        }

        var token = jwt.GenerateToken(user.Id, user.Email, user.Role.ToString());
        return Ok(new { token, role = user.Role.ToString(), userId = user.Id, email = user.Email });
    }

    /// <summary>Create a user. Admin only — self-registration would let anyone grant themselves Admin.</summary>
    [Authorize(Roles = nameof(UserRole.Admin))]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 8)
            return BadRequest(new { message = "Email wajib diisi dan password minimal 8 karakter." });
        if (!Enum.TryParse<UserRole>(req.Role, true, out var role))
            return BadRequest(new { message = $"Role tidak valid: {req.Role}" });
        if (await db.AppUsers.AnyAsync(u => u.Email == req.Email))
            return Conflict(new { message = "Email already registered." });

        var user = new AppUser { Email = req.Email, PasswordHash = PasswordHashing.Hash(req.Password), Role = role, IsActive = true };
        db.AppUsers.Add(user);
        await db.SaveChangesAsync();
        return Ok(new { userId = user.Id, email = user.Email, role = user.Role.ToString() });
    }
}

public record LoginRequest(string Email, string Password);
public record RegisterRequest(string Email, string Password, string Role = "Classifier");
