using BrandonFintech.Identity;

namespace BrandonFintech.Api.Services;

public interface IJwtService
{
    string GenerateToken(User user);
}
