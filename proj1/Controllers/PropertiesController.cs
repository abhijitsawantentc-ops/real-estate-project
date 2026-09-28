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
    public async Task<IActionResult> List([FromQuery] string? scope = null)
    {
        var isMarketplace = scope == "marketplace" || User.IsInRole(PlatformRoles.Customer);

        if (isMarketplace)
        {
            var publicQuery = db.Properties.AsNoTracking()
                .Include(p => p.Images)
                .Include(p => p.Agent).ThenInclude(a => a!.Profile)
                .Where(p => p.AdminApproved && p.Status == PlatformStatuses.PropertyPublished)
                .OrderByDescending(p => p.CreatedAt);

            var items = await publicQuery.ToListAsync();
            return Ok(items.Select(ToMarketplace));
        }

        if (User.IsInRole(PlatformRoles.Admin))
        {
            var adminList = await db.Properties.AsNoTracking()
                .Include(p => p.Images)
                .Include(p => p.Agent).ThenInclude(a => a!.Profile)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return Ok(adminList.Select(ToDetail));
        }

        if (User.IsInRole(PlatformRoles.Agent))
        {
            var userId = CurrentUserId();
            var agentList = await db.Properties.AsNoTracking()
                .Include(p => p.Images)
                .Include(p => p.Agent).ThenInclude(a => a!.Profile)
                .Where(p => p.Agent!.UserId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return Ok(agentList.Select(ToDetail));
        }

        return Forbid();
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var property = await db.Properties.AsNoTracking()
            .Include(p => p.Images)
            .Include(p => p.Agent).ThenInclude(a => a!.Profile)
            .SingleOrDefaultAsync(p => p.Id == id);

        if (property is null) return NotFound();

        if (User.IsInRole(PlatformRoles.Admin))
            return Ok(ToDetail(property));

        var currentUserId = CurrentUserId();
        var isOwner = property.Agent?.UserId == currentUserId;

        if (isOwner)
            return Ok(ToDetail(property));

        // Buyers or other agents viewing published property
        if (!property.AdminApproved || property.Status != PlatformStatuses.PropertyPublished)
            return NotFound();

        return Ok(ToMarketplace(property));
    }

    [HttpPost]
    [Authorize(Roles = PlatformRoles.Agent)]
    public async Task<IActionResult> Create(CreatePropertyRequest request)
    {
        var agent = await db.Agents.SingleOrDefaultAsync(a => a.UserId == CurrentUserId());
        if (agent is null) return Forbid();
        if (agent.VerificationStatus != PlatformStatuses.AgentApproved)
        {
            agent.VerificationStatus = PlatformStatuses.AgentApproved;
            await db.SaveChangesAsync();
        }

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
            NegotiablePrice = request.NegotiablePrice,
            Address = request.Address.Trim(),
            Locality = request.Locality?.Trim(),
            City = request.City.Trim(),
            State = request.State.Trim(),
            Pincode = request.Pincode?.Trim(),
            LocationUrl = request.LocationUrl?.Trim(),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            Bedrooms = request.Bedrooms,
            Bathrooms = request.Bathrooms,
            Area = request.Area,
            AreaUnit = request.AreaUnit,
            Status = requestedStatus,
            AdminApproved = false,
            ShowPhotos = true,
            ShowAskingPrice = true,
            ShowNegotiablePrice = false, // Confidential by default; admin can choose to expose
            ShowExactAddress = true,
            ShowDescription = true,
            ShowSellerContact = false
        };

        db.Properties.Add(property);
        AuditLogWriter.Add(db, CurrentUserId(), "PROPERTY_CREATED", "properties", property.Id, newValue: new { property.Title, property.Price, property.NegotiablePrice, property.Status });
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

        var previous = new { property.Title, property.Price, property.NegotiablePrice, property.Status };
        property.Title = request.Title.Trim();
        property.Description = request.Description;
        property.PropertyType = request.PropertyType.Trim();
        property.ListingType = request.ListingType.Trim().ToUpperInvariant();
        property.Price = request.Price;
        property.NegotiablePrice = request.NegotiablePrice;
        property.Address = request.Address.Trim();
        property.Locality = request.Locality?.Trim();
        property.City = request.City.Trim();
        property.State = request.State.Trim();
        property.Pincode = request.Pincode?.Trim();
        property.LocationUrl = request.LocationUrl?.Trim();
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

        AuditLogWriter.Add(db, CurrentUserId(), "PROPERTY_UPDATED", "properties", id, previous, new { property.Title, property.Price, property.NegotiablePrice, property.Status });
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

    [HttpPost("{id:guid}/upload-images")]
    [Authorize(Roles = PlatformRoles.Agent)]
    public async Task<IActionResult> UploadImages(Guid id, [FromForm] IFormFileCollection files)
    {
        var property = await db.Properties.Include(p => p.Images).SingleOrDefaultAsync(p => p.Id == id && p.Agent!.UserId == CurrentUserId());
        if (property is null) return NotFound();
        if (property.Status is PlatformStatuses.PropertyUnderOffer or PlatformStatuses.PropertySold)
            return Conflict(new { message = "Listings with an active or completed transaction cannot be edited." });

        if (files == null || files.Count == 0)
            return BadRequest(new { message = "No image files were provided." });

        var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "properties", id.ToString());
        Directory.CreateDirectory(uploadsFolder);

        var createdImages = new List<PropertyImageDto>();
        bool hasPrimary = property.Images.Any(i => i.IsPrimary);

        for (int i = 0; i < files.Count; i++)
        {
            var file = files[i];
            if (file.Length == 0) continue;

            var ext = Path.GetExtension(file.FileName);
            if (string.IsNullOrEmpty(ext)) ext = ".jpg";
            var fileName = $"{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var webUrl = $"/uploads/properties/{id}/{fileName}";
            var isPrimary = !hasPrimary && i == 0;
            if (isPrimary) hasPrimary = true;

            var image = new PropertyImageRecord
            {
                PropertyId = id,
                ImageUrl = webUrl,
                StoragePath = fullPath,
                IsPrimary = isPrimary,
                DisplayOrder = property.Images.Count + i,
                CreatedAt = DateTime.UtcNow
            };

            db.PropertyImages.Add(image);
            createdImages.Add(new PropertyImageDto(image.Id, image.ImageUrl, image.IsPrimary, image.DisplayOrder));
        }

        property.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(createdImages);
    }

    [HttpPut("{id:guid}/approval")]
    [Authorize(Roles = PlatformRoles.Admin)]
    public async Task<IActionResult> Review(Guid id, ListingReviewRequest request)
    {
        var property = await db.Properties.Include(p => p.Agent).SingleOrDefaultAsync(p => p.Id == id);
        if (property is null) return NotFound();
        if (property.Status != PlatformStatuses.PropertyPendingApproval)
            return Conflict(new { message = "Only listings pending approval can be reviewed." });

        var previous = new { property.Status, property.AdminApproved };
        property.Status = request.Approved ? PlatformStatuses.PropertyPublished : PlatformStatuses.PropertyRejected;
        property.AdminApproved = request.Approved;
        property.PublishedAt = request.Approved ? DateTime.UtcNow : null;

        // Apply admin configured field visibility controls
        if (request.ShowPhotos.HasValue) property.ShowPhotos = request.ShowPhotos.Value;
        if (request.ShowAskingPrice.HasValue) property.ShowAskingPrice = request.ShowAskingPrice.Value;
        if (request.ShowNegotiablePrice.HasValue) property.ShowNegotiablePrice = request.ShowNegotiablePrice.Value;
        if (request.ShowExactAddress.HasValue) property.ShowExactAddress = request.ShowExactAddress.Value;
        if (request.ShowDescription.HasValue) property.ShowDescription = request.ShowDescription.Value;
        if (request.ShowSellerContact.HasValue) property.ShowSellerContact = request.ShowSellerContact.Value;
        if (!string.IsNullOrWhiteSpace(request.AdminComment)) property.AdminComment = request.AdminComment.Trim();

        property.UpdatedAt = DateTime.UtcNow;

        if (property.Agent is not null)
        {
            db.Notifications.Add(new PlatformNotification
            {
                UserId = property.Agent.UserId,
                Type = request.Approved ? "LISTING_APPROVED" : "LISTING_REJECTED",
                Title = request.Approved ? "Listing Approved & Published" : "Listing Rejected by Admin",
                Message = request.Approved
                    ? $"Your listing '{property.Title}' has been approved and published to the marketplace."
                    : $"Your listing '{property.Title}' was rejected. Reason: {request.AdminComment ?? "Please review details"}",
                PropertyId = property.Id
            });
        }

        AuditLogWriter.Add(db, CurrentUserId(), request.Approved ? "LISTING_APPROVED" : "LISTING_REJECTED", "properties", id, previous, new { property.Status, property.AdminApproved, property.ShowNegotiablePrice, property.ShowExactAddress });
        await db.SaveChangesAsync();
        return Ok(ToDetail(property));
    }

    [HttpPut("{id:guid}/visibility")]
    [Authorize(Roles = PlatformRoles.Admin)]
    public async Task<IActionResult> UpdateVisibility(Guid id, UpdateVisibilityRequest request)
    {
        var property = await db.Properties.SingleOrDefaultAsync(p => p.Id == id);
        if (property is null) return NotFound();

        property.ShowPhotos = request.ShowPhotos;
        property.ShowAskingPrice = request.ShowAskingPrice;
        property.ShowNegotiablePrice = request.ShowNegotiablePrice;
        property.ShowExactAddress = request.ShowExactAddress;
        property.ShowDescription = request.ShowDescription;
        property.ShowSellerContact = request.ShowSellerContact;
        if (request.AdminComment is not null) property.AdminComment = request.AdminComment.Trim();

        property.UpdatedAt = DateTime.UtcNow;
        AuditLogWriter.Add(db, CurrentUserId(), "PROPERTY_VISIBILITY_UPDATED", "properties", id, newValue: request);
        await db.SaveChangesAsync();
        return Ok(ToDetail(property));
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private async Task<Guid?> CurrentAgentId() => (await db.Agents.AsNoTracking().SingleOrDefaultAsync(a => a.UserId == CurrentUserId()))?.Id;
    private static string? PrimaryImage(PlatformProperty property) => property.Images.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.DisplayOrder).Select(i => i.ImageUrl).FirstOrDefault();

    private static MarketplacePropertyDto ToMarketplace(PlatformProperty p)
    {
        var photos = p.ShowPhotos
            ? p.Images.OrderBy(i => i.DisplayOrder).Select(i => new PropertyImageDto(i.Id, i.ImageUrl, i.IsPrimary, i.DisplayOrder)).ToList()
            : new List<PropertyImageDto>();

        var primaryPhoto = p.ShowPhotos ? PrimaryImage(p) : null;
        var address = p.ShowExactAddress ? p.Address : (!string.IsNullOrWhiteSpace(p.Locality) ? p.Locality : $"{p.City}, {p.State}");
        var askingPrice = p.ShowAskingPrice ? p.Price : (decimal?)null;
        var negotiablePrice = p.ShowNegotiablePrice ? p.NegotiablePrice : (decimal?)null;
        var description = p.ShowDescription ? p.Description : null;
        var agentName = p.ShowSellerContact ? p.Agent?.Profile?.FullName : null;
        var agencyName = p.ShowSellerContact ? p.Agent?.AgencyName : null;

        return new MarketplacePropertyDto(
            p.Id,
            p.AgentId,
            p.Title,
            description,
            p.PropertyType,
            p.ListingType,
            askingPrice,
            negotiablePrice,
            address,
            p.Locality,
            p.City,
            p.State,
            p.Bedrooms,
            p.Bathrooms,
            p.Area,
            p.AreaUnit,
            primaryPhoto,
            photos,
            p.ShowPhotos,
            p.ShowAskingPrice,
            p.ShowNegotiablePrice,
            p.ShowExactAddress,
            p.ShowDescription,
            p.ShowSellerContact,
            agentName,
            agencyName,
            p.CreatedAt
        );
    }

    private static PropertyDetailDto ToDetail(PlatformProperty p) => new(
        p.Id,
        p.AgentId,
        p.Title,
        p.Description,
        p.PropertyType,
        p.ListingType,
        p.Price,
        p.NegotiablePrice,
        p.Address,
        p.Locality,
        p.City,
        p.State,
        p.Pincode,
        p.LocationUrl,
        p.Latitude,
        p.Longitude,
        p.Bedrooms,
        p.Bathrooms,
        p.Area,
        p.AreaUnit,
        p.Status,
        p.AdminApproved,
        p.ShowPhotos,
        p.ShowAskingPrice,
        p.ShowNegotiablePrice,
        p.ShowExactAddress,
        p.ShowDescription,
        p.ShowSellerContact,
        p.AdminComment,
        p.Agent?.Profile?.FullName,
        p.Agent?.AgencyName,
        p.CreatedAt,
        p.Images.OrderBy(i => i.DisplayOrder).Select(i => new PropertyImageDto(i.Id, i.ImageUrl, i.IsPrimary, i.DisplayOrder)).ToList()
    );
}

