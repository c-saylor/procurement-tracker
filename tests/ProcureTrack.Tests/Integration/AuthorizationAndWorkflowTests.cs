using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProcureTrack.Web.Data;
using ProcureTrack.Web.Models;

namespace ProcureTrack.Tests.Integration;

/// <summary>
/// End-to-end HTTP tests for security-critical behavior: sign-in, roles, ownership,
/// antiforgery, over-posting, and the approval workflow through the real pipeline and database.
/// All integration tests live in this one class so they never race on the shared test database.
/// </summary>
public class AuthorizationAndWorkflowTests : IClassFixture<ProcureTrackAppFactory>
{
    private readonly ProcureTrackAppFactory _factory;

    public AuthorizationAndWorkflowTests(ProcureTrackAppFactory factory)
    {
        _factory = factory;
    }

    // ---------- Safety check ----------

    [Fact]
    public async Task Factory_UsesIsolatedTestDatabase()
    {
        var databaseName = await WithDbAsync(db => Task.FromResult(db.Database.GetDbConnection().Database));

        Assert.Equal(ProcureTrackAppFactory.TestDatabaseName, databaseName);
    }

    // ---------- Authentication ----------

    [Fact]
    public async Task Anonymous_Dashboard_RedirectsToSignIn()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Anonymous_SignInPage_IsAvailable()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/Account/Login");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Sign in to ProcureTrack", await response.Content.ReadAsStringAsync());
    }

    // ---------- Role-based authorization ----------

    [Fact]
    public async Task Requester_CannotPostDecision_IsDeniedAndNothingChanges()
    {
        var requestId = await CreatePendingRequestAsync(requestedBy: "Integration Requester");
        var client = CreateClient();
        await SignInAsAsync(client, "dana"); // Requester only

        var response = await PostDecisionAsync(client, requestId, "Approved", notes: null);

        AssertRedirectsToAccessDenied(response);
        var request = await GetRequestAsync(requestId);
        Assert.Equal(RequestStatus.Pending, request.Status);
        Assert.Empty(request.ApprovalRecords);
    }

    [Fact]
    public async Task Approver_CannotOpenCreatePage()
    {
        var client = CreateClient();
        await SignInAsAsync(client, "morgan"); // Approver only

        var response = await client.GetAsync("/PurchaseRequests/Create");

        AssertRedirectsToAccessDenied(response);
    }

    // ---------- Ownership ----------

    [Fact]
    public async Task Requester_CannotEditSomeoneElsesRequest()
    {
        var othersRequestId = await FindSeededRequestIdAsync("Monitor upgrades for analysts (8)"); // Marcus Bell, Pending
        var client = CreateClient();
        await SignInAsAsync(client, "dana");

        var response = await client.GetAsync($"/PurchaseRequests/Edit/{othersRequestId}");

        AssertRedirectsToAccessDenied(response);
    }

    [Fact]
    public async Task Owner_CannotEditApprovedRequest_IsRedirectedToDetails()
    {
        var approvedRequestId = await FindSeededRequestIdAsync("Ergonomic office chairs (12)"); // Dana, Approved
        var client = CreateClient();
        await SignInAsAsync(client, "dana");

        var response = await client.GetAsync($"/PurchaseRequests/Edit/{approvedRequestId}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.ToString();
        Assert.Contains($"/PurchaseRequests/Details/{approvedRequestId}", location);
        Assert.DoesNotContain("AccessDenied", location);
    }

    // ---------- Approval workflow through HTTP ----------

    [Fact]
    public async Task Approver_Approve_RecordsDecisionWithSignedInIdentity()
    {
        var requestId = await CreatePendingRequestAsync(requestedBy: "Integration Requester");
        var client = CreateClient();
        await SignInAsAsync(client, "morgan");

        var response = await PostDecisionAsync(client, requestId, "Approved", notes: "Approved by integration test.");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var request = await GetRequestAsync(requestId);
        Assert.Equal(RequestStatus.Approved, request.Status);
        var record = Assert.Single(request.ApprovalRecords);
        Assert.Equal(ApprovalAction.Approved, record.Action);
        Assert.Equal("Morgan Alvarez", record.ActedBy); // from the sign-in cookie, not the form
    }

    [Fact]
    public async Task Approver_RejectWithoutReason_ShowsErrorAndChangesNothing()
    {
        var requestId = await CreatePendingRequestAsync(requestedBy: "Integration Requester");
        var client = CreateClient();
        await SignInAsAsync(client, "morgan");

        var response = await PostDecisionAsync(client, requestId, "Rejected", notes: "   ");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // form redisplayed
        Assert.Contains("A reason is required to reject a request.", await response.Content.ReadAsStringAsync());
        var request = await GetRequestAsync(requestId);
        Assert.Equal(RequestStatus.Pending, request.Status);
        Assert.Empty(request.ApprovalRecords);
    }

    [Fact]
    public async Task Approver_CannotDecideOwnRequest_SeparationOfDuties()
    {
        var requestId = await CreatePendingRequestAsync(requestedBy: "Priya Natarajan");
        var client = CreateClient();
        await SignInAsAsync(client, "priya"); // Requester + Approver

        var response = await PostDecisionAsync(client, requestId, "Approved", notes: null);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var request = await GetRequestAsync(requestId);
        Assert.Equal(RequestStatus.Pending, request.Status);
        Assert.Empty(request.ApprovalRecords);
    }

    // ---------- Antiforgery and over-posting ----------

    [Fact]
    public async Task PostWithoutAntiforgeryToken_IsRejectedWith400()
    {
        var requestId = await CreatePendingRequestAsync(requestedBy: "Integration Requester");
        var rowVersion = await GetRowVersionAsync(requestId);
        var client = CreateClient();
        await SignInAsAsync(client, "morgan");

        var response = await client.PostAsync($"/PurchaseRequests/Decide/{requestId}", Form(
            ("Decision.Action", "Approved"),
            ("Decision.RowVersion", Convert.ToBase64String(rowVersion))));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(RequestStatus.Pending, (await GetRequestAsync(requestId)).Status);
    }

    [Fact]
    public async Task Create_IgnoresOverpostedStatusRequesterAndDate()
    {
        var title = $"Overpost test {Guid.NewGuid():N}";
        var (departmentId, vendorId) = await WithDbAsync(async db => (
            await db.Departments.Select(d => d.Id).FirstAsync(),
            await db.Vendors.Where(v => v.IsActive).Select(v => v.Id).FirstAsync()));

        var client = CreateClient();
        await SignInAsAsync(client, "dana");
        var token = await GetAntiforgeryTokenAsync(client, "/PurchaseRequests/Create");

        var response = await client.PostAsync("/PurchaseRequests/Create", Form(
            ("__RequestVerificationToken", token),
            ("Title", title),
            ("DepartmentId", departmentId.ToString()),
            ("VendorId", vendorId.ToString()),
            ("Amount", "250.00"),
            ("RequiredByDate", DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30).ToString("yyyy-MM-dd")),
            ("BusinessJustification", "Integration test for over-posting protection."),
            // Fields an attacker might add. None exist on the form view model, so none should bind.
            ("Status", "Approved"),
            ("RequestedBy", "Mallory Attacker"),
            ("SubmittedDate", "2000-01-01")));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var created = await WithDbAsync(db => db.PurchaseRequests.AsNoTracking().SingleAsync(p => p.Title == title));
        Assert.Equal(RequestStatus.Pending, created.Status);
        Assert.Equal("Dana Whitfield", created.RequestedBy);
        Assert.True(created.SubmittedDate > DateTime.UtcNow.AddMinutes(-5));
    }

    // ---------- Error handling ----------

    [Fact]
    public async Task MissingRequest_Returns404()
    {
        var client = CreateClient();
        await SignInAsAsync(client, "dana");

        var response = await client.GetAsync("/PurchaseRequests/Details/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- Helpers ----------

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,                    // assert redirects explicitly
        BaseAddress = new Uri("https://localhost"),   // the auth cookie is Secure-only
    });

    private static FormUrlEncodedContent Form(params (string Key, string Value)[] fields) =>
        new(fields.Select(f => new KeyValuePair<string, string>(f.Key, f.Value)));

    /// <summary>Reads the hidden antiforgery token that the form tag helper renders on a page.</summary>
    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string url)
    {
        var html = await client.GetStringAsync(url);
        var match = Regex.Match(html, @"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""");
        Assert.True(match.Success, $"No antiforgery token found on {url}.");
        return match.Groups[1].Value;
    }

    /// <summary>Signs in through the real demo sign-in form; the client's cookie jar keeps the session.</summary>
    private static async Task SignInAsAsync(HttpClient client, string userKey)
    {
        var token = await GetAntiforgeryTokenAsync(client, "/Account/Login");
        var response = await client.PostAsync("/Account/Login", Form(
            ("__RequestVerificationToken", token),
            ("userKey", userKey)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private async Task<HttpResponseMessage> PostDecisionAsync(HttpClient client, int requestId, string action, string? notes)
    {
        // Every signed-in page contains a token (the sign-out form), and tokens are tied to the signed-in user.
        var token = await GetAntiforgeryTokenAsync(client, "/");
        var rowVersion = await GetRowVersionAsync(requestId);

        return await client.PostAsync($"/PurchaseRequests/Decide/{requestId}", Form(
            ("__RequestVerificationToken", token),
            ("Decision.Action", action),
            ("Decision.Notes", notes ?? string.Empty),
            ("Decision.RowVersion", Convert.ToBase64String(rowVersion))));
    }

    private static void AssertRedirectsToAccessDenied(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/AccessDenied", response.Headers.Location?.ToString());
    }

    /// <summary>Runs work against the app's own DbContext in a fresh scope (like one HTTP request).</summary>
    private async Task<T> WithDbAsync<T>(Func<ProcureTrackDbContext, Task<T>> work)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcureTrackDbContext>();
        return await work(db);
    }

    private Task<int> CreatePendingRequestAsync(string requestedBy) => WithDbAsync(async db =>
    {
        var request = new PurchaseRequest
        {
            Title = $"Integration test request {Guid.NewGuid():N}",
            DepartmentId = await db.Departments.Select(d => d.Id).FirstAsync(),
            VendorId = await db.Vendors.Where(v => v.IsActive).Select(v => v.Id).FirstAsync(),
            Amount = 1234.56m,
            BusinessJustification = "Created by an automated integration test.",
            RequestedBy = requestedBy,
            SubmittedDate = DateTime.UtcNow,
            RequiredByDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30),
            Status = RequestStatus.Pending,
        };
        db.PurchaseRequests.Add(request);
        await db.SaveChangesAsync();
        return request.Id;
    });

    private Task<int> FindSeededRequestIdAsync(string title) =>
        WithDbAsync(db => db.PurchaseRequests.Where(p => p.Title == title).Select(p => p.Id).SingleAsync());

    private Task<byte[]> GetRowVersionAsync(int requestId) =>
        WithDbAsync(db => db.PurchaseRequests.Where(p => p.Id == requestId).Select(p => p.RowVersion).SingleAsync());

    private Task<PurchaseRequest> GetRequestAsync(int requestId) =>
        WithDbAsync(db => db.PurchaseRequests
            .AsNoTracking()
            .Include(p => p.ApprovalRecords)
            .SingleAsync(p => p.Id == requestId));
}