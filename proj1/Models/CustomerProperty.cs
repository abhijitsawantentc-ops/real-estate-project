namespace proj1.Models;

public class CustomerProperty
{
    public int Id { get; set; }

    // Customer who submitted the property
    public int CustomerId { get; set; }

    // Basic details
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string PropertyType { get; set; } = string.Empty;

    public string ListingType { get; set; } = "Sale";

    // Location
    public string Address { get; set; } = string.Empty;

    public string Locality { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string? Pincode { get; set; }

    public string? LocationUrl { get; set; }

    // Pricing
    public decimal AskingPrice { get; set; }

    public decimal? NegotiablePrice { get; set; }

    public decimal? LocalityAveragePrice { get; set; }

    public decimal? PricePerSqFt { get; set; }

    // Property information
    public decimal? AreaSqFt { get; set; }

    public int? Bedrooms { get; set; }

    public int? Bathrooms { get; set; }

    public int? Floor { get; set; }

    public bool Parking { get; set; }

    public bool Furnished { get; set; }

    // Approval
    public string Status { get; set; } = "pending_approval";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ApprovedAt { get; set; }

    public int? ApprovedBy { get; set; }

    public string? AdminComment { get; set; }

    // Customer visibility controls
    public bool ShowPhotos { get; set; } = true;

    public bool ShowAskingPrice { get; set; } = true;

    public bool ShowNegotiablePrice { get; set; } = true;

    public bool ShowLocalityAveragePrice { get; set; } = true;

    public bool ShowLocation { get; set; } = true;

    public bool ShowOwnerName { get; set; } = false;

    public bool ShowOwnerContact { get; set; } = false;

    public bool ShowDescription { get; set; } = true;

    public List<CustomerPropertyImage> Images { get; set; }
        = new();
}