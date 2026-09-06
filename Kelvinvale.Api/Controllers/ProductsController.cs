using Kelvinvale.Application.DTOs;
using Kelvinvale.Application.Interfaces;
using Kelvinvale.Application.Rules.Product;
using Kelvinvale.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Kelvinvale.Api.Controllers
{
    [ApiController]
    [Produces("application/json")]
    [Authorize]
    public class ProductsController : ControllerBase
    {
        private readonly IProductRepository _productRepo;
        private readonly ICustomerRepository _customerRepo;
        private readonly ILogger<ProductsController> _logger;
        private readonly IEnumerable<IProductOpeningRule> _openingRules;

        public ProductsController(
            IProductRepository productRepo,
            ICustomerRepository customerRepo,
            ILogger<ProductsController> logger,
            IEnumerable<IProductOpeningRule> openingRules)
        {
            _productRepo = productRepo;
            _customerRepo = customerRepo;
            _logger = logger;   
            _openingRules = openingRules;
        }

        // GET /api/v1/customers/{customerId}/products
        [HttpGet("api/v1/customers/{customerId:guid}/products")]
        [Authorize(Roles = "Adviser,Customer")]
        [ProducesResponseType(typeof(IEnumerable<ProductDetailDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetCustomerProducts(Guid customerId)
        {
            _logger.LogInformation("Fetching products for customer: {CustomerId}", customerId);
            var callerId = GetCurrentUserId();
            var callerRole = User.FindFirstValue(ClaimTypes.Role);

            if (callerRole == "Customer" && callerId != customerId)
            {
                _logger.LogWarning("Unauthorized access attempt by user: {CallerId}", callerId);    
                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Forbidden",
                    Detail = "Customers may only access their own products."
                });
            }

            if (callerRole == "Adviser")
            {
                _logger.LogInformation("Caller role: {CallerRole}", callerRole);
                var isAssigned = await _customerRepo.IsCustomerAssignedToAdviserAsync(customerId, callerId);
                if (!isAssigned)
                {
                    _logger.LogWarning("Adviser {CallerId} attempted to view products for unassigned customer {CustomerId}", callerId, customerId);
                    return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                    {
                        Status = StatusCodes.Status403Forbidden,
                        Title = "Forbidden",
                        Detail = "Advisers cannot view products for unassigned customers."
                    });
                }
            }

            var products = await _productRepo.GetProductsByCustomerIdAsync(customerId);
            _logger.LogInformation("Retrieved products for customer: {CustomerId}", customerId);
            return Ok(products);
        }

        // GET /api/v1/products/{productId}
        [HttpGet("api/v1/products/{productId:guid}")]
        [Authorize(Roles = "Adviser,Customer")]
        [ProducesResponseType(typeof(ProductDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetProductById(Guid productId)
        {
            var product = await _productRepo.GetProductDetailByIdAsync(productId);
            if (product == null)
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Product Not Found",
                    Detail = $"No product found for ID '{productId}'."
                });
            }

            var callerId = GetCurrentUserId();
            var callerRole = User.FindFirstValue(ClaimTypes.Role);

            _logger.LogInformation("Fetching product details for ID: {ProductId}", productId);

            if (callerRole == "Customer" && callerId != product.CustomerId)
            {
                _logger.LogWarning("Unauthorized access attempt by user: {CallerId}", callerId);
                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Forbidden",
                    Detail = "Customers may only access their own product accounts."
                });
            }

            if (callerRole == "Adviser")
            {
                var isAssigned = await _customerRepo.IsCustomerAssignedToAdviserAsync(product.CustomerId, callerId);
                if (!isAssigned)
                {
                    _logger.LogWarning("Adviser {CallerId} attempted to view product {ProductId} for unassigned customer {CustomerId}", callerId, productId, product.CustomerId);
                    return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                    {
                        Status = StatusCodes.Status403Forbidden,
                        Title = "Forbidden",
                        Detail = "Advisers cannot view products for unassigned customers."
                    });
                }
            }

            _logger.LogInformation("Returning product details for ID: {ProductId}", productId);
            return Ok(product);
        }

        // POST /api/v1/customers/{customerId}/products (Adviser opens account)
        [HttpPost("api/v1/customers/{customerId:guid}/products")]
        [Authorize(Roles = "Adviser")]
        [ProducesResponseType(typeof(ProductDetailDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> OpenProduct(Guid customerId, [FromBody] OpenProductRequest request)
        {
            var adviserId = GetCurrentUserId();

            var isAssigned = await _customerRepo.IsCustomerAssignedToAdviserAsync(customerId, adviserId);
            if (!isAssigned)
            {
                _logger.LogWarning("Adviser {AdviserId} attempted to open product for unassigned customer {CustomerId}", adviserId, customerId);
                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Forbidden",
                    Detail = "You are not authorized to open accounts for this customer."
                });
            }

            var customer = await _customerRepo.GetByIdAsync(customerId);
            if (customer == null)
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Customer Not Found",
                    Detail = $"Customer '{customerId}' not found."
                });
            }

            var productType = await _productRepo.GetProductTypeByCodeAsync(request.ProductTypeCode);
            if (productType == null)
            {
                _logger.LogWarning("Invalid product type requested: {ProductTypeCode}", request.ProductTypeCode);
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Invalid Product Type",
                    Detail = $"Product type '{request.ProductTypeCode}' does not exist."
                });
            }

            // Evaluate all rules matching this product type
            var applicableRules = _openingRules.Where(r =>
                r.ProductTypeCode.Equals(productType.Code, StringComparison.OrdinalIgnoreCase));

            foreach (var rule in applicableRules)
            {
                var result = await rule.ValidateAsync(customer, request.TaxYear);
                if (!result.IsValid)
                {
                    _logger.LogWarning("Product opening rule failed for customer {CustomerId}: {ErrorMessage}", customerId, result.ErrorMessage);
                    return BadRequest(new ProblemDetails
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Title = "Product Opening Rule Failed",
                        Detail = result.ErrorMessage
                    });
                }
            }

            var product = new Product
            {
                Id = Guid.NewGuid(),
                CustomerId = customerId,
                ProductTypeId = productType.Id,
                TaxYear = request.TaxYear,
                CreatedById = adviserId,
                CreatedOn = DateTime.UtcNow,
                IsActive = true
            };

            await _productRepo.CreateProductAsync(product);
            _logger.LogInformation("Product created successfully for customer: {CustomerId}", customerId);  
            var responseDto = await _productRepo.GetProductDetailByIdAsync(product.Id);
            return CreatedAtAction(nameof(GetProductById), new { productId = product.Id }, responseDto);
        }

        private Guid GetCurrentUserId() =>
            Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}
