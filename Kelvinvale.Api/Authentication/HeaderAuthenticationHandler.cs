using System.Security.Claims;
using System.Text.Encodings.Web;
using Kelvinvale.Application.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kelvinvale.Api.Authentication;

public class HeaderAuthenticationHandler : AuthenticationHandler<HeaderAuthenticationOptions>
{
    public HeaderAuthenticationHandler(
        IOptionsMonitor<HeaderAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // 1. Check header
        if (!Request.Headers.TryGetValue("X-Caller-Id", out var callerIdHeader))
        {
            return AuthenticateResult.NoResult();
        }

        // 2. Validate GUID format
        if (!Guid.TryParse(callerIdHeader.ToString(), out var callerId))
        {
            return AuthenticateResult.Fail("X-Caller-Id must be a valid GUID.");
        }

        // 3. Resolve IUserRepository via DI
        var userRepository = Context.RequestServices.GetRequiredService<IUserRepository>();
        var user = await userRepository.GetByIdWithRoleAsync(callerId);

        if (user == null)
        {
            return AuthenticateResult.Fail("User not found.");
        }

        // 4. Build claims from database record
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.UserName),
            new Claim(ClaimTypes.Role, user.Role.Name)
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}