namespace ExpenseTracker.Models
{
    public class Category
    {
        public int CategoryId { get; set; }
        public int UserId { get; set; }
        public string Name { get; set; } = "";
        public string Type { get; set; } = "expense";
    }
}
