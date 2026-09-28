namespace proj1.Models;

public class CustomerPropertyImage
{
    public int Id { get; set; }

    public int CustomerPropertyId { get; set; }

    public string ImageUrl { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public CustomerProperty CustomerProperty { get; set; } = null!;
}