using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CINEMA.Models;
using System.Linq;
using System.Threading.Tasks;

namespace CINEMA.Controllers
{
    public class TicketsController : Controller
    {
        private readonly CinemaContext _context;

        public TicketsController(CinemaContext context)
        {
            _context = context;
        }

        // =================== [1] Danh sách vé của người dùng ===================
        public IActionResult MyTickets()
        {
            var customerId = HttpContext.Session.GetInt32("CustomerId");
            if (customerId == null)
                return RedirectToAction("Login", "Customer");

            var orders = _context.Orders
                .Where(o => o.CustomerId == customerId)
                .Include(o => o.Tickets)
                    .ThenInclude(t => t.Seat)
                .Include(o => o.Tickets)
                    .ThenInclude(t => t.Showtime)
                        .ThenInclude(s => s.Movie)
                .Include(o => o.Tickets)
                    .ThenInclude(t => t.Showtime)
                        .ThenInclude(s => s.Auditorium) // ✅ Lấy thông tin phòng chiếu
                .Include(o => o.OrderCombos)
                    .ThenInclude(oc => oc.Combo) // ✅ Lấy thông tin combo
                .OrderByDescending(o => o.CreatedAt)
                .ToList();

            return View(orders);
        }

        // =================== [2] Hủy vé (chỉ khi chưa thanh toán) ===================
        [HttpPost]
        public async Task<IActionResult> CancelOrder(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.Tickets)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null)
                return NotFound();

            // 🔹 Chỉ cho phép hủy nếu chưa thanh toán
            if (order.Status == "Chờ thanh toán" || order.Status == "Đang chờ thanh toán")
            {
                order.Status = "Đã hủy";
                foreach (var t in order.Tickets)
                {
                    t.Status = "Đã hủy";
                    t.PaymentStatus = "Đã hủy";
                }

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã hủy đơn #{order.OrderId} thành công.";
            }
            else
            {
                TempData["ErrorMessage"] = "❌ Không thể hủy đơn đã thanh toán hoặc đang xử lý.";
            }

            return RedirectToAction("MyTickets");
        }

        // =================== [3] Xem chi tiết vé ===================
        public async Task<IActionResult> Details(int id)
        {
            var customerId = HttpContext.Session.GetInt32("CustomerId");
            if (customerId == null)
                return RedirectToAction("Login", "Customer");

            var order = await _context.Orders
                .Include(o => o.Customer) // ✅ Lấy thông tin người mua
                .Include(o => o.Tickets)
                    .ThenInclude(t => t.Seat)
                .Include(o => o.Tickets)
                    .ThenInclude(t => t.Showtime)
                        .ThenInclude(s => s.Movie)
                .Include(o => o.Tickets)
                    .ThenInclude(t => t.Showtime)
                        .ThenInclude(s => s.Auditorium)
                .Include(o => o.OrderCombos)
                    .ThenInclude(oc => oc.Combo)
                .FirstOrDefaultAsync(o => o.OrderId == id && o.CustomerId == customerId);

            if (order == null)
                return NotFound();

            return View(order);
        }
    }
}
