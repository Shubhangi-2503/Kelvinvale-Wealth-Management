using Kelvinvale.Application.DTOs;
using Kelvinvale.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Kelvinvale.Application.Interfaces
{
    public interface IInstructionRepository
    {
        Task<Product?> GetProductWithHoldingsAndCustomerAsync(Guid productId);
        Task<InstructionType?> GetInstructionTypeByCodeAsync(string code);
        Task<Fund?> GetFundByCodeAsync(string fundCode);
        Task<long> GetIsaSubscriptionsSumInTaxYearAsync(Guid customerId, int taxYear);

        Task ExecuteInstructionAsync(
            Instruction instruction,
            string instructionType,
            Fund sourceFund,
            Fund? targetFund,
            long amountPence,
            Guid callerId);
    }
}
