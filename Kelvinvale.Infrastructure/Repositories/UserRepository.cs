using Kelvinvale.Application.Interfaces;
using Kelvinvale.Domain.Entities;
using Kelvinvale.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Kelvinvale.Infrastructure.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly KelvinvaleDbContext _dbContext;

        public UserRepository(KelvinvaleDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<User?> GetByIdWithRoleAsync(Guid id)
        {
            return await _dbContext.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == id && u.IsActive);
        }
    }
}
