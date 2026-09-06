using System.Net;
using Kelvinvale.Domain.Entities;
using Kelvinvale.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Kelvinvale.Tests.IntegrationTests;

[TestFixture]
public class AuthenticationEndpointTests
{
    private CustomWebApplicationFactory _factory;
    private HttpClient _client;
    private static readonly Guid InactiveUserId = Guid.Parse("99999999-9999-9999-9999-999999999999");

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new CustomWebApplicationFactory();
        _client = _factory.CreateClient();

        // Seed a test user with IsActive = false
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KelvinvaleDbContext>();

        if (!db.Users.Any(u => u.Id == InactiveUserId))
        {
            db.Users.Add(new User
            {
                Id = InactiveUserId,
                UserName = "inactive.user",
                Email = "inactive.user@example.com",
                RoleId = DbInitializer.RoleCustomerId,
                IsActive = false,
                CreatedOn = DateTime.UtcNow
            });
            db.SaveChanges();
        }
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task Authenticate_WhenHeaderIsMissing_ReturnsUnauthorized401()
    {
        //Arrange: Omit the X-Caller-Id header completely
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/customers/{CustomWebApplicationFactory.CustomerAliceId}");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Authenticate_WhenHeaderIsInvalidGuid_ReturnsUnauthorized401()
    {
        // Arrange: Provide a string that fails Guid.TryParse
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/customers/{CustomWebApplicationFactory.CustomerAliceId}");
        request.Headers.Add("X-Caller-Id", "not-a-valid-guid");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Authenticate_WhenUserIsInactive_ReturnsUnauthorized401()
    {
        // Arrange: Provide the ID of our deactivated user
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/customers/{InactiveUserId}");
        request.Headers.Add("X-Caller-Id", InactiveUserId.ToString());

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }
}