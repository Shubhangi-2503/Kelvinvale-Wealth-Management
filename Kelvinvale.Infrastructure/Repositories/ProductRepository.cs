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
    public class ProductRepository : IProductRepository
    {
        private readonly KelvinvaleDbContext _context;

        public ProductRepository(KelvinvaleDbContext context)
        {
            _context = context;
        }

        public async Task<bool> HasActiveProductOfTypeInTaxYearAsync(
            Guid customerId,
            string productTypeCode,
            int taxYear,
            CancellationToken ct = default)
        {
            return await _context.Products
                .AsNoTracking()
                .AnyAsync(p => p.CustomerId == customerId
                            && p.ProductType.Code.ToUpper() == productTypeCode.ToUpper()
                            && p.TaxYear == taxYear
                            && p.IsActive, ct);
        }

        public async Task<IEnumerable<ProductDetailDto>> GetProductsByCustomerIdAsync(Guid customerId)
        {
            var products = await _context.Products
                .AsNoTracking()
                .Where(p => p.CustomerId == customerId && p.IsActive)
                .Include(p => p.ProductType)
                .Include(p => p.Holdings.Where(h => h.IsActive))
                    .ThenInclude(h => h.Fund)
                .ToListAsync();

            var productIds = products.Select(p => p.Id).ToList();

            var instructions = await _context.Instructions
                .AsNoTracking()
                .Where(i => productIds.Contains(i.ProductId) && i.IsActive)
                .Include(i => i.InstructionType)
                .Include(i => i.Fund)
                .OrderByDescending(i => i.CreatedOn)
                .ToListAsync();

            return products.Select(p => MapToDto(p, instructions)).ToList();
        }

        public async Task<ProductDetailDto?> GetProductDetailByIdAsync(Guid productId)
        {
            var product = await _context.Products
                .AsNoTracking()
                .Where(p => p.Id == productId && p.IsActive)
                .Include(p => p.ProductType)
                .Include(p => p.Holdings.Where(h => h.IsActive))
                    .ThenInclude(h => h.Fund)
                .FirstOrDefaultAsync();

            if (product == null) return null;

            var instructions = await _context.Instructions
                .AsNoTracking()
                .Where(i => i.ProductId == productId && i.IsActive)
                .Include(i => i.InstructionType)
                .Include(i => i.Fund)
                .OrderByDescending(i => i.CreatedOn)
                .ToListAsync();

            return MapToDto(product, instructions);
        }

        public async Task<Product?> GetByIdAsync(Guid productId)
        {
            return await _context.Products
                .FirstOrDefaultAsync(p => p.Id == productId && p.IsActive);
        }

        public async Task<ProductType?> GetProductTypeByCodeAsync(string code)
        {
            return await _context.ProductTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(pt => pt.Code.ToUpper() == code.ToUpper() && pt.IsActive);
        }

        public async Task CreateProductAsync(Product product)
        {
            await _context.Products.AddAsync(product);
            await _context.SaveChangesAsync();
        }

        private static ProductDetailDto MapToDto(Product p, List<Instruction> allInstructions)
        {
            var holdings = p.Holdings.Select(h => new HoldingDto(
                h.Id,
                new FundDto(h.Fund.Id, h.Fund.Code, h.Fund.Name),
                h.AmountPence
            )).ToList();

            var instructions = allInstructions
                .Where(i => i.ProductId == p.Id)
                .Select(i => new InstructionDto(
                    i.Id,
                    i.InstructionType.Code,
                    i.AmountPence,
                    i.Fund.Code,
                    i.TargetFundCode,
                    i.ClientReference,
                    i.CreatedOn
                )).ToList();

            return new ProductDetailDto(
                p.Id,
                p.CustomerId,
                p.ProductType.Code,
                p.TaxYear,
                holdings.Sum(h => h.AmountPence),
                holdings,
                instructions
            );
        }
    }
}
