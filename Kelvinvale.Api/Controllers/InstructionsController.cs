using Kelvinvale.Application.DTOs;
using Kelvinvale.Application.Interfaces;
using Kelvinvale.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Kelvinvale.Domain;

namespace Kelvinvale.Api.Controllers
{
        [ApiController]
        [Route("api/v1/products/{productId:guid}/instructions")]
        [Produces("application/json")]
        [Authorize]
        public class InstructionsController : ControllerBase
        {
        private readonly IInstructionRepository _instructionRepo;
        private readonly ILogger<InstructionsController> _logger;
        private readonly ICustomerRepository _customerRepo;
        private readonly IIsaSubscriptionAllowanceRule _isaAllowanceRule;
        int SippMinimumPensionAge = DomainConstants.SippMinimumPensionAge;


        public InstructionsController(
                IInstructionRepository instructionRepo,
                ILogger<InstructionsController> logger,
                ICustomerRepository customerRepo,
                IIsaSubscriptionAllowanceRule isaAllowanceRule)
            {
                _instructionRepo = instructionRepo;
                _customerRepo = customerRepo;
                _logger = logger;
                _isaAllowanceRule = isaAllowanceRule;
            }

            [HttpPost]
            [ProducesResponseType(typeof(InstructionResponseDto), StatusCodes.Status201Created)]
            [ProducesResponseType(StatusCodes.Status400BadRequest)]
            [ProducesResponseType(StatusCodes.Status403Forbidden)]
            [ProducesResponseType(StatusCodes.Status404NotFound)]
            public async Task<IActionResult> CreateInstruction(
                Guid productId,
                [FromBody] CreateInstructionRequest request)
            {
                // 1. Amount Verification (> 0 pence)
                if (request.AmountPence <= 0)
                {
                    _logger.LogWarning("Invalid instruction amount provided for product {ProductId}", productId);
                    return BadRequest(new ProblemDetails
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Title = "Invalid Amount",
                        Detail = "Instruction amount must be strictly greater than zero pence."
                    });
                }

                // 2. Reject Advisers explicitly
                var callerRole = User.FindFirst(ClaimTypes.Role)?.Value
                                 ?? Request.Headers["X-Caller-Role"].FirstOrDefault();

                _logger.LogInformation("Caller role: {CallerRole}", callerRole);

