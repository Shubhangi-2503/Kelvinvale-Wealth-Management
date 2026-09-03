using System;
using System.Collections.Generic;
using System.Text;

namespace Kelvinvale.Domain.Entities
{
    public class Fund: BaseAuditableEntity
    {
        public string Code { get; set; } = string.Empty; 
        public string Name { get; set; } = string.Empty;
    }
}
