using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using proj1.Data;
using proj1.Models;
using proj1.Services;

namespace proj1.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = PlatformRoles.Admin)]
public class AdminController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet("profiles")]
    public async Task<IActionResult> Profiles() => Ok(await db.Profiles.AsNoTracking()
        .OrderBy(p => p.FullName).Select(p => new { p.Id, p.Email, p.FullName, p.Phone, p.Role, p.IsActive, p.CreatedAt }).ToListAsync());

    [HttpGet("audit-logs")]
    public async Task<IActionResult> AuditLogs() => Ok(await db.AuditLogs.AsNoTracking().Include(x => x.User)
        .OrderByDescending(x => x.CreatedAt).Take(100)
        .Select(x => new { x.Id, x.Action, x.EntityType, x.EntityId, UserName = x.User == null ? null : x.User.FullName, x.CreatedAt })
        .ToListAsync());

    [HttpGet("agents")]
    public async Task<IActionResult> Agents() => Ok(await db.Agents.AsNoTracking().Include(a => a.Profile)
        .OrderBy(a => a.VerificationStatus).Select(a => new { a.Id, a.UserId, a.AgencyName, a.LicenseNumber, a.Phone, a.City, a.State, a.VerificationStatus, Name = a.Profile!.FullName, Email = a.Profile.Email }).ToListAsync());

    [HttpPut("agents/{id:guid}/verification")]
    public async Task<IActionResult> VerifyAgent(Guid id, VerifyAgentRequest request)
    {
        var agent = await db.Agents.Include(a => a.Profile).SingleOrDefaultAsync(a => a.Id == id);
        if (agent is null) return NotFound();
        var status = request.Status.Trim().ToLowerInvariant();
        if (status is not (PlatformStatuses.AgentApproved or PlatformStatuses.AgentRejected or PlatformStatuses.AgentSuspended))
            return BadRequest(new { message = "Status must be approved, rejected, or suspended." });
        var oldStatus = agent.VerificationStatus;
        agent.VerificationStatus = status;
        agent.UpdatedAt = DateTime.UtcNow;
        AuditLogWriter.Add(db, CurrentUserId(), "AGENT_VERIFICATION_UPDATED", "agents", id, oldStatus, status);
        db.Notifications.Add(new PlatformNotification
        {
            UserId = agent.UserId,
            Type = "AGENT_VERIFICATION",
            Title = "Agent verification updated",
            Message = $"Your agent verification status is {status}."
        });
        await db.SaveChangesAsync();
        return Ok(new { agent.Id, agent.VerificationStatus });
    }

    [HttpPut("profiles/{id:guid}/active")]
    public async Task<IActionResult> SetProfileActive(Guid id, SetProfileActiveRequest request)
    {
        var profile = await db.Profiles.SingleOrDefaultAsync(p => p.Id == id);
        if (profile is null) return NotFound();
        if (profile.Role == PlatformRoles.Admin && !request.IsActive && await db.Profiles.CountAsync(p => p.Role == PlatformRoles.Admin && p.IsActive) <= 1)
            return Conflict(new { message = "Cannot deactivate the only active administrator." });
        var old = profile.IsActive;
        profile.IsActive = request.IsActive;
        profile.UpdatedAt = DateTime.UtcNow;
        AuditLogWriter.Add(db, CurrentUserId(), "PROFILE_ACTIVE_UPDATED", "profiles", id, old, profile.IsActive);
        await db.SaveChangesAsync();
        return Ok(new { profile.Id, profile.IsActive });
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

public sealed record VerifyAgentRequest(string Status);
public sealed record SetProfileActiveRequest(bool IsActive);
