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
[Route("api/properties")]
[Authorize]
public class PropertiesController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var query = db.Properties.AsNoTracking().Include(p => p.Images).AsQueryable();
        if (User.IsInRole(PlatformRoles.Customer))
            query = query.Where(p => p.AdminApproved && p.Status == PlatformStatuses.PropertyPublished);
        else if (User.IsInRole(PlatformRoles.Agent))
        {
            var userId = CurrentUserId();
            query = query.Where(p => p.Agent!.UserId == userId);
        }

        var results = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();
        if (User.IsInRole(PlatformRoles.Customer))
            return Ok(results.Select(p => new CustomerPropertyDto(p.Id, p.Address, p.City, p.State, p.Price, PrimaryImage(p))));
        return Ok(results.Select(ToDetail));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var property = await db.Properties.AsNoTracking().Include(p => p.Images).SingleOrDefaultAsync(p => p.Id == id);
        if (property is null) return NotFound();
        if (User.IsInRole(PlatformRoles.Customer))
        {
            if (!property.AdminApproved || property.Status != PlatformStatuses.PropertyPublished) return NotFound();
            return Ok(new CustomerPropertyDto(property.Id, property.Address, property.City, property.State, property.Price, PrimaryImage(property)));
        }
        if (User.IsInRole(PlatformRoles.Agent) && property.AgentId != await CurrentAgentId()) return NotFound();
        return Ok(ToDetail(property));
    }

    [HttpPost]
    [Authorize(Roles = PlatformRoles.Agent)]
    public async Task<IActionResult> Create(CreatePropertyRequest request)
    {
        var agent = await db.Agents.SingleOrDefaultAsync(a => a.UserId == CurrentUserId());
        if (agent is null) return Forbid();
        if (agent.VerificationStatus != PlatformStatuses.AgentApproved) return Conflict(new { message = "Your agent account must be approved before posting listings." });
        var requestedStatus = request.Status.Trim().ToLowerInvariant();
        if (requestedStatus != PlatformStatuses.PropertyDraft && requestedStatus != PlatformStatuses.PropertyPendingApproval)
            return BadRequest(new { message = "New listings can only be saved as draft or submitted for approval." });

        var property = new PlatformProperty
        {
            AgentId = agent.Id,
            Title = request.Title.Trim(),
            Description = request.Description,
            PropertyType = request.PropertyType.Trim(),
            ListingType = request.ListingType.Trim().ToUpperInvariant(),
            Price = request.Price,
            Address = request.Address.Trim(),
            City = request.City.Trim(),
            State = request.State.Trim(),
            Pincode = request.Pincode,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            Bedrooms = request.Bedrooms,
            Bathrooms = request.Bathrooms,
            Area = request.Area,
            AreaUnit = request.AreaUnit,
            Status = requestedStatus,
            AdminApproved = false
        };
        db.Properties.Add(property);
        AuditLogWriter.Add(db, CurrentUserId(), "PROPERTY_CREATED", "properties", property.Id, newValue: new { property.Title, property.Status });
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = property.Id }, ToDetail(property));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = PlatformRoles.Agent)]
    public async Task<IActionResult> Update(Guid id, UpdatePropertyRequest request)
    {
        var property = await db.Properties.SingleOrDefaultAsync(p => p.Id == id && p.Agent!.UserId == CurrentUserId());
        if (property is null) return NotFound();
        if (property.Status is PlatformStatuses.PropertyUnderOffer or PlatformStatuses.PropertySold)
            return Conflict(new { message = "Listings with an active or completed transaction cannot be edited." });
        var previous = new { property.Title, property.Description, property.Price, property.Status };
        property.Title = request.Title.Trim();
        property.Description = request.Description;
        property.PropertyType = request.PropertyType.Trim();
        property.ListingType = request.ListingType.Trim().ToUpperInvariant();
        property.Price = request.Price;
        property.Address = request.Address.Trim();
        property.City = request.City.Trim();
        property.State = request.State.Trim();
        property.Pincode = request.Pincode;
        property.Latitude = request.Latitude;
        property.Longitude = request.Longitude;
        property.Bedrooms = request.Bedrooms;
        property.Bathrooms = request.Bathrooms;
        property.Area = request.Area;
        property.AreaUnit = request.AreaUnit;
        property.Status = PlatformStatuses.PropertyPendingApproval;
        property.AdminApproved = false;
        property.PublishedAt = null;
        property.UpdatedAt = DateTime.UtcNow;
        AuditLogWriter.Add(db, CurrentUserId(), "PROPERTY_UPDATED", "properties", id, previous, new { property.Title, property.Description, property.Price, property.Status });
        await db.SaveChangesAsync();
        return Ok(ToDetail(property));
    }

    [HttpPut("{id:guid}/status")]
    [Authorize(Roles = PlatformRoles.Admin)]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdatePropertyStatusRequest request)
    {
        var property = await db.Properties.SingleOrDefaultAsync(p => p.Id == id);
        if (property is null) return NotFound();
        var status = request.Status.Trim().ToLowerInvariant();
        if (status == PlatformStatuses.PropertyInactive && property.Status is PlatformStatuses.PropertyPublished or PlatformStatuses.PropertyRejected)
        {
            property.Status = PlatformStatuses.PropertyInactive;
            property.AdminApproved = false;
            property.PublishedAt = null;
        }
        else if (status == PlatformStatuses.PropertyPublished && property.Status == PlatformStatuses.PropertyInactive)
        {
            property.Status = PlatformStatuses.PropertyPublished;
            property.AdminApproved = true;
            property.PublishedAt = DateTime.UtcNow;
        }
        else return Conflict(new { message = $"Cannot move property from {property.Status} to {status}." });

        property.UpdatedAt = DateTime.UtcNow;
        AuditLogWriter.Add(db, CurrentUserId(), "PROPERTY_STATUS_UPDATED", "properties", id, newValue: property.Status);
        await db.SaveChangesAsync();
        return Ok(ToDetail(property));
    }

    [HttpPost("{id:guid}/images")]
    [Authorize(Roles = PlatformRoles.Agent)]
    public async Task<IActionResult> AddImage(Guid id, AddImageRequest request)
    {
        var property = await db.Properties.Include(p => p.Images).SingleOrDefaultAsync(p => p.Id == id && p.Agent!.UserId == CurrentUserId());
        if (property is null) return NotFound();
        if (property.Status is PlatformStatuses.PropertyUnderOffer or PlatformStatuses.PropertySold)
            return Conflict(new { message = "Listings with an active or completed transaction cannot be edited." });
        if (request.IsPrimary)
            foreach (var existing in property.Images) existing.IsPrimary = false;
        var image = new PropertyImageRecord
        {
            PropertyId = id,
            StoragePath = request.StoragePath,
            ImageUrl = request.ImageUrl,
            IsPrimary = request.IsPrimary,
            DisplayOrder = request.DisplayOrder
        };
        db.PropertyImages.Add(image);
        // Only force reapproval for listings that were previously visible or inactive/rejected.
        // Keep draft listings in draft when adding their first image.
        if (property.Status is PlatformStatuses.PropertyPublished or PlatformStatuses.PropertyRejected or PlatformStatuses.PropertyInactive)
        {
            property.AdminApproved = false;
            property.Status = PlatformStatuses.PropertyPendingApproval;
            property.PublishedAt = null;
        }
        property.UpdatedAt = DateTime.UtcNow;
        AuditLogWriter.Add(db, CurrentUserId(), "PROPERTY_IMAGE_ADDED", "property_images", image.Id, newValue: new { image.ImageUrl, image.IsPrimary });
        await db.SaveChangesAsync();
        return Created($"/api/properties/{id}", image);
    }

    [HttpPut("{id:guid}/approval")]
    [Authorize(Roles = PlatformRoles.Admin)]
    public async Task<IActionResult> Review(Guid id, ListingReviewRequest request)
    {
        var property = await db.Properties.SingleOrDefaultAsync(p => p.Id == id);
        if (property is null) return NotFound();
        if (property.Status != PlatformStatuses.PropertyPendingApproval)
            return Conflict(new { message = "Only listings pending approval can be reviewed." });
        var previous = new { property.Status, property.AdminApproved };
        property.Status = request.Approved ? PlatformStatuses.PropertyPublished : PlatformStatuses.PropertyRejected;
        property.AdminApproved = request.Approved;
        property.PublishedAt = request.Approved ? DateTime.UtcNow : null;
        property.UpdatedAt = DateTime.UtcNow;
        AuditLogWriter.Add(db, CurrentUserId(), request.Approved ? "LISTING_APPROVED" : "LISTING_REJECTED", "properties", id, previous, new { property.Status, property.AdminApproved });
        await db.SaveChangesAsync();
        return Ok(ToDetail(property));
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private async Task<Guid?> CurrentAgentId() => (await db.Agents.AsNoTracking().SingleOrDefaultAsync(a => a.UserId == CurrentUserId()))?.Id;
    private static string? PrimaryImage(PlatformProperty property) => property.Images.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.DisplayOrder).Select(i => i.ImageUrl).FirstOrDefault();
    private static PropertyDetailDto ToDetail(PlatformProperty p) => new(p.Id, p.AgentId, p.Title, p.Description, p.PropertyType, p.ListingType, p.Price, p.Address, p.City, p.State, p.Pincode, p.Latitude, p.Longitude, p.Bedrooms, p.Bathrooms, p.Area, p.AreaUnit, p.Status, p.AdminApproved, p.CreatedAt, p.Images.OrderBy(i => i.DisplayOrder).Select(i => new PropertyImageDto(i.Id, i.ImageUrl, i.IsPrimary, i.DisplayOrder)).ToList());
}

