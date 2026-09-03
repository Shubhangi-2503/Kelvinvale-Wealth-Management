using Kelvinvale.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Kelvinvale.Application.Interfaces
{
    public interface IUserRepository
    {
      Task<User?> GetByIdWithRoleAsync(Guid id);
    }
}
