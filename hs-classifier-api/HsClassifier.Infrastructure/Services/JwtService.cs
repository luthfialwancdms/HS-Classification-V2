using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HsClassifier.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace HsClassifier.Infrastructure.Services;

public class JwtService(IConfiguration config) : IJwtService
{
    private string Key  => config["Jwt:Key"]      ?? throw new InvalidOperationException("Jwt:Key missing");
    private string Iss  => config["Jwt:Issuer"]   ?? "HsClassifierAPI";
    private string Aud  => config["Jwt:Audience"] ?? "HsClassifierClient";
    private int    Hours => int.TryParse(config["Jwt:ExpiryHours"], out var h) ? h : 8;

    public string GenerateToken(int userId, string email, string role)
    {
        var key     = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key));
        var creds   = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims  = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Email,          email),
            new Claim(ClaimTypes.Role,           role),
        };
        var token = new JwtSecurityToken(Iss, Aud, claims,
            expires: DateTime.UtcNow.AddHours(Hours),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public (bool Valid, int UserId, string Email, string Role) ValidateToken(string token)
    {
        try
        {
            var key     = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key));
            var handler = new JwtSecurityTokenHandler();
            handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey        = key,
                ValidateIssuer          = true,
                ValidIssuer             = Iss,
                ValidateAudience        = true,
                ValidAudience           = Aud,
                ClockSkew               = TimeSpan.Zero,
            }, out var validated);

            var jwt    = (JwtSecurityToken)validated;
            var userId = int.Parse(jwt.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value);
            var email  = jwt.Claims.First(c => c.Type == ClaimTypes.Email).Value;
            var role   = jwt.Claims.First(c => c.Type == ClaimTypes.Role).Value;
            return (true, userId, email, role);
        }
        catch { return (false, 0, string.Empty, string.Empty); }
    }
}