                if (string.Equals(callerRole, "Adviser", StringComparison.OrdinalIgnoreCase))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                    {
                        Status = StatusCodes.Status403Forbidden,
                        Title = "Forbidden",
                        Detail = "You are not an authorised person to do it. Advisers cannot place instructions."
                    });
                }

                var callerId = GetCurrentUserId();

                // 3. Check customer existence using your existing CustomerRepository method
                var customer = await _customerRepo.GetByIdAsync(callerId);
                if (customer == null)
                {
                    _logger.LogWarning("Customer not found for ID: {CallerId}", callerId);
                    return NotFound(new ProblemDetails
                    {
                        Status = StatusCodes.Status404NotFound,
                        Title = "Customer Not Found",
                        Detail = $"Customer with ID '{callerId}' does not exist or is inactive."
                    });
                }

                // 4. Fetch Product with Holdings and Type from DB
                var product = await _instructionRepo.GetProductWithHoldingsAndCustomerAsync(productId);
                if (product == null)
                {
                    _logger.LogWarning("Product not found for ID: {ProductId}", productId);
                    return NotFound(new ProblemDetails
                    {
                        Status = StatusCodes.Status404NotFound,
                        Title = "Product Not Found",
                        Detail = $"Product account '{productId}' was not found."
                    });
                }

                // 5. Enforce Ownership Isolation (Customers can only touch what they own)
                if (product.CustomerId != callerId)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                    {
                        Status = StatusCodes.Status403Forbidden,
                        Title = "Forbidden",
                        Detail = "You cannot place instructions against a product you do not own."
                    });
                }

                // 6. Validate Instruction Type
                var instructionType = await _instructionRepo.GetInstructionTypeByCodeAsync(request.Type);
                if (instructionType == null)
                {
                    _logger.LogWarning("Invalid instruction type provided: {InstructionType}", request.Type);
                    return BadRequest(new ProblemDetails
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Title = "Invalid Instruction Type",
                        Detail = "Supported instruction types are 'Subscription', 'Withdrawal', or 'Switch'."
                    });
                }

                // 7. Validate Source Fund
                var sourceFund = await _instructionRepo.GetFundByCodeAsync(request.FundCode);
                if (sourceFund == null)
                {
                    return BadRequest(new ProblemDetails
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Title = "Invalid Fund",
                        Detail = $"Source fund '{request.FundCode}' does not exist."
                    });
                }

                var sourceHolding = product.Holdings.FirstOrDefault(h => h.FundId == sourceFund.Id && h.IsActive);
                Fund? targetFund = null;

                // 8. Regulatory & Balance Checks
                switch (request.Type.ToUpperInvariant())
                {
                    case "SUBSCRIPTION":
                    if (product.ProductType.Code.Equals("ISA", StringComparison.OrdinalIgnoreCase))
                    {
                        var allowanceResult = await _isaAllowanceRule.ValidateAsync(
                            product.CustomerId,
                            product.TaxYear,
                            request.AmountPence);

                        if (!allowanceResult.IsValid)
                        {
                            _logger.LogWarning("ISA subscription amount exceeds annual allowance for product {ProductId}", productId);
                            return BadRequest(new ProblemDetails
                            {
                                Status = StatusCodes.Status400BadRequest,
                                Title = "ISA Allowance Exceeded",
                                Detail = allowanceResult.ErrorMessage
                            });
                        }
                    }
                    break;

                case "WITHDRAWAL":
                        if (product.ProductType.Code.Equals("SIPP", StringComparison.OrdinalIgnoreCase))
                        {
                            var dob = product.Customer.DateOfBirth;
                            if (!dob.HasValue || dob.Value.AddYears(SippMinimumPensionAge) > DateTime.UtcNow)
                            {
                                _logger.LogWarning("SIPP withdrawal attempted before minimum retirement age for product {ProductId}", productId);
                                return BadRequest(new ProblemDetails
                                {
                                    Status = StatusCodes.Status400BadRequest,
                                    Title = "SIPP Withdrawal Prohibited",
                                    Detail = $"Withdrawals from a SIPP are prohibited before reaching the minimum retirement age of {SippMinimumPensionAge}."
                                });
                            }
                        }

                        if (sourceHolding == null || sourceHolding.AmountPence < request.AmountPence)
                        {
                            var available = sourceHolding?.AmountPence ?? 0;
                            _logger.LogWarning("Insufficient balance for withdrawal for product {ProductId}", productId);
                            return BadRequest(new ProblemDetails
                            {
                                Status = StatusCodes.Status400BadRequest,
                                Title = "Insufficient Balance",
                                Detail = $"Insufficient funds in '{sourceFund.Code}'. Available: £{available / 100.0:F2}, requested: £{request.AmountPence / 100.0:F2}."
                            });
                        }
                        break;

                    case "SWITCH":
                        if (string.IsNullOrWhiteSpace(request.TargetFundCode))
                        {
                            return BadRequest(new ProblemDetails
                            {
                                Status = StatusCodes.Status400BadRequest,
                                Title = "Target Fund Required",
                                Detail = "TargetFundCode must be specified for a Switch instruction."
                            });
                        }

                        if (request.FundCode.Equals(request.TargetFundCode, StringComparison.OrdinalIgnoreCase))
                        {
                            return BadRequest(new ProblemDetails
                            {
                                Status = StatusCodes.Status400BadRequest,
                                Title = "Invalid Switch Target",
                                Detail = "Source and destination funds cannot be identical."
                            });
                        }

                        targetFund = await _instructionRepo.GetFundByCodeAsync(request.TargetFundCode);
                        if (targetFund == null)
                        {
                            return BadRequest(new ProblemDetails
                            {
                                Status = StatusCodes.Status400BadRequest,
                                Title = "Invalid Target Fund",
                                Detail = $"Target destination fund '{request.TargetFundCode}' does not exist."
                            });
                        }

                        if (sourceHolding == null || sourceHolding.AmountPence < request.AmountPence)
                        {
                            var available = sourceHolding?.AmountPence ?? 0;
                            return BadRequest(new ProblemDetails
                            {
                                Status = StatusCodes.Status400BadRequest,
                                Title = "Insufficient Balance",
                                Detail = $"Insufficient funds in '{sourceFund.Code}' to switch. Available: £{available / 100.0:F2}, requested: £{request.AmountPence / 100.0:F2}."
                            });
                        }
                        break;
                }

                // 9. Create instruction instance (Foreign keys only — NO circular Product = product)
                var instruction = new Instruction
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    InstructionTypeId = instructionType.Id,
                    FundId = sourceFund.Id,
                    TargetFundCode = request.TargetFundCode,
                    AmountPence = request.AmountPence,
                    ClientReference = request.ClientReference ?? Guid.NewGuid().ToString("N")[..12],
                    CreatedById = callerId,
                    CreatedOn = DateTime.UtcNow,
                    IsActive = true
                };

                // 10. Execute Transaction
                await _instructionRepo.ExecuteInstructionAsync(
                    instruction,
                    request.Type,
                    sourceFund,
                    targetFund,
                    request.AmountPence,
                    callerId);

                var responseDto = new InstructionResponseDto(
                    instruction.Id,
                    instruction.ProductId,
                    instructionType.Code,
                    instruction.AmountPence,
                    sourceFund.Code,
                    instruction.TargetFundCode,
                    instruction.ClientReference,
                    instruction.CreatedOn
                );

                return StatusCode(StatusCodes.Status201Created, responseDto);
            }

            private Guid GetCurrentUserId()
            {
                var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier)
                                 ?? Request.Headers["X-Caller-Id"].FirstOrDefault();

                if (Guid.TryParse(claimValue, out var guid))
                {
                    return guid;
                }

                return Guid.Empty;
            }
        }
    }
