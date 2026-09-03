using System.Data;

namespace Kelvinvale.Domain.Entities
{
    public class User: BaseAuditableEntity
    {
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public DateTime? DateOfBirth { get; set; }
        public Guid RoleId { get; set; }
        public Role Role { get; set; } = null!;
    }
}
