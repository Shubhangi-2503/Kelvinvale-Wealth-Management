using Kelvinvale.Application.DTOs;
using Kelvinvale.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Kelvinvale.Application.Interfaces
{
    public interface ICustomerRepository
    {
        Task<IEnumerable<CustomerDetailDto>> GetAssignedCustomersWithDetailsAsync(Guid adviserId);
        Task<CustomerDetailDto?> GetCustomerDetailByIdAsync(Guid customerId);
        Task<User?> GetByIdAsync(Guid customerId);
        Task<Guid?> GetCustomerRoleIdAsync();
        Task<ProductType?> GetProductTypeByCodeAsync(string code);
        Task<bool> IsCustomerAssignedToAdviserAsync(Guid customerId, Guid adviserId);
        Task CreateCustomerWithAdviserAndProductsAsync(User customer, Guid adviserId, List<Product>? products);
        Task UpdateCustomerAsync(User customer);
    }
}
