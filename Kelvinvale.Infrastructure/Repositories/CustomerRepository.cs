using Kelvinvale.Application.DTOs;
using Kelvinvale.Application.Exceptions;
using Kelvinvale.Application.Interfaces;
using Kelvinvale.Domain.Entities;
using Kelvinvale.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kelvinvale.Infrastructure.Repositories;

public class CustomerRepository : ICustomerRepository
{
    private readonly KelvinvaleDbContext _context;
    private readonly ILogger<CustomerRepository> _logger;

    public CustomerRepository(KelvinvaleDbContext context, ILogger<CustomerRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<CustomerDetailDto>> GetAssignedCustomersWithDetailsAsync(Guid adviserId)
    {
        // 1. Fetch all assigned active customer IDs in one query
        var customerIds = await _context.CustomerAdvisors
            .AsNoTracking()
            .Where(ca => ca.AdviserId == adviserId && ca.IsActive)
            .Select(ca => ca.CustomerId)
            .ToListAsync();

        if (customerIds.Count == 0)
        {
            _logger.LogWarning("No assigned customers found.");
            return Enumerable.Empty<CustomerDetailDto>();
        }

        // 2. Fetch customers, products, holdings, and instructions in a single batch
        var users = await _context.Users
            .AsNoTracking()
            .Where(u => customerIds.Contains(u.Id) && u.IsActive)
            .ToListAsync();

        var products = await _context.Products
            .AsNoTracking()
            .Where(p => customerIds.Contains(p.CustomerId) && p.IsActive)
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

        _logger.LogInformation("Retrieved {Count} assigned customers for adviser {AdviserId}", users.Count, adviserId);
        // 3. In-memory assembly (0 extra database queries)
        return users.Select(user => MapToDto(user, products, instructions)).ToList();
    }

    public async Task<CustomerDetailDto?> GetCustomerDetailByIdAsync(Guid customerId)
    {
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == customerId && u.IsActive);

        if (user == null) return null;

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

        return MapToDto(user, products, instructions);
    }

    public async Task<User?> GetByIdAsync(Guid customerId)
    {
        _logger.LogInformation("Fetching customer with ID {CustomerId}", customerId);
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Id == customerId && u.IsActive);
    }

    public async Task<Guid?> GetCustomerRoleIdAsync()
    {
        var role = await _context.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Name == "Customer");
        _logger.LogInformation("Fetched Customer role ID: {RoleId}", role?.Id);
        return role?.Id;
    }

    public async Task<bool> IsCustomerAssignedToAdviserAsync(Guid customerId, Guid adviserId)
    {
        _logger.LogInformation("Checking if customer {CustomerId} is assigned to adviser {AdviserId}", customerId, adviserId);
        return await _context.CustomerAdvisors
            .AsNoTracking()
            .AnyAsync(ca => ca.CustomerId == customerId && ca.AdviserId == adviserId && ca.IsActive);
    }


    public async Task UpdateCustomerAsync(User customer)
    {
        _logger.LogInformation("Database updated entity for customer {CustomerId}", customer.Id);
        _context.Users.Update(customer);
        await _context.SaveChangesAsync();
    }

    public async Task<ProductType?> GetProductTypeByCodeAsync(string code)
    {
        _logger.LogInformation("Fetching product type with code {Code}", code);
        return await _context.ProductTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(pt => pt.Code.ToUpper() == code.ToUpper() && pt.IsActive);
    }

    public async Task CreateCustomerWithAdviserAndProductsAsync(
        User customer,
        Guid adviserId,
        List<Product>? products)
    {
        var relationship = new CustomerAdvisor
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            AdviserId = adviserId,
            CreatedById = adviserId,
            CreatedOn = DateTime.UtcNow,
            IsActive = true
        };

        var emailAlreadyExists = await _context.Users
        .AnyAsync(u => u.Email.ToLower() == customer.Email.ToLower() && u.IsActive);
       

        if (emailAlreadyExists)
        {
            _logger.LogWarning("Attempted to create a customer with duplicate email: {Email}", customer.Email);
            throw new DuplicateEmailException(customer.Email);
        }
        await _context.Users.AddAsync(customer);
        await _context.CustomerAdvisors.AddAsync(relationship);

        if (products != null && products.Count > 0)
        {
            await _context.Products.AddRangeAsync(products);
        }

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx &&
                                  (sqlEx.Number == 2601 || sqlEx.Number == 2627))
        {
            throw new DuplicateEmailException(customer.Email);
        }
    }

    // Helper method for clean DTO mapping
    private static CustomerDetailDto MapToDto(
        User user,
        List<Product> allProducts,
        List<Instruction> allInstructions)

    {
        var userProducts = allProducts.Where(p => p.CustomerId == user.Id).ToList();

        var productDtos = userProducts.Select(p =>
        {
            var productHoldings = p.Holdings
                .Select(h => new HoldingDto(
                    h.Id,
                    new FundDto(h.Fund.Id, h.Fund.Code, h.Fund.Name),
                    h.AmountPence
                ))
                .ToList();

            var productInstructions = allInstructions
                .Where(i => i.ProductId == p.Id)
                .Select(i => new InstructionDto(
                    i.Id,
                    i.InstructionType.Code,
                    i.AmountPence,
                    i.Fund.Code,
                    i.TargetFundCode,
                    i.ClientReference,
                    i.CreatedOn
                ))
                .ToList();

            return new ProductSummaryDto(
                p.Id,
                p.ProductType.Code,
                p.TaxYear,
                productHoldings.Sum(h => h.AmountPence),
                productHoldings,
                productInstructions
            );
        }).ToList();

        return new CustomerDetailDto(
            user.Id,
            user.UserName,
            user.Email,
            user.DateOfBirth,
            productDtos
        );
    }
}