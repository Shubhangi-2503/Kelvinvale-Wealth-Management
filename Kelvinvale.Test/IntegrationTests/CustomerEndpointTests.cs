using System.Net;
using System.Net.Http.Json;
using NUnit.Framework;

namespace Kelvinvale.Tests.IntegrationTests;

[TestFixture]
public class CustomerEndpointTests
{
    private CustomWebApplicationFactory _factory;
    private HttpClient _client;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new CustomWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task OnboardCustomer_WhenCallerIsAdviser_ReturnsCreated201()
    {
        // Arrange: Sarah (Adviser) onboards Bob
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/customers");
        request.Headers.Add("X-Caller-Id", CustomWebApplicationFactory.AdviserSarahId.ToString());
        request.Content = JsonContent.Create(new
        {
            UserName = "bob.builder",
            Email = "bob.builder@example.com",
            DateOfBirth = new DateTime(1985, 3, 15, 0, 0, 0, DateTimeKind.Utc),
            Products = new[]
            {
                new
                {
                    ProductTypeCode = "GIA",
                    TaxYear = 2026,
                    InitialHoldings = new[]
                    {
                        new { FundCode = "GLB-EQ-ACC", AmountPence = 100000 }
                    }
                }
            }
        });

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created).Or.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task OnboardCustomer_WhenCallerIsCustomer_ReturnsForbidden403()
    {
        // Arrange: Alice (Customer) attempts to onboard another customer
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/customers");
        request.Headers.Add("X-Caller-Id", CustomWebApplicationFactory.CustomerAliceId.ToString());
        request.Content = JsonContent.Create(new
        {
            UserName = "dave.smith",
            Email = "dave.smith@example.com",
            DateOfBirth = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        // Act
        var response = await _client.SendAsync(request);

        // Assert: Customers are forbidden from onboarding
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task OnboardCustomer_WhenEmailAlreadyExists_ReturnsBadRequestOrConflict()
    {
        // Arrange: Try to onboard using Alice's existing email
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/customers");
        request.Headers.Add("X-Caller-Id", CustomWebApplicationFactory.AdviserSarahId.ToString());
        request.Content = JsonContent.Create(new
        {
            UserName = "alice.clone",
            Email = "alice.customer@example.com", // Seeded in DbInitializer
            DateOfBirth = new DateTime(1995, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.That(
            response.StatusCode,
            Is.EqualTo(HttpStatusCode.BadRequest).Or.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task UpdateCustomer_WhenCustomerUpdatesOwnProfile_ReturnsOk200()
    {
        // Arrange: Alice updates her own email
        var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/v1/customers/{CustomWebApplicationFactory.CustomerAliceId}");
        request.Headers.Add("X-Caller-Id", CustomWebApplicationFactory.CustomerAliceId.ToString());
        request.Content = JsonContent.Create(new
        {
            Email = "alice.newemail@example.com"
        });

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK).Or.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task UpdateCustomer_WhenCustomerUpdatesAnotherCustomer_ReturnsForbidden403()
    {
        // Arrange: Charlie attempts to modify Alice's record
        var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/v1/customers/{CustomWebApplicationFactory.CustomerAliceId}");
        request.Headers.Add("X-Caller-Id", CustomWebApplicationFactory.CustomerCharlieId.ToString());
        request.Content = JsonContent.Create(new
        {
            Email = "charlie.hijack@example.com"
        });

        // Act
        var response = await _client.SendAsync(request);

        // Assert: Cross-tenant isolation
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }
}