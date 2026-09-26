using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using proj1.Data;
using proj1.Models;
using proj1.Services;

namespace proj1.Controllers;

[ApiController]
[Route("api/interests")]
[Authorize]
public class CustomerInterestsController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var userId = CurrentUserId();
        var query = db.CustomerInterests.AsNoTracking().Include(x => x.Customer).Include(x => x.Property).AsQueryable();
        if (User.IsInRole(PlatformRoles.Customer)) query = query.Where(x => x.CustomerId == userId);
        else if (User.IsInRole(PlatformRoles.Agent)) query = query.Where(x => x.Property!.Agent!.UserId == userId);
        var results = await query.OrderByDescending(x => x.CreatedAt).Select(x => new InterestDto(x.Id, x.CustomerId, x.Customer!.FullName, x.PropertyId, x.Property!.Title, x.Status, x.CustomerMessage, x.CreatedAt)).ToListAsync();
        return Ok(results);
    }

    [HttpPost]
    [Authorize(Roles = PlatformRoles.Customer)]
    public async Task<IActionResult> Create(CreateInterestRequest request)
    {
        var customerId = CurrentUserId();
        var property = await db.Properties.SingleOrDefaultAsync(p => p.Id == request.PropertyId && p.AdminApproved && p.Status == PlatformStatuses.PropertyPublished);
        if (property is null) return NotFound(new { message = "Approved listing not found." });
        if (await db.CustomerInterests.AnyAsync(i => i.CustomerId == customerId && i.PropertyId == request.PropertyId))
            return Conflict(new { message = "You already expressed interest in this listing." });

        var interest = new CustomerInterest
        {
            CustomerId = customerId,
            PropertyId = property.Id,
            CustomerMessage = request.CustomerMessage?.Trim(),
            Status = PlatformStatuses.InterestNew
        };
        db.CustomerInterests.Add(interest);
        var agent = await db.Agents.AsNoTracking().SingleAsync(a => a.Id == property.AgentId);
        db.Notifications.Add(new PlatformNotification
        {
            UserId = agent.UserId,
            Type = "CUSTOMER_INTEREST",
            Title = "New customer interest",
            Message = "A customer is interested in your property listing.",
            PropertyId = property.Id
        });
        AuditLogWriter.Add(db, customerId, "INTEREST_CREATED", "customer_interests", interest.Id, newValue: new { interest.PropertyId });
        await db.SaveChangesAsync();
        return Created("/api/interests", new InterestDto(interest.Id, customerId, string.Empty, property.Id, property.Title, interest.Status, interest.CustomerMessage, interest.CreatedAt));
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateInterestStatusRequest request)
    {
        var status = request.Status.Trim().ToLowerInvariant();
        var interest = await db.CustomerInterests.Include(x => x.Property).ThenInclude(x => x!.Agent)
            .SingleOrDefaultAsync(x => x.Id == id);
        if (interest is null) return NotFound();
        var currentUserId = CurrentUserId();
        if (User.IsInRole(PlatformRoles.Agent))
        {
            if (interest.Property?.Agent?.UserId != currentUserId) return NotFound();
            if (status != PlatformStatuses.InterestContacted || interest.Status is not (PlatformStatuses.InterestNew or PlatformStatuses.InterestUnderReview))
                return Conflict(new { message = "Agents can mark new or under-review interests as contacted." });
        }
        else if (User.IsInRole(PlatformRoles.Admin))
        {
            var allowed = (interest.Status, status) switch
            {
                (PlatformStatuses.InterestNew, PlatformStatuses.InterestUnderReview) => true,
                (PlatformStatuses.InterestUnderReview or PlatformStatuses.InterestContacted, PlatformStatuses.InterestApproved or PlatformStatuses.InterestRejected or PlatformStatuses.InterestClosed) => true,
                (PlatformStatuses.InterestApproved, PlatformStatuses.InterestRejected or PlatformStatuses.InterestConverted or PlatformStatuses.InterestClosed) => true,
                (PlatformStatuses.InterestRejected, PlatformStatuses.InterestClosed) => true,
                _ => false
            };
            if (!allowed) return Conflict(new { message = $"Cannot move interest from {interest.Status} to {status}." });
        }
        else return Forbid();

        var previous = interest.Status;
        interest.Status = status;
        interest.UpdatedAt = DateTime.UtcNow;
        AuditLogWriter.Add(db, currentUserId, "INTEREST_STATUS_UPDATED", "customer_interests", id, previous, status);
        await db.SaveChangesAsync();
        return Ok(new { interest.Id, interest.Status, interest.UpdatedAt });
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

public sealed record CreateInterestRequest(Guid PropertyId, string? CustomerMessage);
public sealed record UpdateInterestStatusRequest(string Status);
public sealed record InterestDto(Guid Id, Guid CustomerId, string CustomerName, Guid PropertyId, string PropertyTitle, string Status, string? CustomerMessage, DateTime CreatedAt);