public class CreatePropertyRequest
{
    [Required] public string Status { get; init; } = PlatformStatuses.PropertyPendingApproval;
    [Required, StringLength(180)] public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    [Required, StringLength(60)] public string PropertyType { get; init; } = string.Empty;
    [Required, StringLength(30)] public string ListingType { get; init; } = string.Empty;
    [Range(typeof(decimal), "0.01", "999999999999.99")] public decimal Price { get; init; }
    [Required, StringLength(300)] public string Address { get; init; } = string.Empty;
    [Required, StringLength(100)] public string City { get; init; } = string.Empty;
    [Required, StringLength(100)] public string State { get; init; } = string.Empty;
    [StringLength(20)] public string? Pincode { get; init; }
    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }
    [Range(0, 100)] public int? Bedrooms { get; init; }
    [Range(0, 100)] public int? Bathrooms { get; init; }
    public decimal? Area { get; init; }
    [StringLength(30)] public string? AreaUnit { get; init; }
}

public sealed class UpdatePropertyRequest : CreatePropertyRequest { }
public sealed class AddImageRequest
{
    [Required, Url, StringLength(2048)] public string ImageUrl { get; init; } = string.Empty;
    [Required, StringLength(2048)] public string StoragePath { get; init; } = string.Empty;
    public bool IsPrimary { get; init; }
    [Range(0, 1000)] public int DisplayOrder { get; init; }
}
public sealed record ListingReviewRequest(bool Approved);
public sealed record UpdatePropertyStatusRequest(string Status);
public sealed record CustomerPropertyDto(Guid Id, string Address, string City, string State, decimal Price, string? PhotoUrl);
public sealed record PropertyImageDto(Guid Id, string ImageUrl, bool IsPrimary, int DisplayOrder);
public sealed record PropertyDetailDto(Guid Id, Guid AgentId, string Title, string? Description, string PropertyType, string ListingType, decimal Price, string Address, string City, string State, string? Pincode, decimal? Latitude, decimal? Longitude, int? Bedrooms, int? Bathrooms, decimal? Area, string? AreaUnit, string Status, bool AdminApproved, DateTime CreatedAt, IReadOnlyList<PropertyImageDto> Images);
