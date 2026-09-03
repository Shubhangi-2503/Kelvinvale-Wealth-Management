using System;
using System.Collections.Generic;
using System.Text;

namespace Kelvinvale.Application.Rules.Product
{

    public class RuleValidationResult
    {
        public bool IsValid { get; }
        public string? ErrorMessage { get; }
        public RuleValidationResult(bool isValid, string? errorMessage = null)
        {
            IsValid = isValid;
            ErrorMessage = errorMessage;
        }
        public static RuleValidationResult Success() => new(true);
        public static RuleValidationResult Failure(string message) => new(false, message);
    }
}
