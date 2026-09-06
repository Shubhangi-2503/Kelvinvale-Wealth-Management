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
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/products/{CustomWebApplicationFactory.AliceIsaProductId}/instructions");

        request.Headers.Add("X-Caller-Id", CustomWebApplicationFactory.CustomerAliceId.ToString());
        request.Content = JsonContent.Create(new
        {
            Type = "Withdrawal",
            AmountPence = 6000000, // Seeded holding is 500,000 pence; 6000,000 triggers the check
            FundCode = "GLB-EQ-ACC"
        });

        var response = await _client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
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

    [Test]
    public async Task PostInstruction_WhenSwitchSourceAndTargetAreSame_ReturnsBadRequest400()
    {
        // Arrange: Alice attempts to switch GLB-EQ-ACC into GLB-EQ-ACC
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/products/{CustomWebApplicationFactory.AliceIsaProductId}/instructions");

        request.Headers.Add("X-Caller-Id", CustomWebApplicationFactory.CustomerAliceId.ToString());
        request.Content = JsonContent.Create(new
        {
            Type = "Switch",
            AmountPence = 50000,
            FundCode = "GLB-EQ-ACC",
            TargetFundCode = "GLB-EQ-ACC"
        });

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Does.Contain("cannot be identical").IgnoreCase);
    }

    [Test]
    public async Task PostInstruction_WhenSwitchAmountExceedsBalance_ReturnsBadRequest400()
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/products/{CustomWebApplicationFactory.AliceIsaProductId}/instructions");

        request.Headers.Add("X-Caller-Id", CustomWebApplicationFactory.CustomerAliceId.ToString());
        request.Content = JsonContent.Create(new
        {
            Type = "Switch",
            AmountPence = 500001, // Must be > 500,000 pence to exceed holding
            FundCode = "GLB-EQ-ACC",
            TargetFundCode = "UK-BND-INC" // Must be a valid second fund
        });

        var response = await _client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task PostInstruction_ValidSwitch_CreatesInstructionAndReturns201()
    {
        // Arrange: Alice switches £2,000 from Global Equity to UK Corporate Bond
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/products/{CustomWebApplicationFactory.AliceIsaProductId}/instructions");

        request.Headers.Add("X-Caller-Id", CustomWebApplicationFactory.CustomerAliceId.ToString());
        request.Content = JsonContent.Create(new
        {
            Type = "Switch",
            AmountPence = 200000,
            FundCode = "GLB-EQ-ACC",
            TargetFundCode = "UK-CORP-BND",
            ClientReference = "UNIT-TEST-SWI-01"
        });

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }
    [Test]
    public async Task PostInstruction_WhenIsaSubscriptionExceedsAnnualAllowance_ReturnsBadRequest400()
    {
        //  Arrange: Attempt to subscribe £20,000.01 (2,000,001 pence) in a single instruction
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/products/{CustomWebApplicationFactory.AliceIsaProductId}/instructions");

        request.Headers.Add("X-Caller-Id", CustomWebApplicationFactory.CustomerAliceId.ToString());
        request.Content = JsonContent.Create(new
        {
            Type = "Subscription",
            AmountPence = 2000001, // Exceeds 2,000,000 pence
            FundCode = "GLB-EQ-ACC"
        });

        // Act 
        var response = await _client.SendAsync(request);

        // Assert 
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.That(responseBody, Does.Contain("ISA Allowance Exceeded"));
        Assert.That(responseBody, Does.Contain("exceeds remaining allowance"));
    }
    [Test]
    public async Task PostInstruction_WhenCumulativeSubscriptionsExceedAllowance_ReturnsBadRequest400()
    {
        // Arrange: First valid subscription of £15,000 (1,500,000 pence)
        var firstRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/products/{CustomWebApplicationFactory.AliceIsaProductId}/instructions");

        firstRequest.Headers.Add("X-Caller-Id", CustomWebApplicationFactory.CustomerAliceId.ToString());
        firstRequest.Content = JsonContent.Create(new
        {
            Type = "Subscription",
            AmountPence = 1500000,
            FundCode = "GLB-EQ-ACC",
            ClientReference = "SUB-PART-1"
        });

        var firstResponse = await _client.SendAsync(firstRequest);
        Assert.That(firstResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        // Act: Second subscription of £6,000 (600,000 pence), which brings the total to £21,000
        var secondRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/products/{CustomWebApplicationFactory.AliceIsaProductId}/instructions");

        secondRequest.Headers.Add("X-Caller-Id", CustomWebApplicationFactory.CustomerAliceId.ToString());
        secondRequest.Content = JsonContent.Create(new
        {
            Type = "Subscription",
            AmountPence = 600000,
            FundCode = "GLB-EQ-ACC",
            ClientReference = "SUB-PART-2"
        });

        var secondResponse = await _client.SendAsync(secondRequest);

        // Assert 
        Assert.That(secondResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var responseBody = await secondResponse.Content.ReadAsStringAsync();
        Assert.That(responseBody, Does.Contain("ISA Allowance Exceeded"));
    }
}