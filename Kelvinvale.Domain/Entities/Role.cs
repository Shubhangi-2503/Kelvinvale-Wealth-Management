using System;
using System.Collections.Generic;
using System.Security;
using System.Text;

namespace Kelvinvale.Domain.Entities
{
    public class Role : BaseAuditableEntity
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ICollection<User> Users { get; set; } = new List<User>();
    }
}
