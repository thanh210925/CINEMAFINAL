using System;
using System.Collections.Generic;

namespace CINEMA.ViewModels
{
    public class RevenueDashboardViewModel
    {
        // --- Tổng quan ---
        public decimal TotalRevenue { get; set; }
        public int TotalTickets { get; set; }

        public decimal TodayRevenue { get; set; }
        public int TodayTickets { get; set; }

        public decimal MonthRevenue { get; set; }
        public int MonthTickets { get; set; }

        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        // --- Doanh thu theo ngày ---
        public List<string> LabelsByDate { get; set; } = new();
        public List<decimal> RevenueByDate { get; set; } = new();

        // --- Doanh thu theo tháng ---
        public List<string> LabelsByMonth { get; set; } = new();
        public List<decimal> RevenueByMonth { get; set; } = new();

        // --- Doanh thu theo năm ---
        public List<string> LabelsByYear { get; set; } = new();
        public List<decimal> RevenueByYear { get; set; } = new();

        // --- Doanh thu theo phim ---
        public List<string> MovieLabels { get; set; } = new();
        public List<decimal> RevenueByMovie { get; set; } = new();
        public decimal ComboRevenue { get; set; }
        public int ComboSold { get; set; }
        public List<string> ComboLabelsByMonth { get; set; } = new();
        public List<decimal> ComboRevenueByMonth { get; set; } = new();
        public List<int> ComboQuantityByMonth { get; set; } = new();

        public List<int> TicketCountByDate { get; set; } = new();
        public List<int> TicketCountByMonth { get; set; } = new();
        public List<int> TicketCountByYear { get; set; } = new();

    }
}
