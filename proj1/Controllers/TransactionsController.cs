using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using proj1.Data;
using proj1.Models;
using proj1.Services;

namespace proj1.Controllers;

[ApiController]
[Route("api/transactions")]
[Authorize]
public class TransactionsController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var userId = CurrentUserId();
        var query = db.Transactions.AsNoTracking().Include(t => t.Property).Include(t => t.Customer).Include(t => t.Agent).AsQueryable();
        if (User.IsInRole(PlatformRoles.Agent)) query = query.Where(t => t.Agent!.UserId == userId);
        else if (User.IsInRole(PlatformRoles.Customer)) query = query.Where(t => t.CustomerId == userId);
        var rows = await query.OrderByDescending(t => t.CreatedAt).Select(t => new TransactionDto(t.Id, t.PropertyId, t.Property!.Title, t.Customer!.FullName, t.Agent!.AgencyName, t.Status, t.AgreedPrice, t.AdminNotes, t.StartedAt, t.CompletedAt)).ToListAsync();
        return Ok(rows);
    }

    [HttpPost("from-bid/{bidId:guid}")]
    [Authorize(Roles = PlatformRoles.Admin)]
    public async Task<IActionResult> StartFromBid(Guid bidId)
    {
        var bid = await db.Bids.Include(b => b.CustomerInterest).SingleOrDefaultAsync(b => b.Id == bidId);
        if (bid is null) return NotFound();
        if (bid.Status != PlatformStatuses.BidAccepted || bid.CustomerInterest is null)
            return Conflict(new { message = "Only an accepted bid linked to customer interest can start a transaction." });
        if (await db.Transactions.AnyAsync(t => t.BidId == bidId))
            return Conflict(new { message = "A transaction already exists for this bid." });

        var transaction = new PropertyTransaction
        {
            PropertyId = bid.PropertyId,
            CustomerId = bid.CustomerInterest.CustomerId,
            AgentId = bid.AgentId,
            InterestId = bid.CustomerInterestId,
            BidId = bid.Id,
            Status = PlatformStatuses.TransactionInitiated,
            AgreedPrice = bid.Amount
        };
        var property = await db.Properties.SingleAsync(p => p.Id == bid.PropertyId);
        property.Status = PlatformStatuses.PropertyUnderOffer;
        property.UpdatedAt = DateTime.UtcNow;
        db.Transactions.Add(transaction);
        AuditLogWriter.Add(db, CurrentUserId(), "TRANSACTION_STARTED", "transactions", transaction.Id, newValue: new { transaction.BidId, transaction.Status, transaction.AgreedPrice });
        await db.SaveChangesAsync();
        return Created("/api/transactions", new { transaction.Id, transaction.Status, transaction.AgreedPrice });
    }

    [HttpPut("{id:guid}/status")]
    [Authorize(Roles = PlatformRoles.Admin)]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateTransactionRequest request)
    {
        var next = request.Status.Trim().ToLowerInvariant();
        var transactionQuery = db.Transactions.AsQueryable();
        if (User.IsInRole(PlatformRoles.Agent))
            transactionQuery = transactionQuery.Where(t => t.Agent!.UserId == CurrentUserId());
        else if (!User.IsInRole(PlatformRoles.Admin))
            return Forbid();
        var transaction = await transactionQuery.Include(t => t.Property).SingleOrDefaultAsync(t => t.Id == id);
        if (transaction is null) return NotFound();
        var valid = User.IsInRole(PlatformRoles.Admin)
            ? (transaction.Status, next) switch
            {
                (PlatformStatuses.TransactionInitiated, PlatformStatuses.TransactionAdminReview) => true,
                (PlatformStatuses.TransactionAdminReview, PlatformStatuses.TransactionNegotiation) => true,
                (PlatformStatuses.TransactionNegotiation, PlatformStatuses.TransactionAgentReview) => true,
                (PlatformStatuses.TransactionApproved, PlatformStatuses.TransactionAgreementPending) => true,
                (PlatformStatuses.TransactionAgreementPending, PlatformStatuses.TransactionCompleted) => true,
                (_, PlatformStatuses.TransactionCancelled) when transaction.Status is not (PlatformStatuses.TransactionCompleted or PlatformStatuses.TransactionCancelled) => true,
                _ => false
            }
            : (transaction.Status, next) switch
            {
                (PlatformStatuses.TransactionAgentReview, PlatformStatuses.TransactionApproved) => true,
                (PlatformStatuses.TransactionAgentReview, PlatformStatuses.TransactionNegotiation) => true,
                _ => false
            };
        if (!valid) return Conflict(new { message = $"Cannot move transaction from {transaction.Status} to {next}." });
        if (request.AgreedPrice is <= 0) return BadRequest(new { message = "Agreed price must be greater than zero." });
        var previous = transaction.Status;
        transaction.Status = next;
        if (request.AgreedPrice.HasValue) transaction.AgreedPrice = request.AgreedPrice;
        transaction.AdminNotes = request.AdminNotes;
        if (next == PlatformStatuses.TransactionNegotiation) transaction.StartedAt ??= DateTime.UtcNow;
        if (next is PlatformStatuses.TransactionCompleted or PlatformStatuses.TransactionCancelled)
        {
            transaction.CompletedAt = DateTime.UtcNow;
            transaction.Property!.Status = next == PlatformStatuses.TransactionCompleted
                ? PlatformStatuses.PropertySold
                : PlatformStatuses.PropertyPublished;
            transaction.Property.AdminApproved = next == PlatformStatuses.TransactionCancelled;
            transaction.Property.UpdatedAt = DateTime.UtcNow;
        }
        transaction.UpdatedAt = DateTime.UtcNow;
        AuditLogWriter.Add(db, CurrentUserId(), "TRANSACTION_STATUS_UPDATED", "transactions", id, previous, new { transaction.Status, transaction.AdminNotes });
        db.Notifications.AddRange(new[]
        {
            new PlatformNotification { UserId = transaction.CustomerId, Type = "TRANSACTION_UPDATE", Title = "Transaction updated", Message = $"Transaction status: {next}.", PropertyId = transaction.PropertyId, TransactionId = transaction.Id },
            new PlatformNotification { UserId = await db.Agents.Where(a => a.Id == transaction.AgentId).Select(a => a.UserId).SingleAsync(), Type = "TRANSACTION_UPDATE", Title = "Transaction updated", Message = $"Transaction status: {next}.", PropertyId = transaction.PropertyId, TransactionId = transaction.Id }
        });
        await db.SaveChangesAsync();
        return Ok(new { transaction.Id, transaction.Status, transaction.AgreedPrice, transaction.StartedAt, transaction.CompletedAt });
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

public sealed record UpdateTransactionRequest(string Status, decimal? AgreedPrice, string? AdminNotes);
public sealed record TransactionDto(Guid Id, Guid PropertyId, string PropertyTitle, string CustomerName, string AgencyName, string Status, decimal? AgreedPrice, string? AdminNotes, DateTime? StartedAt, DateTime? CompletedAt);
