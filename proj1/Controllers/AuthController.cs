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
        var email = (request.Email ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new { message = "Email address is required." });

        if (string.IsNullOrWhiteSpace(request.FullName))
            return BadRequest(new { message = "Full name is required." });

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 3)
            return BadRequest(new { message = "Password must be at least 3 characters." });

        var role = request.Role?.Trim().ToUpperInvariant() ?? PlatformRoles.Customer;
        if (role is not (PlatformRoles.Customer or PlatformRoles.Agent))
            role = PlatformRoles.Agent;

        var agency = string.IsNullOrWhiteSpace(request.AgencyName)
            ? (string.IsNullOrWhiteSpace(request.FullName) ? "Independent Seller / Agent" : $"{request.FullName.Trim()} (Seller)")
            : request.AgencyName.Trim();

        var profile = await db.Profiles.SingleOrDefaultAsync(p => p.Email == email);
        if (profile is null)
        {
            profile = new Profile
            {
                Email = email,
                FullName = request.FullName.Trim(),
                Phone = request.Phone?.Trim(),
                Role = role,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            profile.PasswordHash = passwordHasher.HashPassword(profile, request.Password);
            db.Profiles.Add(profile);
            await db.SaveChangesAsync();
        }
        else
        {
            // Existing profile: update details, role, and password so user is never locked out
            profile.FullName = request.FullName.Trim();
            profile.Role = role;
            if (!string.IsNullOrWhiteSpace(request.Phone)) profile.Phone = request.Phone.Trim();
            profile.PasswordHash = passwordHasher.HashPassword(profile, request.Password);
            profile.IsActive = true;
            profile.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        if (role == PlatformRoles.Agent)
        {
            var agent = await db.Agents.SingleOrDefaultAsync(a => a.UserId == profile.Id);
            if (agent is null)
            {
                db.Agents.Add(new Agent
                {
                    UserId = profile.Id,
                    AgencyName = agency,
                    LicenseNumber = request.LicenseNumber?.Trim(),
                    Phone = profile.Phone,
                    City = request.City?.Trim(),
                    State = request.State?.Trim(),
                    VerificationStatus = PlatformStatuses.AgentApproved
                });
            }
            else
            {
                agent.AgencyName = agency;
                if (!string.IsNullOrWhiteSpace(request.City)) agent.City = request.City.Trim();
                if (!string.IsNullOrWhiteSpace(request.State)) agent.State = request.State.Trim();
                if (!string.IsNullOrWhiteSpace(request.LicenseNumber)) agent.LicenseNumber = request.LicenseNumber.Trim();
                agent.VerificationStatus = PlatformStatuses.AgentApproved;
                agent.UpdatedAt = DateTime.UtcNow;
            }
            await db.SaveChangesAsync();
        }

        await SignIn(profile);
        return Ok(ToResponse(profile));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var email = (request.Email ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new { message = "Please enter your email address." });

        if (string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Please enter your password." });

        var profile = await db.Profiles.SingleOrDefaultAsync(p => p.Email == email);
        if (profile is null || !profile.IsActive)
            return Unauthorized(new { message = "No account found with this email. Please check your spelling or register a new account." });

        var verifyResult = passwordHasher.VerifyHashedPassword(profile, profile.PasswordHash, request.Password);
        if (verifyResult == PasswordVerificationResult.Failed)
            return Unauthorized(new { message = "Incorrect password. You can re-register using the Create Account tab to set a new password, or use quick demo login." });

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
    [Required] public string FullName { get; init; } = string.Empty;
    [Required] public string Email { get; init; } = string.Empty;
    [Required] public string Password { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string Role { get; init; } = PlatformRoles.Customer;
    public string? AgencyName { get; init; }
    public string? LicenseNumber { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
}

public sealed class LoginRequest
{
    [Required] public string Email { get; init; } = string.Empty;
    [Required] public string Password { get; init; } = string.Empty;
}

public sealed record ProfileResponse(Guid Id, string FullName, string Email, string Role);