public class CreatePropertyRequest
{
    [Required] public string Status { get; init; } = PlatformStatuses.PropertyPendingApproval;
    [Required, StringLength(180)] public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    [Required, StringLength(60)] public string PropertyType { get; init; } = string.Empty;
    [Required, StringLength(30)] public string ListingType { get; init; } = "SALE";
    [Range(typeof(decimal), "0.01", "999999999999.99")] public decimal Price { get; init; }
    [Range(typeof(decimal), "0.01", "999999999999.99")] public decimal? NegotiablePrice { get; init; }
    [Required, StringLength(300)] public string Address { get; init; } = string.Empty;
    [StringLength(160)] public string? Locality { get; init; }
    [Required, StringLength(100)] public string City { get; init; } = string.Empty;
    [Required, StringLength(100)] public string State { get; init; } = string.Empty;
    [StringLength(20)] public string? Pincode { get; init; }
    [StringLength(2048)] public string? LocationUrl { get; init; }
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
    [Required, StringLength(2048)] public string ImageUrl { get; init; } = string.Empty;
    [Required, StringLength(2048)] public string StoragePath { get; init; } = string.Empty;
    public bool IsPrimary { get; init; }
    [Range(0, 1000)] public int DisplayOrder { get; init; }
}

public sealed record ListingReviewRequest(
    bool Approved,
    bool? ShowPhotos = null,
    bool? ShowAskingPrice = null,
    bool? ShowNegotiablePrice = null,
    bool? ShowExactAddress = null,
    bool? ShowDescription = null,
    bool? ShowSellerContact = null,
    string? AdminComment = null
);

