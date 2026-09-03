using System;
using System.Collections.Generic;
using System.Text;

namespace Kelvinvale.Domain.Entities
{
    public class CustomerAdvisor : BaseAuditableEntity
    {
        public Guid CustomerId { get; set; }
        public User Customer { get; set; } = null!;
        public Guid AdviserId { get; set; }
        public User Adviser { get; set; } = null!;
    }
}
