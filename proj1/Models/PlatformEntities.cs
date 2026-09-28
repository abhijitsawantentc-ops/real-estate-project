using System.ComponentModel.DataAnnotations;

namespace proj1.Models;

public static class PlatformRoles
{
    public const string Customer = "CUSTOMER";
    public const string Agent = "AGENT";
    public const string Admin = "ADMIN";
}

public static class PlatformStatuses
{
    public const string AgentPending = "pending";
    public const string AgentApproved = "approved";
    public const string AgentRejected = "rejected";
    public const string AgentSuspended = "suspended";

    public const string PropertyDraft = "draft";
    public const string PropertyPendingApproval = "pending_approval";
    public const string PropertyPublished = "published";
    public const string PropertyUnderOffer = "under_offer";
    public const string PropertySold = "sold";
    public const string PropertyRejected = "rejected";
    public const string PropertyInactive = "inactive";

    public const string InterestNew = "new";
    public const string InterestUnderReview = "under_review";
    public const string InterestContacted = "contacted";
    public const string InterestApproved = "approved";
    public const string InterestRejected = "rejected";
    public const string InterestConverted = "converted";
    public const string InterestClosed = "closed";

    public const string BidPendingAdmin = "pending_admin";
    public const string BidDraft = "draft";
    public const string BidSent = "sent";
    public const string BidViewed = "viewed";
    public const string BidAccepted = "accepted";
    public const string BidRejected = "rejected";
    public const string BidRejectedByAdmin = "rejected_by_admin";
    public const string BidExpired = "expired";

    public const string TransactionInitiated = "initiated";
    public const string TransactionAdminReview = "admin_review";
    public const string TransactionNegotiation = "negotiation";
    public const string TransactionAgentReview = "agent_review";
    public const string TransactionApproved = "approved";
    public const string TransactionAgreementPending = "agreement_pending";
    public const string TransactionCompleted = "completed";
    public const string TransactionCancelled = "cancelled";
}

public class Profile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(320)] public string Email { get; set; } = string.Empty;
    [MaxLength(120)] public string FullName { get; set; } = string.Empty;
    [MaxLength(40)] public string? Phone { get; set; }
    [MaxLength(30)] public string Role { get; set; } = PlatformRoles.Customer;
    [MaxLength(2048)] public string? AvatarUrl { get; set; }
    public bool IsActive { get; set; } = true;
    [MaxLength(500)] public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Agent? Agent { get; set; }
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    public ICollection<CustomerInterest> Interests { get; set; } = new List<CustomerInterest>();
    public ICollection<Bid> SubmittedBids { get; set; } = new List<Bid>();
    public ICollection<PlatformNotification> Notifications { get; set; } = new List<PlatformNotification>();
}

public class Agent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    [MaxLength(160)] public string AgencyName { get; set; } = string.Empty;
    [MaxLength(100)] public string? LicenseNumber { get; set; }
    [MaxLength(40)] public string? Phone { get; set; }
    [MaxLength(240)] public string? Address { get; set; }
    [MaxLength(100)] public string? City { get; set; }
    [MaxLength(100)] public string? State { get; set; }
    [MaxLength(30)] public string VerificationStatus { get; set; } = PlatformStatuses.AgentPending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Profile? Profile { get; set; }
    public ICollection<PlatformProperty> Properties { get; set; } = new List<PlatformProperty>();
}

