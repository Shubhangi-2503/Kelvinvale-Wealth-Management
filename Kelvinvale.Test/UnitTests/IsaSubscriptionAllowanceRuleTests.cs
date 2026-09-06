using Kelvinvale.Application.Interfaces;
using Kelvinvale.Application.Rules.Instruction;
using Moq;

namespace Kelvinvale.Test;

[TestFixture]
public class IsaSubscriptionAllowanceRuleTests
{
    private Mock<IInstructionRepository> _mockRepo;
    private IsaSubscriptionAllowanceRule _rule;
    private readonly Guid _customerId = Guid.NewGuid();
    private const int TaxYear = 2024;

    [SetUp]
    public void SetUp()
    {
        _mockRepo = new Mock<IInstructionRepository>();
        _rule = new IsaSubscriptionAllowanceRule(_mockRepo.Object);
    }
    [Test]
    public async Task ValidateAsync_WhenTotalSubscriptionsExceedAnnualAllowance_ReturnsFailure()
    {
        // Arrange: User already deposited £15,000
        _mockRepo
            .Setup(r => r.GetIsaSubscriptionsSumInTaxYearAsync(_customerId, TaxYear))
            .ReturnsAsync(1500000);

        // Act: Attempt to deposit another £15,000
        var result = await _rule.ValidateAsync(_customerId, TaxYear, 1500000);

        // Assert
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("exceeds remaining allowance"));
    }

    [Test]
    public async Task ValidateAsync_WhenDepositEqualsRemainingAllowance_ReturnsSuccess()
    {
        //  Arrange: Customer already deposited £15,000
        _mockRepo
            .Setup(r => r.GetIsaSubscriptionsSumInTaxYearAsync(_customerId, TaxYear))
            .ReturnsAsync(1500000);

        // Act: Deposit the exact remaining £5,000 (500,000 pence)
        var result = await _rule.ValidateAsync(_customerId, TaxYear, 500000);

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.ErrorMessage, Is.Null.Or.Empty);
    }
}

