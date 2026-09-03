using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Text;

namespace Kelvinvale.Domain.Entities
{
    public class Instruction : BaseAuditableEntity
    {
        public Guid ProductId { get; set; }
        public Product Product { get; set; } = null!;

        // Foreign key referencing InstructionType table
        public Guid InstructionTypeId { get; set; }
        public InstructionType InstructionType { get; set; } = null!;

        public long AmountPence { get; set; }
        public Guid FundId { get; set; }
        public Fund Fund { get; set; } = null!; 

        public string? TargetFundCode { get; set; }
        public string ClientReference { get; set; } = string.Empty;
    }
}
