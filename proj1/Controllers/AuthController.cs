using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using proj1.Data;
using proj1.Models;

namespace proj1.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(ApplicationDbContext db, IPasswordHasher<Profile> passwordHasher) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Profiles.AnyAsync(p => p.Email == email))
            return Conflict(new { message = "An account with this email already exists." });

        var role = request.Role?.Trim().ToUpperInvariant();
        if (role is not (PlatformRoles.Customer or PlatformRoles.Agent))
            return BadRequest(new { message = "Choose either CUSTOMER or AGENT." });
        if (role == PlatformRoles.Agent && string.IsNullOrWhiteSpace(request.AgencyName))
            return BadRequest(new { message = "Agency name is required for agent registration." });

        var profile = new Profile
        {
            Email = email,
            FullName = request.FullName.Trim(),
            Phone = request.Phone?.Trim(),
            Role = role,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        profile.PasswordHash = passwordHasher.HashPassword(profile, request.Password);
        db.Profiles.Add(profile);

        if (role == PlatformRoles.Agent)
        {
            db.Agents.Add(new Agent
            {
                UserId = profile.Id,
                AgencyName = request.AgencyName!.Trim(),
                LicenseNumber = request.LicenseNumber?.Trim(),
                Phone = profile.Phone,
                City = request.City?.Trim(),
                State = request.State?.Trim(),
                VerificationStatus = PlatformStatuses.AgentPending
            });
        }

        await db.SaveChangesAsync();
        return Created("/api/auth/login", new { message = "Account created. Please sign in to continue." });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var profile = await db.Profiles.SingleOrDefaultAsync(p => p.Email == request.Email.Trim().ToLowerInvariant());
        if (profile is null || !profile.IsActive || passwordHasher.VerifyHashedPassword(profile, profile.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
            return Unauthorized(new { message = "Email or password is incorrect." });

        await SignIn(profile);
        return Ok(ToResponse(profile));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
            return Unauthorized();
        var profile = await db.Profiles.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id && p.IsActive);
        return profile is null ? Unauthorized() : Ok(ToResponse(profile));
    }

    private async Task SignIn(Profile profile)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, profile.Id.ToString()),
            new Claim(ClaimTypes.Name, profile.FullName),
            new Claim(ClaimTypes.Email, profile.Email),
            new Claim(ClaimTypes.Role, profile.Role)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity), new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(12)
            });
    }

    private static ProfileResponse ToResponse(Profile profile) => new(profile.Id, profile.FullName, profile.Email, profile.Role);
}

public sealed class RegisterRequest
{
    [Required, StringLength(120, MinimumLength = 2)] public string FullName { get; init; } = string.Empty;
    [Required, EmailAddress, StringLength(320)] public string Email { get; init; } = string.Empty;
    [Required, StringLength(128, MinimumLength = 10)] public string Password { get; init; } = string.Empty;
    [StringLength(40)] public string? Phone { get; init; }
    [Required] public string Role { get; init; } = PlatformRoles.Customer;
    [StringLength(160)] public string? AgencyName { get; init; }
    [StringLength(100)] public string? LicenseNumber { get; init; }
    [StringLength(100)] public string? City { get; init; }
    [StringLength(100)] public string? State { get; init; }
}

public sealed class LoginRequest
{
    [Required, EmailAddress] public string Email { get; init; } = string.Empty;
    [Required] public string Password { get; init; } = string.Empty;
}

public sealed record ProfileResponse(Guid Id, string FullName, string Email, string Role);
