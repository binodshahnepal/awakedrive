using Dms.Api.Data.Entities;
using Dms.Shared.Contracts.Auth;

namespace Dms.Api.Services;

public interface IJwtTokenService
{
    LoginResponse IssueToken(User user);
}
