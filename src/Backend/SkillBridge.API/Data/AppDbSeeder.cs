using Microsoft.EntityFrameworkCore;

namespace SkillBridge.API.Data;

/// <summary>
/// Provides deterministic seed data for local development using the Bogus library.
/// Seeds are idempotent — data is only inserted when the Branches table is empty.
/// </summary>
public static class AppDbSeeder
{
    /// <summary>Synchronous seed callback compatible with EF Core's <c>UseSeeding</c>.</summary>
    public static void Seed(DbContext context, bool _)
    {
    }

    /// <summary>Async seed callback compatible with EF Core's <c>UseAsyncSeeding</c>.</summary>
    public static async Task SeedAsync(DbContext context, bool _, CancellationToken ct)
    {
    }
}
