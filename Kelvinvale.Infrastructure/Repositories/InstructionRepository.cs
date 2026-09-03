using Kelvinvale.Application.DTOs;
using Kelvinvale.Application.Interfaces;
using Kelvinvale.Domain.Entities;
using Kelvinvale.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Kelvinvale.Infrastructure.Repositories
{
    public class InstructionRepository : IInstructionRepository
    {
        private readonly KelvinvaleDbContext _context;

        public InstructionRepository(KelvinvaleDbContext context)
        {
            _context = context;
        }

        public async Task<Product?> GetProductWithHoldingsAndCustomerAsync(Guid productId)
        {
            // AsNoTracking ensures early validation does not pollute EF Core's ChangeTracker
            return await _context.Products
                .AsNoTracking()
                .Include(p => p.Customer)
                .Include(p => p.ProductType)
                .Include(p => p.Holdings)
                    .ThenInclude(h => h.Fund)
                .FirstOrDefaultAsync(p => p.Id == productId && p.IsActive);
        }

        public async Task<InstructionType?> GetInstructionTypeByCodeAsync(string code)
        {
            return await _context.InstructionTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(it => it.Code.ToUpper() == code.ToUpper() && it.IsActive);
        }

        public async Task<Fund?> GetFundByCodeAsync(string fundCode)
        {
            return await _context.Funds
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Code.ToUpper() == fundCode.ToUpper() && f.IsActive);
        }

        public async Task<long> GetIsaSubscriptionsSumInTaxYearAsync(Guid customerId, int taxYear)
        {
            return await _context.Instructions
                .AsNoTracking()
                .Where(i => i.Product.CustomerId == customerId
                         && i.Product.ProductType.Code == "ISA"
                         && i.Product.TaxYear == taxYear
                         && i.InstructionType.Code == "SUBSCRIPTION"
                         && i.IsActive)
                .SumAsync(i => i.AmountPence);
        }

        public async Task ExecuteInstructionAsync(
            Instruction instruction,
            string instructionType,
            Fund sourceFund,
            Fund? targetFund,
            long amountPence,
            Guid callerId)
        {
            // Resilient execution strategy for SqlServerRetryingExecutionStrategy
            var strategy = _context.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () =>
            {
              
                    // 1. Ledger entry: Always record instruction
                    await _context.Instructions.AddAsync(instruction);

                    // 2. Query source holding directly from DbSet
                    var sourceHolding = await _context.Holdings
                        .FirstOrDefaultAsync(h => h.ProductId == instruction.ProductId
                                               && h.FundId == sourceFund.Id
                                               && h.IsActive);

                    var typeUpper = instructionType.ToUpperInvariant();

                    switch (typeUpper)
                    {
                        case "SUBSCRIPTION":
                            if (sourceHolding != null)
                            {
                                // Row exists -> UPDATE
                                sourceHolding.AmountPence += amountPence;
                                sourceHolding.ModifiedOn = DateTime.UtcNow;
                                sourceHolding.ModifiedById = callerId;
                            }
                            else
                            {
                                // No row exists -> INSERT brand new holding
                                var newHolding = new Holding
                                {
                                    Id = Guid.NewGuid(),
                                    ProductId = instruction.ProductId,
                                    FundId = sourceFund.Id,
                                    AmountPence = amountPence,
                                    CreatedById = callerId,
                                    CreatedOn = DateTime.UtcNow,
                                    IsActive = true
                                };
                                await _context.Holdings.AddAsync(newHolding);
                            }
                            break;

                        case "WITHDRAWAL":
                            if (sourceHolding == null || sourceHolding.AmountPence < amountPence)
                            {
                                throw new InvalidOperationException("Insufficient balance to execute withdrawal.");
                            }

                            // Deduct from existing holding
                            sourceHolding.AmountPence -= amountPence;
                            sourceHolding.ModifiedOn = DateTime.UtcNow;
                            sourceHolding.ModifiedById = callerId;
                            break;

                        case "SWITCH":
                            if (sourceHolding == null || sourceHolding.AmountPence < amountPence)
                            {
                                throw new InvalidOperationException("Insufficient source balance to execute switch.");
                            }

                            // A. Debit source fund holding
                            sourceHolding.AmountPence -= amountPence;
                            sourceHolding.ModifiedOn = DateTime.UtcNow;
                            sourceHolding.ModifiedById = callerId;

                            // B. Credit destination fund holding (Check -> Update or Insert)
                            var targetHolding = await _context.Holdings
                                .FirstOrDefaultAsync(h => h.ProductId == instruction.ProductId
                                                       && h.FundId == targetFund!.Id
                                                       && h.IsActive);

                            if (targetHolding != null)
                            {
                                targetHolding.AmountPence += amountPence;
                                targetHolding.ModifiedOn = DateTime.UtcNow;
                                targetHolding.ModifiedById = callerId;
                            }
                            else
                            {
                                var newTargetHolding = new Holding
                                {
                                    Id = Guid.NewGuid(),
                                    ProductId = instruction.ProductId,
                                    FundId = targetFund!.Id,
                                    AmountPence = amountPence,
                                    CreatedById = callerId,
                                    CreatedOn = DateTime.UtcNow,
                                    IsActive = true
                                };
                                await _context.Holdings.AddAsync(newTargetHolding);
                            }
                            break;

                        default:
                            throw new InvalidOperationException($"Unsupported instruction type: {instructionType}");
                    }

                    // 3. Atomically commit both Instruction and Holding changes
                    await _context.SaveChangesAsync();
                
            });
        }
    }
}
