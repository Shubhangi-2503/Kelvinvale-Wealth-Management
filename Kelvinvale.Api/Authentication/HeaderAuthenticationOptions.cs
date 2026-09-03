using Microsoft.AspNetCore.Authentication;

namespace Kelvinvale.Api.Authentication;

public class HeaderAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string SchemeName = "HeaderAuth";
}