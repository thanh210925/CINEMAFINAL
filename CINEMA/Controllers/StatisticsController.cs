using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CINEMA.Models;
using CINEMA.ViewModels;

namespace CINEMA.Controllers
{
    public class StatisticsController : Controller
    {
        private readonly CinemaContext _context;

        public StatisticsController(CinemaContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(DateTime? from, DateTime? to)
        {
            var model = new RevenueDashboardViewModel
            {
                FromDate = from,
                ToDate = to
            };

            var today = DateTime.Today;
            var startOfMonth = new DateTime(today.Year, today.Month, 1);

            // 🔹 Lấy đơn hàng ĐÃ THANH TOÁN
            var paidOrders = _context.Orders
                .Include(o => o.Tickets)
                    .ThenInclude(t => t.Showtime)
                        .ThenInclude(s => s.Movie)
                .Include(o => o.OrderCombos)
                    .ThenInclude(oc => oc.Combo)
                .Where(o => o.Status != null &&
                            o.Status.ToLower().Contains("thanh toán") &&
                            o.CreatedAt != null);

            // 🔹 Lọc theo khoảng thời gian
            if (from.HasValue)
                paidOrders = paidOrders.Where(o => o.CreatedAt >= from.Value.Date);

            if (to.HasValue)
            {
                var toEnd = to.Value.Date.AddDays(1).AddTicks(-1);
                paidOrders = paidOrders.Where(o => o.CreatedAt <= toEnd);
            }

            // ✅ Tổng doanh thu & vé toàn kỳ
            model.TotalRevenue = await paidOrders.SumAsync(o => (decimal?)o.TotalAmount) ?? 0;
            model.TotalTickets = await paidOrders.SelectMany(o => o.Tickets).CountAsync();

            // ✅ Doanh thu hôm nay
            var todayOrders = paidOrders.Where(o => o.CreatedAt!.Value.Date == today);
            model.TodayRevenue = await todayOrders.SumAsync(o => (decimal?)o.TotalAmount) ?? 0;
            model.TodayTickets = await todayOrders.SelectMany(o => o.Tickets).CountAsync();

            // ✅ Doanh thu tháng này
            var monthOrders = paidOrders.Where(o => o.CreatedAt!.Value >= startOfMonth);
            model.MonthRevenue = await monthOrders.SumAsync(o => (decimal?)o.TotalAmount) ?? 0;
            model.MonthTickets = await monthOrders.SelectMany(o => o.Tickets).CountAsync();

            // ✅ Thống kê combo bắp nước tổng
            var comboData = await _context.OrderCombos
                .Include(oc => oc.Order)
                .Where(oc => oc.Order != null &&
                             oc.Order.Status != null &&
                             oc.Order.Status.ToLower().Contains("thanh toán"))
                .ToListAsync();

            model.ComboRevenue = comboData.Sum(oc => (oc.UnitPrice ?? 0) * (oc.Quantity ?? 0));
            model.ComboSold = comboData.Sum(oc => oc.Quantity ?? 0);

            // ✅ Doanh thu & vé theo ngày
            var dailyStats = await paidOrders
                .Select(o => new
                {
                    Date = o.CreatedAt!.Value.Date,
                    Revenue = o.TotalAmount ?? 0,
                    Tickets = o.Tickets.Count
                })
                .GroupBy(x => x.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Revenue = g.Sum(x => x.Revenue),
                    TicketCount = g.Sum(x => x.Tickets)
                })
                .OrderBy(x => x.Date)
                .ToListAsync();

            foreach (var d in dailyStats)
            {
                model.LabelsByDate.Add(d.Date.ToString("dd/MM"));
                model.RevenueByDate.Add(d.Revenue);
                model.TicketCountByDate.Add(d.TicketCount);
            }

            // ✅ Doanh thu & vé theo tháng
            var monthlyStats = await paidOrders
                .Select(o => new
                {
                    Year = o.CreatedAt!.Value.Year,
                    Month = o.CreatedAt.Value.Month,
                    Revenue = o.TotalAmount ?? 0,
                    Tickets = o.Tickets.Count
                })
                .GroupBy(x => new { x.Year, x.Month })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    Revenue = g.Sum(x => x.Revenue),
                    TicketCount = g.Sum(x => x.Tickets)
                })
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToListAsync();

            foreach (var m in monthlyStats)
            {
                model.LabelsByMonth.Add($"{m.Month}/{m.Year}");
                model.RevenueByMonth.Add(m.Revenue);
                model.TicketCountByMonth.Add(m.TicketCount);
            }

            // ✅ Doanh thu & vé theo năm
            var yearlyStats = await paidOrders
                .Select(o => new
                {
                    Year = o.CreatedAt!.Value.Year,
                    Revenue = o.TotalAmount ?? 0,
                    Tickets = o.Tickets.Count
                })
                .GroupBy(x => x.Year)
                .Select(g => new
                {
                    Year = g.Key,
                    Revenue = g.Sum(x => x.Revenue),
                    TicketCount = g.Sum(x => x.Tickets)
                })
                .OrderByDescending(x => x.Year)
                .Take(5)
                .ToListAsync();

            yearlyStats.Reverse(); // hiển thị theo thứ tự tăng dần

            foreach (var y in yearlyStats)
            {
                model.LabelsByYear.Add(y.Year.ToString());
                model.RevenueByYear.Add(y.Revenue);
                model.TicketCountByYear.Add(y.TicketCount);
            }

            // ✅ Combo theo tháng
            var comboByMonth = await paidOrders
                .SelectMany(o => o.OrderCombos)
                .Where(oc => oc.Order != null && oc.Order.CreatedAt != null)
                .GroupBy(oc => new
                {
                    oc.Order.CreatedAt!.Value.Year,
                    oc.Order.CreatedAt.Value.Month
                })
                .Select(g => new
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Revenue = g.Sum(x => (x.UnitPrice ?? 0) * (x.Quantity ?? 0)),
                    Quantity = g.Sum(x => x.Quantity ?? 0)
                })
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToListAsync();

            foreach (var item in comboByMonth)
            {
                model.ComboLabelsByMonth.Add($"{item.Month}/{item.Year}");
                model.ComboRevenueByMonth.Add(item.Revenue);
                model.ComboQuantityByMonth.Add(item.Quantity);
            }

            // ✅ Top 5 phim doanh thu cao nhất
            var revenueByMovie = await paidOrders
                .SelectMany(o => o.Tickets)
                .Where(t => t.Showtime != null && t.Showtime.Movie != null)
                .GroupBy(t => t.Showtime.Movie.Title)
                .Select(g => new
                {
                    MovieTitle = g.Key,
                    Revenue = g.Sum(x => x.Price) ?? 0
                })
                .OrderByDescending(x => x.Revenue)
                .Take(5)
                .ToListAsync();

            foreach (var mv in revenueByMovie)
            {
                model.MovieLabels.Add(mv.MovieTitle);
                model.RevenueByMovie.Add(mv.Revenue);
            }

            return View(model);
        }
    }
}
