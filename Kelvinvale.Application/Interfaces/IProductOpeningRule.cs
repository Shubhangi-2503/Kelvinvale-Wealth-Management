using Kelvinvale.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Kelvinvale.Application.Rules.Product
{
    public interface IProductOpeningRule
    {
        string ProductTypeCode { get; }
        Task<RuleValidationResult> ValidateAsync(User customer, int taxYear);
    }
}

