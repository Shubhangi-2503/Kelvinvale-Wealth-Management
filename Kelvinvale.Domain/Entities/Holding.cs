    using System;
using System.Collections.Generic;
using System.Text;

namespace Kelvinvale.Domain.Entities
{
    public class Holding : BaseAuditableEntity
    {
        public Guid ProductId { get; set; }
        public Product Product { get; set; } = null!;
        public Guid FundId { get; set; }
        public Fund Fund { get; set; } = null!;
        public long AmountPence { get; set; } 
    }
}