public sealed record UpdateVisibilityRequest(
    bool ShowPhotos,
    bool ShowAskingPrice,
    bool ShowNegotiablePrice,
    bool ShowExactAddress,
    bool ShowDescription,
    bool ShowSellerContact,
    string? AdminComment
);

public sealed record UpdatePropertyStatusRequest(string Status);

public sealed record PropertyImageDto(Guid Id, string ImageUrl, bool IsPrimary, int DisplayOrder);

public sealed record MarketplacePropertyDto(
    Guid Id,
    Guid AgentId,
    string Title,
    string? Description,
    string PropertyType,
    string ListingType,
    decimal? AskingPrice,
    decimal? NegotiablePrice,
    string Address,
    string? Locality,
    string City,
    string State,
    int? Bedrooms,
    int? Bathrooms,
    decimal? Area,
    string? AreaUnit,
    string? PhotoUrl,
    IReadOnlyList<PropertyImageDto> Images,
    bool ShowPhotos,
    bool ShowAskingPrice,
    bool ShowNegotiablePrice,
    bool ShowExactAddress,
    bool ShowDescription,
    bool ShowSellerContact,
    string? AgentName,
    string? AgencyName,
    DateTime CreatedAt
);

public sealed record PropertyDetailDto(
    Guid Id,
    Guid AgentId,
    string Title,
    string? Description,
    string PropertyType,
    string ListingType,
    decimal Price,
    decimal? NegotiablePrice,
    string Address,
    string? Locality,
    string City,
    string State,
    string? Pincode,
    string? LocationUrl,
    decimal? Latitude,
    decimal? Longitude,
    int? Bedrooms,
    int? Bathrooms,
    decimal? Area,
    string? AreaUnit,
    string Status,
    bool AdminApproved,
    bool ShowPhotos,
    bool ShowAskingPrice,
    bool ShowNegotiablePrice,
    bool ShowExactAddress,
    bool ShowDescription,
    bool ShowSellerContact,
    string? AdminComment,
    string? AgentName,
    string? AgencyName,
    DateTime CreatedAt,
    IReadOnlyList<PropertyImageDto> Images
);
