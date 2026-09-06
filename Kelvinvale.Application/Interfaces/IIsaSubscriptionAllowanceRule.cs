using Kelvinvale.Application.Rules.Product;

namespace Kelvinvale.Application.Interfaces
{
    public interface IIsaSubscriptionAllowanceRule
    {
        Task<RuleValidationResult> ValidateAsync(
            Guid customerId,
            int taxYear,
            long amountPence);
    }
}