public class PlatformProperty
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AgentId { get; set; }
    [MaxLength(180)] public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    [MaxLength(60)] public string PropertyType { get; set; } = string.Empty;
    [MaxLength(30)] public string ListingType { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? NegotiablePrice { get; set; }
    [MaxLength(300)] public string Address { get; set; } = string.Empty;
    [MaxLength(160)] public string? Locality { get; set; }
    [MaxLength(100)] public string City { get; set; } = string.Empty;
    [MaxLength(100)] public string State { get; set; } = string.Empty;
    [MaxLength(20)] public string? Pincode { get; set; }
    [MaxLength(2048)] public string? LocationUrl { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public int? Bedrooms { get; set; }
    public int? Bathrooms { get; set; }
    public decimal? Area { get; set; }
    [MaxLength(30)] public string? AreaUnit { get; set; }
    [MaxLength(30)] public string Status { get; set; } = PlatformStatuses.PropertyDraft;
    public bool AdminApproved { get; set; }
    public bool ShowPhotos { get; set; } = true;
    public bool ShowAskingPrice { get; set; } = true;
    public bool ShowNegotiablePrice { get; set; } = false;
    public bool ShowExactAddress { get; set; } = true;
    public bool ShowDescription { get; set; } = true;
    public bool ShowSellerContact { get; set; } = false;
    public string? AdminComment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }
    public Agent? Agent { get; set; }
    public ICollection<PropertyImageRecord> Images { get; set; } = new List<PropertyImageRecord>();
    public ICollection<CustomerInterest> Interests { get; set; } = new List<CustomerInterest>();
    public ICollection<Bid> Bids { get; set; } = new List<Bid>();
    public ICollection<PropertyTransaction> Transactions { get; set; } = new List<PropertyTransaction>();
    public ICollection<PlatformNotification> Notifications { get; set; } = new List<PlatformNotification>();
}

public class PropertyImageRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PropertyId { get; set; }
    [MaxLength(2048)] public string StoragePath { get; set; } = string.Empty;
    [MaxLength(2048)] public string ImageUrl { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public PlatformProperty? Property { get; set; }
}

public class CustomerInterest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public Guid PropertyId { get; set; }
    [MaxLength(30)] public string Status { get; set; } = PlatformStatuses.InterestNew;
    public string? CustomerMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Profile? Customer { get; set; }
    public PlatformProperty? Property { get; set; }
    public ICollection<Bid> Bids { get; set; } = new List<Bid>();
    public ICollection<PropertyTransaction> Transactions { get; set; } = new List<PropertyTransaction>();
}

public class Bid
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PropertyId { get; set; }
    public Guid AgentId { get; set; }
    public Guid? BuyerId { get; set; }
    public Guid? CustomerInterestId { get; set; }
    public bool CreatedByAdmin { get; set; } = false;
    public decimal OriginalAmount { get; set; }
    public decimal Amount { get; set; }
    public bool IsModifiedByAdmin { get; set; }
    public string? AdminNotes { get; set; }
    public string? BuyerMessage { get; set; }
    public string? Message { get; set; }
    [MaxLength(30)] public string Status { get; set; } = PlatformStatuses.BidPendingAdmin;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public PlatformProperty? Property { get; set; }
    public Agent? Agent { get; set; }
    public Profile? Buyer { get; set; }
    public CustomerInterest? CustomerInterest { get; set; }
    public ICollection<PropertyTransaction> Transactions { get; set; } = new List<PropertyTransaction>();
}

public class PropertyTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PropertyId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid AgentId { get; set; }
    public Guid? InterestId { get; set; }
    public Guid? BidId { get; set; }
    [MaxLength(40)] public string Status { get; set; } = PlatformStatuses.TransactionInitiated;
    public decimal? AgreedPrice { get; set; }
    public string? AdminNotes { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public PlatformProperty? Property { get; set; }
    public Profile? Customer { get; set; }
    public Agent? Agent { get; set; }
    public CustomerInterest? Interest { get; set; }
    public Bid? Bid { get; set; }
}

public class PlatformNotification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    [MaxLength(60)] public string Type { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid? PropertyId { get; set; }
    public Guid? BidId { get; set; }
    public Guid? TransactionId { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Profile? User { get; set; }
    public PlatformProperty? Property { get; set; }
}

public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }
    [MaxLength(100)] public string Action { get; set; } = string.Empty;
    [MaxLength(100)] public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Profile? User { get; set; }
}
