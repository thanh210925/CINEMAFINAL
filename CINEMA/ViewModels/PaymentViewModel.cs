using System;
using System.Collections.Generic;
using CINEMA.Models;

namespace CINEMA.ViewModels
{
    public class ComboViewModel
    {
        public int OrderComboId { get; set; }
        public int ComboId { get; set; }     // Id combo trong DB
        public string ComboName { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }

    public class PaymentViewModel
    {
        // 👇 THÊM 3 DÒNG NÀY ĐỂ GIỮ THÔNG TIN KHÁCH HÀNG 👇
        public string CustomerName { get; set; }
        public string CustomerEmail { get; set; }
        public string CustomerPhone { get; set; }
        // --------------------------------------------------

        // --- Các thuộc tính cũ của bạn ---
        public int TicketId { get; set; }
        public Ticket Ticket { get; set; } // Thuộc tính này có vẻ không cần thiết trong ViewModel này

        public int MovieId { get; set; }
        public int ShowtimeId { get; set; }
        public string MovieTitle { get; set; }
        public string Showtime { get; set; }
        public string Auditorium { get; set; }
        public List<string> SelectedSeats { get; set; } = new List<string>();

        public List<ComboViewModel> Combos { get; set; } = new List<ComboViewModel>();

        public int AdultTickets { get; set; }
        public int ChildTickets { get; set; }
        public int StudentTickets { get; set; }

        public decimal TotalPrice { get; set; }

        public string PaymentMethod { get; set; }
        public string PaymentImageUrl { get; set; }
    }
}