using System;
using System.Collections.Generic;
using System.Text;

namespace Kelvinvale.Domain.Entities
{

    public class Product : BaseAuditableEntity
    {
        public Guid CustomerId { get; set; }
        public User Customer { get; set; } = null!;

        //Foreign key referencing ProductType table
        public Guid ProductTypeId { get; set; }
        public ProductType ProductType { get; set; } = null!;

        public int TaxYear { get; set; }
        public ICollection<Holding> Holdings { get; set; } = new List<Holding>();
    }
}

