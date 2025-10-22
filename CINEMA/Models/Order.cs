using System;
using System.Collections.Generic;

namespace CINEMA.Models
{
    public partial class Order
    {
        public int OrderId { get; set; }

        public int? CustomerId { get; set; }

        public string? Status { get; set; }

        public string? PaymentMethod { get; set; }

        public string? PaymentStatus { get; set; }

        public decimal? TotalAmount { get; set; }

        public DateTime? CreatedAt { get; set; }

        public DateTime? PaidAt { get; set; }

        public virtual Customer? Customer { get; set; }

        public virtual ICollection<OrderCombo> OrderCombos { get; set; } = new List<OrderCombo>();

        public virtual ICollection<Ticket> Tickets { get; set; } = new List<Ticket>(); // 👈 Thêm danh sách vé
    }
}
