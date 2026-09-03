using System;
using System.Collections.Generic;
using System.Text;

namespace Kelvinvale.Domain.Entities
{
    public class BaseAuditableEntity
    {
        public Guid Id { get; set; }
         public bool IsActive { get; set; } = true;

        //Creation Metadata
        public Guid CreatedById { get; set; }
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

        // Modification Metadata
        public Guid? ModifiedById { get; set; }
        public DateTime? ModifiedOn { get; set; }

        //Soft Delete  Metadata
        public Guid? DisabledById { get; set; }
        public DateTime? DisabledOn { get; set; }
    }
}
