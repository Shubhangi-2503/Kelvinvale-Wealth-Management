using Kelvinvale.Application.Interfaces;
using Kelvinvale.Domain.Entities;
using Kelvinvale.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Kelvinvale.Infrastructure.Repositories
{
    public class AuditRepository : IAuditRepository
    {
        private readonly KelvinvaleDbContext _context;

        public AuditRepository(KelvinvaleDbContext context)
        {
            _context = context;
        }

        public async Task<string?> GetUserRoleNameByIdAsync(Guid userId)
        {
            return await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == userId && u.IsActive)
                .Select(u => u.Role.Name)
                .FirstOrDefaultAsync();
        }

        public async Task InsertAuditLogAsync(AuditLog log)
        {
            await _context.AuditLogs.AddAsync(log);
            await _context.SaveChangesAsync();
        }
    }
}
