using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using proj1.Data;

namespace proj1.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var userId = CurrentUserId();
        var notifications = await db.Notifications.AsNoTracking().Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new { n.Id, n.Type, n.Title, n.Message, n.PropertyId, n.BidId, n.TransactionId, n.IsRead, n.CreatedAt })
            .Take(100).ToListAsync();
        return Ok(notifications);
    }

    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        var notification = await db.Notifications.SingleOrDefaultAsync(n => n.Id == id && n.UserId == CurrentUserId());
        if (notification is null) return NotFound();
        notification.IsRead = true;
        await db.SaveChangesAsync();
        return NoContent();
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
