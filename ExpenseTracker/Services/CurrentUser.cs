using System.Security.Claims;

namespace ExpenseTracker.Services;

public interface ICurrentUser
{
    int? UserId { get; }
    string? Username { get; }
}

public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _http;

    public CurrentUser(IHttpContextAccessor http) => _http = http;

    public int? UserId
        => int.TryParse(_http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : (int?)null;

    public string? Username
        => _http.HttpContext?.User.FindFirstValue(ClaimTypes.Name);
}
