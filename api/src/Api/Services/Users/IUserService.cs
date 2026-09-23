using Api.Common;
using Api.Dtos.Users;

namespace Api.Services.Users;

public interface IUserService
{
    Task<Result<UserDto>> GetAsync(Guid userId, CancellationToken ct);
}
