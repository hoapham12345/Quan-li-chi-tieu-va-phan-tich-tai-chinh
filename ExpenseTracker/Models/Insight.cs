// Models/Insight.cs
namespace ExpenseTracker.Models
{
    public class Insight
    {
        public string Type { get; set; } = "info"; // info | warn | danger | success
        public string Title { get; set; } = "";
        public string Detail { get; set; } = "";
        public decimal? Amount { get; set; } // số tiền liên quan (nếu có)
        public decimal? Percent { get; set; } // % liên quan (nếu có)
    }
}
