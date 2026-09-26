using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using proj1.Data;
using proj1.Models;
using proj1.Services;

namespace proj1.Controllers;

[ApiController]
[Route("api/bids")]
[Authorize]
public class BidsController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var userId = CurrentUserId();
        var query = db.Bids.AsNoTracking().Include(b => b.Property).Include(b => b.CustomerInterest).ThenInclude(i => i!.Customer).AsQueryable();
        if (User.IsInRole(PlatformRoles.Agent))
            query = query.Where(b => b.Agent!.UserId == userId && b.Status != PlatformStatuses.BidDraft);
        else if (User.IsInRole(PlatformRoles.Customer))
            query = query.Where(b => b.CustomerInterest!.CustomerId == userId && b.Status != PlatformStatuses.BidDraft);
        var results = await query.OrderByDescending(b => b.CreatedAt).Select(b => new BidDto(b.Id, b.PropertyId, b.Property!.Title, b.CustomerInterest!.CustomerId, b.CustomerInterest.Customer!.FullName, b.Amount, b.Message, b.Status, b.CreatedAt)).ToListAsync();
        return Ok(results);
    }

    [HttpPost]
    [Authorize(Roles = PlatformRoles.Admin)]
    public async Task<IActionResult> Create(CreateBidRequest request)
    {
        var interest = await db.CustomerInterests.Include(i => i.Customer).Include(i => i.Property).ThenInclude(p => p!.Agent)
            .SingleOrDefaultAsync(i => i.Id == request.CustomerInterestId);
        if (interest?.Property is null) return NotFound(new { message = "Customer interest not found." });
        if (!interest.Property.AdminApproved || interest.Property.Status != PlatformStatuses.PropertyPublished)
            return Conflict(new { message = "Bids can only be created for published listings." });
        if (interest.Status != PlatformStatuses.InterestApproved)
            return Conflict(new { message = "Approve the customer interest before creating a bid." });
        if (request.Amount <= 0) return BadRequest(new { message = "Bid amount must be greater than zero." });

        var bid = new Bid
        {
            PropertyId = interest.PropertyId,
            AgentId = interest.Property.AgentId,
            CustomerInterestId = interest.Id,
            CreatedByAdmin = true,
            Amount = request.Amount,
            Message = request.Message?.Trim(),
            Status = PlatformStatuses.BidDraft
        };
        db.Bids.Add(bid);
        AuditLogWriter.Add(db, CurrentUserId(), "BID_CREATED", "bids", bid.Id, newValue: new { bid.PropertyId, bid.CustomerInterestId, bid.Amount });
        await db.SaveChangesAsync();
        return Created("/api/bids", ToDto(bid, interest));
    }

    [HttpPut("{id:guid}/send")]
    [Authorize(Roles = PlatformRoles.Admin)]
    public async Task<IActionResult> Send(Guid id)
    {
        var bid = await db.Bids.Include(b => b.Agent).SingleOrDefaultAsync(b => b.Id == id);
        if (bid is null) return NotFound();
        if (bid.Status != PlatformStatuses.BidDraft) return Conflict(new { message = "Only draft bids can be sent." });
        bid.Status = PlatformStatuses.BidSent;
        bid.UpdatedAt = DateTime.UtcNow;
        db.Notifications.Add(new PlatformNotification
        {
            UserId = bid.Agent!.UserId,
            Type = "NEW_BID",
            Title = "Admin sent an offer",
            Message = "Review the offer and mark it viewed before accepting or rejecting it.",
            PropertyId = bid.PropertyId,
            BidId = bid.Id
        });
        AuditLogWriter.Add(db, CurrentUserId(), "BID_SENT", "bids", id, PlatformStatuses.BidDraft, PlatformStatuses.BidSent);
        await db.SaveChangesAsync();
        return Ok(new { bid.Id, bid.Status, bid.UpdatedAt });
    }

    [HttpPut("{id:guid}/viewed")]
    [Authorize(Roles = PlatformRoles.Agent)]
    public async Task<IActionResult> MarkViewed(Guid id)
    {
        var bid = await db.Bids.Include(b => b.Agent).SingleOrDefaultAsync(b => b.Id == id && b.Agent!.UserId == CurrentUserId());
        if (bid is null) return NotFound();
        if (bid.Status != PlatformStatuses.BidSent) return Conflict(new { message = "Only sent bids can be marked viewed." });
        bid.Status = PlatformStatuses.BidViewed;
        bid.UpdatedAt = DateTime.UtcNow;
        AuditLogWriter.Add(db, CurrentUserId(), "BID_VIEWED", "bids", id, PlatformStatuses.BidSent, PlatformStatuses.BidViewed);
        await db.SaveChangesAsync();
        return Ok(new { bid.Id, bid.Status, bid.UpdatedAt });
    }

    [HttpPut("{id:guid}/status")]
    [Authorize(Roles = PlatformRoles.Admin)]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateBidStatusRequest request)
    {
        var bid = await db.Bids.SingleOrDefaultAsync(b => b.Id == id);
        if (bid is null) return NotFound();
        var next = request.Status.Trim().ToLowerInvariant();
        if (next != PlatformStatuses.BidExpired || bid.Status is not (PlatformStatuses.BidDraft or PlatformStatuses.BidSent or PlatformStatuses.BidViewed))
            return Conflict(new { message = "Only draft, sent, or viewed bids can be expired." });
        var previous = bid.Status;
        bid.Status = next;
        bid.UpdatedAt = DateTime.UtcNow;
        AuditLogWriter.Add(db, CurrentUserId(), "BID_EXPIRED", "bids", id, previous, next);
        await db.SaveChangesAsync();
        return Ok(new { bid.Id, bid.Status, bid.UpdatedAt });
    }

    [HttpPut("{id:guid}/decision")]
    [Authorize(Roles = PlatformRoles.Agent)]
    public async Task<IActionResult> Decide(Guid id, BidDecisionRequest request)
    {
        var bid = await db.Bids.Include(b => b.Agent).Include(b => b.Property).Include(b => b.CustomerInterest)
            .SingleOrDefaultAsync(b => b.Id == id && b.Agent!.UserId == CurrentUserId());
        if (bid is null) return NotFound();
        if (bid.Status != PlatformStatuses.BidViewed) return Conflict(new { message = "View the bid before accepting or rejecting it." });
        bid.Status = request.Accepted ? PlatformStatuses.BidAccepted : PlatformStatuses.BidRejected;
        bid.UpdatedAt = DateTime.UtcNow;
        if (bid.CustomerInterest is not null)
        {
            if (request.Accepted) bid.CustomerInterest.Status = PlatformStatuses.InterestConverted;
            db.Notifications.Add(new PlatformNotification
            {
                UserId = bid.CustomerInterest.CustomerId,
                Type = "BID_DECISION",
                Title = request.Accepted ? "Offer accepted" : "Offer declined",
                Message = request.Accepted ? "The agent accepted the offer. An administrator will coordinate the transaction." : "The agent declined the offer.",
                PropertyId = bid.PropertyId,
                BidId = bid.Id
            });
        }
        AuditLogWriter.Add(db, CurrentUserId(), request.Accepted ? "BID_ACCEPTED" : "BID_REJECTED", "bids", id, PlatformStatuses.BidViewed, bid.Status);
        await db.SaveChangesAsync();
        return Ok(new { bid.Id, bid.Status, bid.UpdatedAt });
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static BidDto ToDto(Bid bid, CustomerInterest interest) => new(bid.Id, bid.PropertyId, interest.Property?.Title ?? string.Empty, interest.CustomerId, interest.Customer?.FullName ?? string.Empty, bid.Amount, bid.Message, bid.Status, bid.CreatedAt);
}

public sealed record CreateBidRequest(Guid CustomerInterestId, decimal Amount, string? Message);
public sealed record BidDecisionRequest(bool Accepted);
public sealed record UpdateBidStatusRequest(string Status);
public sealed record BidDto(Guid Id, Guid PropertyId, string PropertyTitle, Guid CustomerId, string CustomerName, decimal Amount, string? Message, string Status, DateTime CreatedAt);
