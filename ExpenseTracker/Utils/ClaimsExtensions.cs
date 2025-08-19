using System.Security.Claims;

namespace ExpenseTracker.Utils
{
    public static class ClaimsExtensions
    {
        public static int? GetUserId(this ClaimsPrincipal user)
        {
            var s = user.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(s, out var id) ? id : (int?)null;
        }
    }
}
