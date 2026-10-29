namespace HsClassifier.Application.Interfaces;

public interface IJwtService
{
    string GenerateToken(int userId, string email, string role);
    (bool Valid, int UserId, string Email, string Role) ValidateToken(string token);
}
