using BrandonFintech.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BrandonFintech.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/admin/dev")]
public class AdminDevController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _environment;

    public AdminDevController(ApplicationDbContext db, IWebHostEnvironment environment)
    {
        _db = db;
        _environment = environment;
    }

    [HttpPost("promote")]
    public async Task<IActionResult> PromoteCurrentUser()
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound(new
            {
                success = false,
                message = "Endpoint not available"
            });
        }

        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new
            {
                success = false,
                message = "Invalid token"
            });
        }

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user == null)
        {
            return Unauthorized(new
            {
                success = false,
                message = "User not found"
            });
        }

        user.Role = "Admin";

        await _db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = "User promoted to Admin. Login again to receive an admin JWT.",
            user = new
            {
                user.Id,
                user.Email,
                user.Role
            }
        });
    }

    private bool TryGetUserId(out Guid userId)
    {
        var userIdValue =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("sub");

        return Guid.TryParse(userIdValue, out userId);
    }
}
