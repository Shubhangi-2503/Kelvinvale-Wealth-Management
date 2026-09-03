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
        private readonly IEnumerable<IProductOpeningRule> _openingRules;

        public ProductsController(
            IProductRepository productRepo,
            ICustomerRepository customerRepo,
            IEnumerable<IProductOpeningRule> openingRules)
        {
            _productRepo = productRepo;
            _customerRepo = customerRepo;
            _openingRules = openingRules;
        }

        // GET /api/v1/customers/{customerId}/products
        [HttpGet("api/v1/customers/{customerId:guid}/products")]
        [Authorize(Roles = "Adviser,Customer")]
        [ProducesResponseType(typeof(IEnumerable<ProductDetailDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetCustomerProducts(Guid customerId)
        {
            var callerId = GetCurrentUserId();
            var callerRole = User.FindFirstValue(ClaimTypes.Role);

            if (callerRole == "Customer" && callerId != customerId)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Forbidden",
                    Detail = "Customers may only access their own products."
                });
            }

            if (callerRole == "Adviser")
            {
                var isAssigned = await _customerRepo.IsCustomerAssignedToAdviserAsync(customerId, callerId);
                if (!isAssigned)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                    {
                        Status = StatusCodes.Status403Forbidden,
                        Title = "Forbidden",
                        Detail = "Advisers cannot view products for unassigned customers."
                    });
                }
            }

            var products = await _productRepo.GetProductsByCustomerIdAsync(customerId);
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

            if (callerRole == "Customer" && callerId != product.CustomerId)
            {
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
                    return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                    {
                        Status = StatusCodes.Status403Forbidden,
                        Title = "Forbidden",
                        Detail = "Advisers cannot view products for unassigned customers."
                    });
                }
            }

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

            var responseDto = await _productRepo.GetProductDetailByIdAsync(product.Id);
            return CreatedAtAction(nameof(GetProductById), new { productId = product.Id }, responseDto);
        }

        private Guid GetCurrentUserId() =>
            Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}
