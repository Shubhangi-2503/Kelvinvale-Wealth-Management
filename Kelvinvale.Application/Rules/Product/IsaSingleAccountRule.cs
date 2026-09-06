using Kelvinvale.Domain.Entities;
using Kelvinvale.Application.Interfaces;

namespace Kelvinvale.Application.Rules.Product
{
    public class IsaSingleAccountRule : IProductOpeningRule
    {
        private readonly IProductRepository _productRepo;

        // Injects the Application interface, NOT the DbContext
        public IsaSingleAccountRule(IProductRepository productRepo)
        {
            _productRepo = productRepo;
        }

        public string ProductTypeCode => "ISA";

        public async Task<RuleValidationResult> ValidateAsync(
            User customer,
            int taxYear)
        {
            // 1. Fetch data through repository abstraction
            var hasExistingIsa = await _productRepo.HasActiveProductOfTypeInTaxYearAsync(
                customer.Id,
                ProductTypeCode,
                taxYear);

            // 2. Evaluate business logic
            if (hasExistingIsa)
            {
                return RuleValidationResult.Failure(
                    $"Customer already has an active ISA for tax year {taxYear}.");
            }

            return RuleValidationResult.Success();
        }
    }
}
