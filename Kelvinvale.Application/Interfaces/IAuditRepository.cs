using Kelvinvale.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Kelvinvale.Application.Interfaces
{
    public interface IAuditRepository
    {
        Task<string?> GetUserRoleNameByIdAsync(Guid userId);
        Task InsertAuditLogAsync(AuditLog log);
    }
}

