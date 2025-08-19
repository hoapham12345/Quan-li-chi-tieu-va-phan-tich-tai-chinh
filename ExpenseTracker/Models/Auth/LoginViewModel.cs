namespace ExpenseTracker.Models.Auth
{
    public class LoginViewModel
    {
        public string EmailOrUsername { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool RememberMe { get; set; }   
        public string? ReturnUrl { get; set; } 
    }
}
