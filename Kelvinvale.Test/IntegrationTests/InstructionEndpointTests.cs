using System.Net;
using System.Net.Http.Json;
using NUnit.Framework;

namespace Kelvinvale.Tests.IntegrationTests;

[TestFixture]
public class InstructionEndpointTests
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
    public async Task PostInstruction_WhenCallerIsAdviser_ReturnsForbidden403()
    {
        // Arrange: Sarah is an Adviser. Only sending X-Caller-Id.
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/products/{CustomWebApplicationFactory.AliceIsaProductId}/instructions");

        request.Headers.Add("X-Caller-Id", CustomWebApplicationFactory.AdviserSarahId.ToString());
        request.Content = JsonContent.Create(new
        {
            Type = "Subscription",
            AmountPence = 50000,
            FundCode = "GLB-EQ-ACC"
        });

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.That(responseBody, Does.Contain("not an authorised person"));
    }

    [Test]
    public async Task PostInstruction_WhenCustomerTouchesAnotherUsersProduct_ReturnsForbidden403()
    {
        // Arrange: Charlie attempts to place an instruction on Alice's product
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/products/{CustomWebApplicationFactory.AliceIsaProductId}/instructions");

        request.Headers.Add("X-Caller-Id", CustomWebApplicationFactory.CustomerCharlieId.ToString());
        request.Content = JsonContent.Create(new
        {
            Type = "Subscription",
            AmountPence = 50000,
            FundCode = "GLB-EQ-ACC"
        });

        // Act
        var response = await _client.SendAsync(request);

        // Assert: Ownership isolation rejects the call
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task PostInstruction_WhenWithdrawalFromSippUnderAge55_ReturnsBadRequest400()
    {
        // Arrange: Alice is 32 years old; pension age is 55
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/products/{CustomWebApplicationFactory.AliceSippProductId}/instructions");

        request.Headers.Add("X-Caller-Id", CustomWebApplicationFactory.CustomerAliceId.ToString());
        request.Content = JsonContent.Create(new
        {
            Type = "Withdrawal",
            AmountPence = 10000,
            FundCode = "GLB-EQ-ACC"
        });

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Does.Contain("55").Or.Contain("retirement age"));
    }

    [Test]
    public async Task PostInstruction_WhenWithdrawalExceedsHoldingBalance_ReturnsBadRequest400()
    {
        // Arrange: Alice has £5,000 (500,000 pence), attempts to withdraw £10,000 (1,000,000 pence)
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/products/{CustomWebApplicationFactory.AliceIsaProductId}/instructions");

        request.Headers.Add("X-Caller-Id", CustomWebApplicationFactory.CustomerAliceId.ToString());
        request.Content = JsonContent.Create(new
        {
            Type = "Withdrawal",
            AmountPence = 1000000,
            FundCode = "GLB-EQ-ACC"
        });

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Does.Contain("Insufficient funds").Or.Contain("Available"));
    }

    [Test]
    public async Task PostInstruction_ValidSubscription_CreatesInstructionAndReturns201()
    {
        // Arrange: Alice subscribes £2,000 into Global Equity Fund
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/products/{CustomWebApplicationFactory.AliceIsaProductId}/instructions");

        request.Headers.Add("X-Caller-Id", CustomWebApplicationFactory.CustomerAliceId.ToString());
        request.Content = JsonContent.Create(new
        {
            Type = "Subscription",
            AmountPence = 200000,
            FundCode = "GLB-EQ-ACC",
            ClientReference = "UNIT-TEST-SUB-01"
        });

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }
}