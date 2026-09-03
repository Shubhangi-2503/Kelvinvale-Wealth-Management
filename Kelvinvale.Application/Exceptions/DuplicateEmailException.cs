using System;
using System.Collections.Generic;
using System.Text;

namespace Kelvinvale.Application.Exceptions
{
    public class DuplicateEmailException : Exception
    {
        public DuplicateEmailException(string email)
        : base($"A user with the email address '{email}' already exists.")
        {
        }
    }
}
