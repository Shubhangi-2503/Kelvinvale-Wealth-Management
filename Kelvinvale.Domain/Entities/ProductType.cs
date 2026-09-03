using System;
using System.Collections.Generic;
using System.Text;

namespace Kelvinvale.Domain.Entities
{
    public class ProductType : BaseAuditableEntity
    {
        public string Code { get; set; } = string.Empty; // e.g., "ISA", "GIA", "SIPP"
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}
