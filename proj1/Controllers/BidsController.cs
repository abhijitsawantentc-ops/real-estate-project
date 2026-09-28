using System.ComponentModel.DataAnnotations;
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
        var userRole = User.FindFirstValue(ClaimTypes.Role);

        if (userRole == PlatformRoles.Admin)
        {
            var adminBids = await db.Bids.AsNoTracking()
                .Include(b => b.Property)
                .Include(b => b.Buyer)
                .Include(b => b.Agent).ThenInclude(a => a!.Profile)
                .Include(b => b.CustomerInterest).ThenInclude(ci => ci!.Customer)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            return Ok(adminBids.Select(ToAdminBidDto));
        }

        if (userRole == PlatformRoles.Agent)
        {
            var agent = await db.Agents.AsNoTracking().SingleOrDefaultAsync(a => a.UserId == userId);
            var agentId = agent?.Id;

            // Agent as Seller: Sees bids sent/approved by Admin on their properties
            // Agent as Buyer: Sees bids they placed on other agents' properties
            var query = db.Bids.AsNoTracking()
                .Include(b => b.Property)
                .Include(b => b.Buyer)
                .Include(b => b.Agent).ThenInclude(a => a!.Profile)
                .Where(b =>
                    (b.AgentId == agentId && b.Status != PlatformStatuses.BidPendingAdmin && b.Status != PlatformStatuses.BidRejectedByAdmin && b.Status != PlatformStatuses.BidDraft) ||
                    (b.BuyerId == userId));

            var results = await query.OrderByDescending(b => b.CreatedAt).ToListAsync();
            return Ok(results.Select(b => ToAgentOrBuyerBidDto(b, userId)));
        }

        if (userRole == PlatformRoles.Customer)
        {
            // Customer as Buyer: sees their bids
            var query = db.Bids.AsNoTracking()
                .Include(b => b.Property)
                .Include(b => b.Buyer)
                .Include(b => b.CustomerInterest).ThenInclude(ci => ci!.Customer)
                .Where(b => b.BuyerId == userId || (b.CustomerInterest != null && b.CustomerInterest.CustomerId == userId))
                .OrderByDescending(b => b.CreatedAt);

            var results = await query.ToListAsync();
            return Ok(results.Select(b => ToAgentOrBuyerBidDto(b, userId)));
        }

        return Forbid();
    }

    [HttpPost]
    public async Task<IActionResult> Create(SubmitBidRequest request)
    {
        var currentUserId = CurrentUserId();
        var userRole = User.FindFirstValue(ClaimTypes.Role);

        // Check if this is an Admin creating an offer from CustomerInterest (legacy/alternative admin flow)
        if (request.CustomerInterestId.HasValue && userRole == PlatformRoles.Admin)
        {
            var interest = await db.CustomerInterests.Include(i => i.Customer).Include(i => i.Property).ThenInclude(p => p!.Agent)
                .SingleOrDefaultAsync(i => i.Id == request.CustomerInterestId.Value);
            if (interest?.Property is null) return NotFound(new { message = "Customer interest not found." });
            if (!interest.Property.AdminApproved || interest.Property.Status != PlatformStatuses.PropertyPublished)
                return Conflict(new { message = "Bids can only be created for published listings." });
            if (request.Amount <= 0) return BadRequest(new { message = "Bid amount must be greater than zero." });

            var adminCreatedBid = new Bid
            {
                PropertyId = interest.PropertyId,
                AgentId = interest.Property.AgentId,
                CustomerInterestId = interest.Id,
                BuyerId = interest.CustomerId,
                CreatedByAdmin = true,
                OriginalAmount = request.Amount,
                Amount = request.Amount,
                Message = request.Message?.Trim(),
                BuyerMessage = request.Message?.Trim(),
                Status = PlatformStatuses.BidSent // directly sent by admin
            };
            db.Bids.Add(adminCreatedBid);
            AuditLogWriter.Add(db, currentUserId, "BID_CREATED_BY_ADMIN", "bids", adminCreatedBid.Id, newValue: new { adminCreatedBid.PropertyId, adminCreatedBid.Amount });
            await db.SaveChangesAsync();
            return Created("/api/bids", ToAdminBidDto(adminCreatedBid));
        }

        // Standard flow: Buyer (Customer or other Agent) placing a bid on an approved property
        if (!request.PropertyId.HasValue)
            return BadRequest(new { message = "PropertyId is required." });

        if (request.Amount <= 0)
            return BadRequest(new { message = "Bid amount must be greater than zero." });

        var property = await db.Properties.Include(p => p.Agent)
            .SingleOrDefaultAsync(p => p.Id == request.PropertyId.Value);

        if (property is null)
            return NotFound(new { message = "Property not found." });

        if (!property.AdminApproved || property.Status != PlatformStatuses.PropertyPublished)
            return Conflict(new { message = "Bids can only be submitted for published, admin-approved listings." });

        // Agent cannot bid on their own listing
        if (property.Agent?.UserId == currentUserId)
            return Conflict(new { message = "You cannot place a bid on your own property listing." });

        var buyer = await db.Profiles.SingleOrDefaultAsync(p => p.Id == currentUserId);
        if (buyer is null) return Unauthorized();

        var bid = new Bid
        {
            PropertyId = property.Id,
            AgentId = property.AgentId,
            BuyerId = currentUserId,
            CreatedByAdmin = false,
            OriginalAmount = request.Amount,
            Amount = request.Amount,
            IsModifiedByAdmin = false,
            BuyerMessage = request.Message?.Trim(),
            Message = request.Message?.Trim(),
            Status = PlatformStatuses.BidPendingAdmin // Sent to admin for review/approval/modification!
        };

        db.Bids.Add(bid);

        // Notify admins about the new bid
        var adminProfiles = await db.Profiles.Where(p => p.Role == PlatformRoles.Admin && p.IsActive).ToListAsync();
        foreach (var admin in adminProfiles)
        {
            db.Notifications.Add(new PlatformNotification
            {
                UserId = admin.Id,
                Type = "NEW_BID_FOR_ADMIN",
                Title = "New Bid Awaiting Approval",
                Message = $"{buyer.FullName} placed a bid of {request.Amount:C0} on '{property.Title}'. Review and approve or modify before sending to seller.",
                PropertyId = property.Id,
                BidId = bid.Id
            });
        }

        AuditLogWriter.Add(db, currentUserId, "BUYER_BID_SUBMITTED", "bids", bid.Id, newValue: new { bid.PropertyId, bid.OriginalAmount });
        await db.SaveChangesAsync();

        return Created("/api/bids", ToAgentOrBuyerBidDto(bid, currentUserId));
    }

    [HttpPut("{id:guid}/admin-review")]
    [Authorize(Roles = PlatformRoles.Admin)]
    public async Task<IActionResult> AdminReview(Guid id, AdminBidReviewRequest request)
    {
        var bid = await db.Bids
            .Include(b => b.Property)
            .Include(b => b.Agent)
            .Include(b => b.Buyer)
            .SingleOrDefaultAsync(b => b.Id == id);

        if (bid is null) return NotFound(new { message = "Bid not found." });

        var action = request.Action.Trim().ToLowerInvariant();
        if (action is not ("approve" or "modify" or "reject"))
            return BadRequest(new { message = "Action must be 'approve', 'modify', or 'reject'." });

        var previousStatus = bid.Status;
        var previousAmount = bid.Amount;

        if (action == "approve")
        {
            bid.Status = PlatformStatuses.BidSent;
            bid.AdminNotes = request.AdminNotes?.Trim();
            bid.UpdatedAt = DateTime.UtcNow;

            // Notify Seller / Agent
            if (bid.Agent is not null)
            {
                db.Notifications.Add(new PlatformNotification
                {
                    UserId = bid.Agent.UserId,
                    Type = "BID_FORWARDED",
                    Title = "New Offer Approved by Admin",
                    Message = $"Admin has verified and forwarded an offer of {bid.Amount:C0} for '{bid.Property?.Title}'.",
                    PropertyId = bid.PropertyId,
                    BidId = bid.Id
                });
            }

            // Notify Buyer
            if (bid.BuyerId.HasValue)
            {
                db.Notifications.Add(new PlatformNotification
                {
                    UserId = bid.BuyerId.Value,
                    Type = "BID_APPROVED_BY_ADMIN",
                    Title = "Your Bid Was Approved",
                    Message = $"Your offer of {bid.Amount:C0} for '{bid.Property?.Title}' was approved by Admin and submitted to the seller.",
                    PropertyId = bid.PropertyId,
                    BidId = bid.Id
                });
            }

            AuditLogWriter.Add(db, CurrentUserId(), "BID_ADMIN_APPROVED", "bids", id, previousStatus, bid.Status);
        }
        else if (action == "modify")
        {
            if (!request.ModifiedAmount.HasValue || request.ModifiedAmount.Value <= 0)
                return BadRequest(new { message = "ModifiedAmount greater than zero is required when action is 'modify'." });

            bid.Amount = request.ModifiedAmount.Value;
            bid.IsModifiedByAdmin = true;
            bid.AdminNotes = request.AdminNotes?.Trim();
            bid.Status = PlatformStatuses.BidSent;
            bid.UpdatedAt = DateTime.UtcNow;

            // Notify Seller: shows ONLY the Admin's adjusted amount as the official bid amount
            if (bid.Agent is not null)
            {
                db.Notifications.Add(new PlatformNotification
                {
                    UserId = bid.Agent.UserId,
                    Type = "BID_FORWARDED",
                    Title = "Offer Forwarded by Admin",
                    Message = $"Admin has reviewed and forwarded an offer of {bid.Amount:C0} for '{bid.Property?.Title}'.",
                    PropertyId = bid.PropertyId,
                    BidId = bid.Id
                });
            }

            // Notify Buyer about the adjusted offer forwarded to seller
            if (bid.BuyerId.HasValue)
            {
                db.Notifications.Add(new PlatformNotification
                {
                    UserId = bid.BuyerId.Value,
                    Type = "BID_MODIFIED_BY_ADMIN",
                    Title = "Bid Adjusted by Admin",
                    Message = $"Your offer for '{bid.Property?.Title}' was reviewed and adjusted by Admin to {bid.Amount:C0} (Note: {bid.AdminNotes ?? "terms adjusted"}), and forwarded to the seller.",
                    PropertyId = bid.PropertyId,
                    BidId = bid.Id
                });
            }

            AuditLogWriter.Add(db, CurrentUserId(), "BID_ADMIN_MODIFIED", "bids", id,
                new { PreviousAmount = previousAmount, PreviousStatus = previousStatus },
                new { NewAmount = bid.Amount, bid.Status, bid.AdminNotes });
        }
        else if (action == "reject")
        {
            bid.Status = PlatformStatuses.BidRejectedByAdmin;
            bid.AdminNotes = request.AdminNotes?.Trim();
            bid.UpdatedAt = DateTime.UtcNow;

            // Notify Buyer
            if (bid.BuyerId.HasValue)
            {
                db.Notifications.Add(new PlatformNotification
                {
                    UserId = bid.BuyerId.Value,
                    Type = "BID_REJECTED_BY_ADMIN",
                    Title = "Bid Not Approved",
                    Message = $"Your offer for '{bid.Property?.Title}' was not approved by Admin. {(!string.IsNullOrWhiteSpace(bid.AdminNotes) ? $"Reason: {bid.AdminNotes}" : "")}",
                    PropertyId = bid.PropertyId,
                    BidId = bid.Id
                });
            }

            AuditLogWriter.Add(db, CurrentUserId(), "BID_ADMIN_REJECTED", "bids", id, previousStatus, bid.Status);
        }

        await db.SaveChangesAsync();
        return Ok(ToAdminBidDto(bid));
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

    [HttpPut("{id:guid}/decision")]
    [Authorize(Roles = PlatformRoles.Agent)]
    public async Task<IActionResult> Decide(Guid id, BidDecisionRequest request)
    {
        var bid = await db.Bids
            .Include(b => b.Agent)
            .Include(b => b.Property)
            .Include(b => b.Buyer)
            .Include(b => b.CustomerInterest)
            .SingleOrDefaultAsync(b => b.Id == id && b.Agent!.UserId == CurrentUserId());

        if (bid is null) return NotFound();
        if (bid.Status != PlatformStatuses.BidViewed && bid.Status != PlatformStatuses.BidSent)
            return Conflict(new { message = "Offer must be sent or viewed before accepting or rejecting." });

        bid.Status = request.Accepted ? PlatformStatuses.BidAccepted : PlatformStatuses.BidRejected;
        bid.UpdatedAt = DateTime.UtcNow;

        if (bid.CustomerInterest is not null && request.Accepted)
        {
            bid.CustomerInterest.Status = PlatformStatuses.InterestConverted;
        }

        var recipientUserId = bid.BuyerId ?? bid.CustomerInterest?.CustomerId;
        if (recipientUserId.HasValue)
        {
            db.Notifications.Add(new PlatformNotification
            {
                UserId = recipientUserId.Value,
                Type = "BID_DECISION",
                Title = request.Accepted ? "🎉 Offer Accepted by Seller!" : "Offer Declined by Seller",
                Message = request.Accepted
                    ? $"The seller accepted the offer of {bid.Amount:C0} for '{bid.Property?.Title}'. Admin will coordinate closing transaction."
                    : $"The seller declined the offer for '{bid.Property?.Title}'.",
                PropertyId = bid.PropertyId,
                BidId = bid.Id
            });
        }

        // Notify Admin of seller decision
        var adminProfiles = await db.Profiles.Where(p => p.Role == PlatformRoles.Admin && p.IsActive).ToListAsync();
        foreach (var admin in adminProfiles)
        {
            db.Notifications.Add(new PlatformNotification
            {
                UserId = admin.Id,
                Type = "SELLER_BID_DECISION",
                Title = request.Accepted ? "Seller Accepted Offer" : "Seller Declined Offer",
                Message = $"Seller {(request.Accepted ? "accepted" : "declined")} offer of {bid.Amount:C0} on '{bid.Property?.Title}'.",
                PropertyId = bid.PropertyId,
                BidId = bid.Id
            });
        }

        // Auto-create transaction if accepted and not existing
        if (request.Accepted && recipientUserId.HasValue && bid.Property is not null)
        {
            var existingTx = await db.Transactions.AnyAsync(t => t.BidId == bid.Id);
            if (!existingTx)
            {
                var transaction = new PropertyTransaction
                {
                    PropertyId = bid.PropertyId,
                    CustomerId = recipientUserId.Value,
                    AgentId = bid.AgentId,
                    BidId = bid.Id,
                    InterestId = bid.CustomerInterestId,
                    Status = PlatformStatuses.TransactionInitiated,
                    AgreedPrice = bid.Amount,
                    AdminNotes = "Auto-initiated upon seller acceptance of bid.",
                    StartedAt = DateTime.UtcNow
                };
                db.Transactions.Add(transaction);
            }
        }

        AuditLogWriter.Add(db, CurrentUserId(), request.Accepted ? "BID_ACCEPTED" : "BID_REJECTED", "bids", id, PlatformStatuses.BidViewed, bid.Status);
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

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static AdminBidDto ToAdminBidDto(Bid b) => new(
        b.Id,
        b.PropertyId,
        b.Property?.Title ?? "Property",
        b.Property?.Price ?? 0m,
        b.Property?.NegotiablePrice,
        b.BuyerId ?? b.CustomerInterest?.CustomerId,
        b.Buyer?.FullName ?? b.CustomerInterest?.Customer?.FullName ?? "Prospective Buyer",
        b.Buyer?.Email ?? b.CustomerInterest?.Customer?.Email,
        b.Buyer?.Role ?? PlatformRoles.Customer,
        b.AgentId,
        b.Agent?.Profile?.FullName,
        b.Agent?.AgencyName,
        b.OriginalAmount,
        b.Amount,
        b.IsModifiedByAdmin,
        b.AdminNotes,
        b.BuyerMessage ?? b.Message,
        b.Status,
        b.CreatedAt,
        b.UpdatedAt
    );

    private static UserBidDto ToAgentOrBuyerBidDto(Bid b, Guid currentUserId)
    {
        var isSeller = b.Agent?.UserId == currentUserId;
        var isBuyer = b.BuyerId == currentUserId || b.CustomerInterest?.CustomerId == currentUserId;

        return new UserBidDto(
            b.Id,
            b.PropertyId,
            b.Property?.Title ?? "Property",
            b.Property?.Price ?? 0m,
            // As per Q2: seller sees only the final/adjusted official amount
            b.Amount,
            // Buyer sees their original amount too
            isBuyer ? b.OriginalAmount : (decimal?)null,
            isBuyer ? b.IsModifiedByAdmin : false,
            b.AdminNotes,
            isSeller ? (b.Buyer?.FullName ?? b.CustomerInterest?.Customer?.FullName ?? "Verified Buyer") : null,
            isBuyer ? (b.Agent?.AgencyName ?? b.Agent?.Profile?.FullName ?? "Listing Agent") : null,
            b.BuyerMessage ?? b.Message,
            b.Status,
            isSeller,
            isBuyer,
            b.CreatedAt
        );
    }
}

public sealed record SubmitBidRequest(
    Guid? PropertyId,
    decimal Amount,
    string? Message,
    Guid? CustomerInterestId = null
);

public sealed record AdminBidReviewRequest(
    [Required] string Action,
    decimal? ModifiedAmount,
    string? AdminNotes
);

public sealed record BidDecisionRequest(bool Accepted);
public sealed record UpdateBidStatusRequest(string Status);

public sealed record AdminBidDto(
    Guid Id,
    Guid PropertyId,
    string PropertyTitle,
    decimal PropertyAskingPrice,
    decimal? PropertyNegotiablePrice,
    Guid? BuyerId,
    string BuyerName,
    string? BuyerEmail,
    string BuyerRole,
    Guid AgentId,
    string? SellerName,
    string? SellerAgency,
    decimal OriginalAmount,
    decimal Amount,
    bool IsModifiedByAdmin,
    string? AdminNotes,
    string? BuyerMessage,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public sealed record UserBidDto(
    Guid Id,
    Guid PropertyId,
    string PropertyTitle,
    decimal AskingPrice,
    decimal Amount,
    decimal? OriginalAmount,
    bool IsModifiedByAdmin,
    string? AdminNotes,
    string? BuyerName,
    string? SellerName,
    string? Message,
    string Status,
    bool IsSeller,
    bool IsBuyer,
    DateTime CreatedAt
);
