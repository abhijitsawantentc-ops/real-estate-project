using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using proj1.Data;
using proj1.Models;

namespace proj1.Services;

public static class AdminSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, ILogger logger)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Profile>>();

        // Seed default admin accounts
        foreach (var adminEmail in new[] { "admin@horizon.local", "admin@haven.local" })
        {
            var adminProfile = await db.Profiles.SingleOrDefaultAsync(x => x.Email == adminEmail);
            if (adminProfile is null)
            {
                adminProfile = new Profile
                {
                    Email = adminEmail,
                    FullName = "Horizon Admin",
                    Role = PlatformRoles.Admin,
                    IsActive = true
                };
                adminProfile.PasswordHash = hasher.HashPassword(adminProfile, "Admin");
                db.Profiles.Add(adminProfile);
                await db.SaveChangesAsync();
                logger.LogInformation("Administrator created ({Email}).", adminEmail);
            }
        }

        // Seed default seller / agent account
        Agent? primaryAgent = null;
        foreach (var sellerEmail in new[] { "seller@horizon.local", "seller@haven.local" })
        {
            var sellerProfile = await db.Profiles.SingleOrDefaultAsync(x => x.Email == sellerEmail);
            if (sellerProfile is null)
            {
                sellerProfile = new Profile
                {
                    Email = sellerEmail,
                    FullName = "Abhijit Sawant (Seller)",
                    Role = PlatformRoles.Agent,
                    Phone = "+1 555-019-2834",
                    IsActive = true
                };
                sellerProfile.PasswordHash = hasher.HashPassword(sellerProfile, "Seller");
                db.Profiles.Add(sellerProfile);
                await db.SaveChangesAsync();

                var agent = new Agent
                {
                    UserId = sellerProfile.Id,
                    AgencyName = "Horizon Realty Group",
                    LicenseNumber = "HR-98402",
                    Phone = sellerProfile.Phone,
                    City = "San Jose",
                    State = "CA",
                    VerificationStatus = PlatformStatuses.AgentApproved
                };
                db.Agents.Add(agent);
                await db.SaveChangesAsync();
                if (primaryAgent is null) primaryAgent = agent;
                logger.LogInformation("Seller created ({Email}).", sellerEmail);
            }
            else
            {
                var ag = await db.Agents.SingleOrDefaultAsync(a => a.UserId == sellerProfile.Id);
                if (primaryAgent is null && ag is not null) primaryAgent = ag;
            }
        }

        // Seed default buyer / customer account
        foreach (var buyerEmail in new[] { "buyer@horizon.local", "buyer@haven.local" })
        {
            var buyerProfile = await db.Profiles.SingleOrDefaultAsync(x => x.Email == buyerEmail);
            if (buyerProfile is null)
            {
                buyerProfile = new Profile
                {
                    Email = buyerEmail,
                    FullName = "Priya Sharma (Buyer)",
                    Role = PlatformRoles.Customer,
                    Phone = "+1 555-014-9988",
                    IsActive = true
                };
                buyerProfile.PasswordHash = hasher.HashPassword(buyerProfile, "Buyer");
                db.Profiles.Add(buyerProfile);
                await db.SaveChangesAsync();
                logger.LogInformation("Buyer created ({Email}).", buyerEmail);
            }
        }

        // Ensure user account abhijit.sawant.entc@gmail.com is set up as an approved Agent/Seller with Horizon Realty
        const string userEmail = "abhijit.sawant.entc@gmail.com";
        var userProfile = await db.Profiles.SingleOrDefaultAsync(x => x.Email == userEmail);
        if (userProfile is not null)
        {
            userProfile.Role = PlatformRoles.Agent;
            userProfile.IsActive = true;
            
            var userAgent = await db.Agents.SingleOrDefaultAsync(a => a.UserId == userProfile.Id);
            if (userAgent is null)
            {
                userAgent = new Agent
                {
                    UserId = userProfile.Id,
                    AgencyName = "Horizon Realty - Abhijit Sawant",
                    Phone = userProfile.Phone ?? "+1 555-010-0000",
                    City = "San Jose",
                    State = "CA",
                    VerificationStatus = PlatformStatuses.AgentApproved
                };
                db.Agents.Add(userAgent);
            }
            else
            {
                userAgent.VerificationStatus = PlatformStatuses.AgentApproved;
                userAgent.AgencyName = "Horizon Realty - Abhijit Sawant";
            }
            await db.SaveChangesAsync();
            if (primaryAgent is null) primaryAgent = userAgent;
            logger.LogInformation("Account abhijit.sawant.entc@gmail.com verified as Horizon Realty Agent.");
        }

        // Seed top featured properties if none exist, so the top properties window in seller dashboard is populated
        if (primaryAgent != null && !await db.Properties.AnyAsync())
        {
            var sampleProps = new[]
            {
                new {
                    Title = "Modern Panoramic Hillside Villa",
                    Type = "Villa",
                    Price = 1250000m,
                    Floor = 1180000m,
                    Address = "820 Skyview Ridge",
                    City = "San Jose",
                    State = "CA",
                    Desc = "Custom architect-designed 4-bedroom villa featuring floor-to-ceiling glass walls, private terrace deck, and panoramic city views.",
                    Image = "https://images.unsplash.com/photo-1600596542815-ffad4c1539a9?auto=format&fit=crop&w=1200&q=80",
                    ShowPhotos = true, ShowPrice = true, ShowFloor = true, ShowAddress = true
                },
                new {
                    Title = "Horizon Luxury Downtown Penthouse",
                    Type = "Penthouse",
                    Price = 890000m,
                    Floor = 840000m,
                    Address = "350 Silicon Valley Blvd, Penthouse 18",
                    City = "San Jose",
                    State = "CA",
                    Desc = "Skyline penthouse with private wrap-around terrace, bespoke European kitchen, smart home automation, and 24/7 concierge.",
                    Image = "https://images.unsplash.com/photo-1512917774080-9991f1c4c750?auto=format&fit=crop&w=1200&q=80",
                    ShowPhotos = true, ShowPrice = true, ShowFloor = false, ShowAddress = true
                },
                new {
                    Title = "Contemporary Garden Residence",
                    Type = "Apartment",
                    Price = 620000m,
                    Floor = 590000m,
                    Address = "1140 Willow Glen Way",
                    City = "San Jose",
                    State = "CA",
                    Desc = "Bright sunlit residence surrounded by landscaped gardens, modern finishes, hardwood flooring, and private two-car parking.",
                    Image = "https://images.unsplash.com/photo-1600585154340-be6161a56a0c?auto=format&fit=crop&w=1200&q=80",
                    ShowPhotos = true, ShowPrice = true, ShowFloor = true, ShowAddress = false
                }
            };

            foreach (var sp in sampleProps)
            {
                var prop = new PlatformProperty
                {
                    AgentId = primaryAgent.Id,
                    Title = sp.Title,
                    PropertyType = sp.Type,
                    ListingType = "SALE",
                    Price = sp.Price,
                    NegotiablePrice = sp.Floor,
                    Address = sp.Address,
                    City = sp.City,
                    State = sp.State,
                    Description = sp.Desc,
                    Status = PlatformStatuses.PropertyPublished,
                    AdminApproved = true,
                    ShowPhotos = sp.ShowPhotos,
                    ShowAskingPrice = sp.ShowPrice,
                    ShowNegotiablePrice = sp.ShowFloor,
                    ShowExactAddress = sp.ShowAddress,
                    ShowDescription = true,
                    ShowSellerContact = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-1),
                    PublishedAt = DateTime.UtcNow.AddDays(-1)
                };
                db.Properties.Add(prop);
                db.PropertyImages.Add(new PropertyImageRecord
                {
                    PropertyId = prop.Id,
                    ImageUrl = sp.Image,
                    StoragePath = sp.Image,
                    IsPrimary = true,
                    DisplayOrder = 0,
                    CreatedAt = DateTime.UtcNow
                });
            }
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded top featured properties for Horizon Realty marketplace window.");
        }

        // Then, create configured admin from environment if provided
        var email = configuration["Platform:AdminEmail"]?.Trim().ToLowerInvariant();
        var password = configuration["Platform:AdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogInformation("Additional admin bootstrap skipped; configure Platform__AdminEmail and Platform__AdminPassword to create another admin account.");
            return;
        }

        var profile = await db.Profiles.SingleOrDefaultAsync(x => x.Email == email);
        if (profile is null)
        {
            profile = new Profile
            {
                Email = email,
                FullName = "Horizon Administrator",
                Role = PlatformRoles.Admin,
                IsActive = true
            };
            profile.PasswordHash = hasher.HashPassword(profile, password);
            db.Profiles.Add(profile);
            await db.SaveChangesAsync();
            logger.LogInformation("Configured platform administrator created.");
        }
        else if (profile.Role != PlatformRoles.Admin)
        {
            throw new InvalidOperationException("The configured admin email is already assigned to a non-admin profile.");
        }
    }
}
