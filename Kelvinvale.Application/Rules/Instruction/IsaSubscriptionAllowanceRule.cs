using Kelvinvale.Application.Interfaces;
using Kelvinvale.Application.Rules.Product;
using Kelvinvale.Domain;

namespace Kelvinvale.Application.Rules.Instruction
{
    public class IsaSubscriptionAllowanceRule : IIsaSubscriptionAllowanceRule
    {
        private readonly IInstructionRepository _instructionRepo;

        public IsaSubscriptionAllowanceRule(IInstructionRepository instructionRepo)
        {
            _instructionRepo = instructionRepo;
        }

        public async Task<RuleValidationResult> ValidateAsync(
            Guid customerId,
            int taxYear,
            long amountPence)
        {
            var currentSubs = await _instructionRepo
            .GetIsaSubscriptionsSumInTaxYearAsync(customerId, taxYear);

            if (currentSubs + amountPence > DomainConstants.IsaAnnualAllowancePence)
            {
                var remainingPence = Math.Max(0, DomainConstants.IsaAnnualAllowancePence - currentSubs);
                return RuleValidationResult.Failure(
                    $"Instruction of £{amountPence / 100.0:F2} exceeds remaining allowance of £{remainingPence / 100.0:F2}.");
            }

            return RuleValidationResult.Success();
        }
    }
}
