using Kelvinvale.Domain.Entities;
using Kelvinvale.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Text;

namespace Kelvinvale.Application.Interfaces
{
    public interface IProductRepository
    {
        Task<bool> HasActiveProductOfTypeInTaxYearAsync(Guid customerId, string productTypeCode, int taxYear, CancellationToken ct = default);

        // existing methods...
        Task<IEnumerable<ProductDetailDto>> GetProductsByCustomerIdAsync(Guid customerId);
        Task<ProductDetailDto?> GetProductDetailByIdAsync(Guid productId);
        Task<Product?> GetByIdAsync(Guid productId);
        Task<ProductType?> GetProductTypeByCodeAsync(string code);
        Task CreateProductAsync(Product product);
    }
}
