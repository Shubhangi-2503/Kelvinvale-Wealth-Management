using Kelvinvale.Domain.Entities;
using Kelvinvale.Domain;

namespace Kelvinvale.Application.Rules.Product
{
    public class SippAgeEligibilityRule : IProductOpeningRule
    {
        public string ProductTypeCode => "SIPP";
        int SippMinimumOpeningAge = DomainConstants.SippMinimumOpeningAge;  

        public Task<RuleValidationResult> ValidateAsync(User customer, int taxYear)
        {
            if (!customer.DateOfBirth.HasValue)
            {
                return Task.FromResult(RuleValidationResult.Failure("Date of birth is required to open a SIPP."));
            }

            var age = DateTime.UtcNow.Year - customer.DateOfBirth.Value.Year;
            if (customer.DateOfBirth.Value.Date > DateTime.UtcNow.AddYears(-age)) age--;

            if (age < SippMinimumOpeningAge)
            {
                return Task.FromResult(RuleValidationResult.Failure("Customer must be at least 18 years old to open a SIPP."));
            }

            return Task.FromResult(RuleValidationResult.Success());
        }
    }
}
