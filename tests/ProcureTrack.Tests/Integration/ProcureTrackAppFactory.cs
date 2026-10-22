using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using ProcureTrack.Web.Data;

namespace ProcureTrack.Tests.Integration;

/// <summary>
/// Boots the real applicaiton in memory against an isolated SQL Server test database
/// Shared by all integration tests in one test class (see IClassFixture)
/// </summary>
public class ProcureTrackAppFactory : WebApplicationFactory<Program>
{
    public const string TestDatabaseName = "ProcureTrack_Tests";

    public const string TestConnectionString = 
    $"Server=(localdb)\\MSSQLLocalDB;Database={TestDatabaseName};Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

    public ProcureTrackAppFactory()
    {
        // Start every test run from an empty database. On startup, Program.cs applies migrations and seeds sample data.
        var options = new DbContextOptionsBuilder<ProcureTrackDbContext>()
            .UseSqlServer(TestConnectionString)
            .Options;

        using var db = new ProcureTrackDbContext(options);
        db.Database.EnsureDeleted();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:ProcureTrack", TestConnectionString);
    }
}