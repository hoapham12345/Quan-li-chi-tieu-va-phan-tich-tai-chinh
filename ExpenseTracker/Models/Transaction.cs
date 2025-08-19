using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Models
{
    public class Transaction
    {
        public int TransactionId { get; set; }
        public int UserId { get; set; }
        public int? CategoryId { get; set; }
        public decimal Amount { get; set; }
        [DataType(DataType.Date)]
        public DateTime TransactionDate { get; set; }
        public string? Note { get; set; }

        public Category? Category { get; set; }
    }
}
