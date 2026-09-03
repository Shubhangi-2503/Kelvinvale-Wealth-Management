using System;
using System.Collections.Generic;
using System.Text;

namespace Kelvinvale.Application.DTOs
{
        public record OpenProductRequest(
       string ProductTypeCode, // "ISA", "GIA", or "SIPP"
       int TaxYear
       );

        public record ProductDetailDto(
            Guid Id,
            Guid CustomerId,
            string ProductType,
            int TaxYear,
            long TotalBalancePence,
            List<HoldingDto> Holdings,
            List<InstructionDto> RecentInstructions
        );
        public record FundDto(
             Guid Id,
             string Code,
             string Name
        );

        public record HoldingDto(
            Guid Id,
            FundDto Fund,
            long AmountPence
        );

        public record InstructionDto(
            Guid Id,
            string InstructionType,
            long AmountPence,
            string FundCode,
            string? TargetFundCode,
            string ClientReference,
            DateTime CreatedOn
        );

        public record ProductSummaryDto(
            Guid Id,
            string ProductType,
            int TaxYear,
            long TotalBalancePence,
            List<HoldingDto> Holdings,
            List<InstructionDto> RecentInstructions
        );

        public record CustomerDetailDto(
            Guid Id,
            string UserName,
            string Email,
            DateTime? DateOfBirth,
            List<ProductSummaryDto> Products
        );

        public record CreateCustomerRequest(
            string UserName,
            string Email,       
            DateTime? DateOfBirth,
            List<InitialProductRequest>? Products = null
        );

        public record InitialProductRequest(
            string ProductTypeCode, // "ISA", "GIA", or "SIPP"
              int TaxYear
            );
        public record UpdateCustomerProfileRequest(
            string Email,
            DateTime? DateOfBirth
        );

        public record CreateInstructionRequest(
                string Type, // "Subscription", "Withdrawal", or "Switch"
                long AmountPence,
                string FundCode,
                string? TargetFundCode = null,
                string? ClientReference = null
        );

        public record InstructionResponseDto(
            Guid Id,
            Guid ProductId,
            string InstructionType,
            long AmountPence,
            string FundCode,
            string? TargetFundCode,
            string? ClientReference,
            DateTime CreatedOn
        );

}
