using Kelvinvale.Application.DTOs;
using Kelvinvale.Application.Exceptions;
using Kelvinvale.Application.Interfaces;
using Kelvinvale.Application.Rules.Product;
using Kelvinvale.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.Extensions.Logging;


[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
[Authorize]
public class CustomersController : ControllerBase
{
    private readonly ICustomerRepository _customerRepo;
    private readonly ILogger<CustomersController> _logger;
    private readonly IEnumerable<IProductOpeningRule> _openingRules; // Injected rules engine

    public CustomersController(ILogger<CustomersController> logger,
        ICustomerRepository customerRepo,
        IEnumerable<IProductOpeningRule> openingRules)
    {
        _logger = logger;
        _customerRepo = customerRepo;
        _openingRules = openingRules;
    }

    [HttpGet]
    [Authorize(Roles = "Adviser")]
    [ProducesResponseType(typeof(IEnumerable<CustomerDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyAssignedCustomers()
    {
        var adviserId = GetCurrentUserId();
        var customers = await _customerRepo.GetAssignedCustomersWithDetailsAsync(adviserId);
        return Ok(customers);
        
    }

    [HttpGet("{customerId:guid}")]
    [Authorize(Roles = "Adviser,Customer")]
    [ProducesResponseType(typeof(CustomerDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCustomerById(Guid customerId)
    {
        var callerId = GetCurrentUserId();
        var callerRole = User.FindFirstValue(ClaimTypes.Role);

        if (callerRole == "Customer" && callerId != customerId)
        {
            _logger.LogWarning("Unauthorized access attempt to customer record."); 
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Forbidden",
                Detail = "Customers are only authorized to access their own records."
            });
        }

        if (callerRole == "Adviser")
        {
            var isAssigned = await _customerRepo.IsCustomerAssignedToAdviserAsync(customerId, callerId);
            if (!isAssigned)
            {
                _logger.LogWarning("Unauthorized access attempt to customer record."); 
                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Forbidden",
                    Detail = "Advisers cannot view records for non-assigned customers."
                });
            }
        }

        var customerDetails = await _customerRepo.GetCustomerDetailByIdAsync(customerId);
        if (customerDetails == null)
        {
            _logger.LogWarning("Requested customer record not found."); 
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Not Found",
                Detail = $"Customer record with ID '{customerId}' was not found."
            });
        }

        return Ok(customerDetails);
    }
    [HttpPost]
    [Authorize(Roles = "Adviser")]
    [ProducesResponseType(typeof(CustomerDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateCustomer([FromBody] CreateCustomerRequest request)
    {
        var adviserId = GetCurrentUserId();

        var customerRoleId = await _customerRepo.GetCustomerRoleIdAsync();
        if (customerRoleId == null)
        {
            _logger.LogError("Customer role reference is not configured.");
            return StatusCode(StatusCodes.Status500InternalServerError, "Customer role reference is not configured.");
        }

        var customerId = Guid.NewGuid();

        var customer = new User
        {
            Id = customerId,
            UserName = request.UserName,
            Email = request.Email,
            DateOfBirth = request.DateOfBirth,
            RoleId = customerRoleId.Value,
            CreatedById = adviserId,
            CreatedOn = DateTime.UtcNow,
            IsActive = true
        };

        var productsToCreate = new List<Product>();

        if (request.Products != null && request.Products.Count > 0)
        {
            // 1. In-batch Duplicate Guard: Prevent submitting two ISAs in the same request payload
            var duplicateIsaInBatch = request.Products
                .Where(p => p.ProductTypeCode.Equals("ISA", StringComparison.OrdinalIgnoreCase))
                .GroupBy(p => p.TaxYear)
                .Any(g => g.Count() > 1);

            if (duplicateIsaInBatch)
            {
                _logger.LogWarning("Duplicate ISA found in the request payload.");
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Duplicate Product in Request",
                    Detail = "Cannot open multiple ISAs for the same tax year."
                });
            }

            // 2. Evaluate all requested products against the Open-Closed Rule Engine
            foreach (var prodReq in request.Products)
            {
                var productType = await _customerRepo.GetProductTypeByCodeAsync(prodReq.ProductTypeCode);
                if (productType == null)
                {
                    _logger.LogWarning("Invalid product type requested.");
                    return BadRequest(new ProblemDetails
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Title = "Invalid Product Type",
                        Detail = $"Product type '{prodReq.ProductTypeCode}' does not exist."
                    });
                }

                // Run every rule registered for this specific product type
                var matchingRules = _openingRules.Where(r =>
                    r.ProductTypeCode.Equals(productType.Code, StringComparison.OrdinalIgnoreCase));

                foreach (var rule in matchingRules)
                {
                    var result = await rule.ValidateAsync(customer, prodReq.TaxYear);
                    if (!result.IsValid)
                    {
                        _logger.LogWarning("Product opening rule validation failed.");
                        return BadRequest(new ProblemDetails
                        {
                            Status = StatusCodes.Status400BadRequest,
                            Title = "Product Opening Rule Failed",
                            Detail = result.ErrorMessage
                        });
                    }
                }

                productsToCreate.Add(new Product
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customerId,
                    ProductTypeId = productType.Id,
                    TaxYear = prodReq.TaxYear,
                    CreatedById = adviserId,
                    CreatedOn = DateTime.UtcNow,
                    IsActive = true
                });
            }
        }

        try
        {
            await _customerRepo.CreateCustomerWithAdviserAndProductsAsync(customer, adviserId, productsToCreate);
        }
        catch (DuplicateEmailException ex)
        {
            _logger.LogWarning("Duplicate email found during customer creation.");
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Duplicate Email",
                Detail = ex.Message
            });
        }

        var responseDto = await _customerRepo.GetCustomerDetailByIdAsync(customerId);
        _logger.LogInformation("Customer {CustomerId} created by adviser {AdviserId} with {ProductCount} products", customerId, adviserId, productsToCreate.Count);

        return CreatedAtAction(nameof(GetCustomerById), new { customerId = customer.Id }, responseDto);
    }

    [HttpPut("{customerId:guid}")]
    [Authorize(Roles = "Customer")]
    [ProducesResponseType(typeof(CustomerDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCustomerProfile(Guid customerId, [FromBody] UpdateCustomerProfileRequest request)
    {
        var callerId = GetCurrentUserId();

        if (callerId != customerId)
        {
            _logger.LogWarning("Unauthorized access attempt to customer record.");
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Forbidden",
                Detail = "You cannot update profile information for other accounts."
            });
        }

        var customer = await _customerRepo.GetByIdAsync(customerId);
        if (customer == null)
        {
            _logger.LogWarning("Requested customer record not found.");
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Not Found",
                Detail = $"Customer record with ID '{customerId}' was not found."
            });
        }

        customer.Email = request.Email;
        customer.DateOfBirth = request.DateOfBirth;
        customer.ModifiedById = callerId;
        customer.ModifiedOn = DateTime.UtcNow;

        try
        {
            await _customerRepo.UpdateCustomerAsync(customer);
        }
        catch (DuplicateEmailException ex)
        {
            _logger.LogWarning("Duplicate email found during customer update.");
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Duplicate Email",
                Detail = ex.Message
            });
        }

        _logger.LogInformation("Profile updated for customer {CustomerId} by user {CallerId}", customerId, callerId);
        var updatedDetails = await _customerRepo.GetCustomerDetailByIdAsync(customerId);
        return Ok(updatedDetails);
        
    }

    private Guid GetCurrentUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}