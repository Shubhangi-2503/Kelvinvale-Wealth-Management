using System.Net;
using System.Net.Http.Json;
using NUnit.Framework;

namespace Kelvinvale.Tests.IntegrationTests;

[TestFixture]
public class ProductEndpointTests
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
    public async Task OpenProduct_WhenOpeningSecondIsaInSameTaxYear_ReturnsBadRequest400()
    {
        // Arrange: Alice already has an ISA for tax year 2026 seeded in CustomWebApplicationFactory.
        // Sarah attempts to open a second ISA for Alice in 2026.
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/customers/{CustomWebApplicationFactory.CustomerAliceId}/products");
        request.Headers.Add("X-Caller-Id", CustomWebApplicationFactory.AdviserSarahId.ToString());
        request.Content = JsonContent.Create(new
        {
            ProductTypeCode = "ISA",
            TaxYear = 2026
        });

        // Act
        var response = await _client.SendAsync(request);

        // Assert: HMRC Rule - Max 1 ISA per tax year
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Does.Contain("ISA").Or.Contain("active"));
    }

    [Test]
    public async Task OpenProduct_WhenOpeningSippForMinorUnder18_ReturnsBadRequest400()
    {
        // 1. First onboard a 16-year-old minor
        var onboardRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/customers");
        onboardRequest.Headers.Add("X-Caller-Id", CustomWebApplicationFactory.AdviserSarahId.ToString());
        onboardRequest.Content = JsonContent.Create(new
        {
            UserName = "teenager.user",
            Email = "teen@example.com",
            DateOfBirth = DateTime.UtcNow.AddYears(-16) // 16 years old
        });

        var onboardResponse = await _client.SendAsync(onboardRequest);
        var createdUser = await onboardResponse.Content.ReadFromJsonAsync<CreatedUserTestDto>();
        var minorId = createdUser!.Id;

        // 2. Attempt to open a SIPP for this 16-year-old
        var productRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/customers/{minorId}/products");
        productRequest.Headers.Add("X-Caller-Id", CustomWebApplicationFactory.AdviserSarahId.ToString());
        productRequest.Content = JsonContent.Create(new
        {
            ProductTypeCode = "SIPP",
            TaxYear = 2026
        });

        // Act
        var response = await _client.SendAsync(productRequest);

        // Assert: SIPP requires >= 18 years of age
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Does.Contain("18"));
    }

    [Test]
    public async Task OpenProduct_WhenCustomerAttemptsToOpenTheirOwnProduct_ReturnsForbidden403()
    {
        // Arrange: Alice (Customer) attempts to call the product opening endpoint directly
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/customers/{CustomWebApplicationFactory.CustomerAliceId}/products");
        request.Headers.Add("X-Caller-Id", CustomWebApplicationFactory.CustomerAliceId.ToString());
        request.Content = JsonContent.Create(new
        {
            ProductTypeCode = "GIA",
            TaxYear = 2026
        });

        // Act
        var response = await _client.SendAsync(request);

        // Assert: Advisers open products; Customers cannot open accounts independently
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task OpenProduct_WhenAdviserOpensGiaForCustomer_ReturnsCreated201()
    {
        // Arrange: Sarah opens a GIA for Charlie (GIA has no age or tax year limits)
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/customers/{CustomWebApplicationFactory.CustomerCharlieId}/products");
        request.Headers.Add("X-Caller-Id", CustomWebApplicationFactory.AdviserSarahId.ToString());
        request.Content = JsonContent.Create(new
        {
            ProductTypeCode = "GIA",
            TaxYear = 2026
        });

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created).Or.EqualTo(HttpStatusCode.OK));
    }

    // Helper record for deserializing response in tests
    private record CreatedUserTestDto(Guid Id);
}