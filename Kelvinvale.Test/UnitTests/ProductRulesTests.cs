using Kelvinvale.Application.Interfaces;
using Kelvinvale.Application.Rules;
using Kelvinvale.Application.Rules.Product;
using Kelvinvale.Domain.Entities;
using Moq;
using NUnit.Framework;

namespace Kelvinvale.Tests.UnitTests;

[TestFixture]
public class ProductRulesTests
{
    private Mock<IProductRepository> _mockProductRepo;
    private IsaSingleAccountRule _isaRule;
    private SippAgeEligibilityRule _sippRule;

    [SetUp]
    public void Setup()
    {
        // 1. Create the fake/mocked repository
        _mockProductRepo = new Mock<IProductRepository>();

        // 2. Inject the mock into the rule being tested
        _isaRule = new IsaSingleAccountRule(_mockProductRepo.Object);
        _sippRule = new SippAgeEligibilityRule();
    }

    [Test]
    public async Task IsaRule_WhenCustomerAlreadyHasIsaInCurrentTaxYear_ReturnsFailure()
    {
        // Arrange (Prepare data and fake behavior)
        var customerId = Guid.NewGuid();
        var customer = new User { Id = customerId, UserName = "alice.smith" };
        const int currentTaxYear = 2026;

        // Tell Moq: Pretend the DB says "Yes, Alice already has an active ISA"
        _mockProductRepo
            .Setup(repo => repo.HasActiveProductOfTypeInTaxYearAsync(customerId, "ISA", currentTaxYear))
            .ReturnsAsync(true);

        // Act (Execute the actual business rule)
        var result = await _isaRule.ValidateAsync(customer, currentTaxYear);

        // Assert (Verify the outcome)
        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("already has an active ISA"));
        });

        // Verify that the rule actually consulted the repository once
        _mockProductRepo.Verify(
            repo => repo.HasActiveProductOfTypeInTaxYearAsync(customerId, "ISA", currentTaxYear),
            Times.Once);
    }

    [TestCase(17, false, Description = "Age 17 must be rejected for SIPP")]
    [TestCase(18, true, Description = "Age 18 must be accepted for SIPP")]
    [TestCase(35, true, Description = "Age 35 must be accepted for SIPP")]
    public async Task SippRule_EvaluatesAgeEligibilityCorrectly(int ageYears, bool expectedSuccess)
    {
        // Arrange
        var customer = new User
        {
            Id = Guid.NewGuid(),
            UserName = "test.applicant",
            DateOfBirth = DateTime.UtcNow.AddYears(-ageYears)
        };

        // Act
        var result = await _sippRule.ValidateAsync(customer, 2026);

        // Assert
        Assert.That(result.IsValid, Is.EqualTo(expectedSuccess));
        if (!expectedSuccess)
        {
            Assert.That(result.ErrorMessage, Does.Contain("18 years old"));
        }
    }
}