using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CINEMA.Models;
using System.Linq;
using System.Threading.Tasks;

namespace CINEMA.Controllers
{
    public class AdminOrdersController : Controller
    {
        private readonly CinemaContext _context;

        public AdminOrdersController(CinemaContext context)
        {
            _context = context;
        }

        // =================== [1] Danh sách đơn hàng ===================
        public async Task<IActionResult> Index(string search, string status)
        {
            var orders = _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.Tickets)
                .Include(o => o.OrderCombos)
                    .ThenInclude(oc => oc.Combo)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
                orders = orders.Where(o => o.Customer.FullName.Contains(search) || o.OrderId.ToString().Contains(search));

            if (!string.IsNullOrEmpty(status))
                orders = orders.Where(o => o.Status == status);

            var list = await orders
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return View(list);
        }

        // =================== [2] Xem chi tiết đơn hàng ===================
        public async Task<IActionResult> Details(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.Tickets)
                    .ThenInclude(t => t.Seat)
                .Include(o => o.Tickets)
                    .ThenInclude(t => t.Showtime)
                        .ThenInclude(s => s.Movie)
                .Include(o => o.OrderCombos)
                    .ThenInclude(oc => oc.Combo)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
                return NotFound();

            return View(order);
        }

        // =================== [3] Cập nhật trạng thái đơn hàng ===================
        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int orderId, string newStatus)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null)
                return NotFound();

            order.Status = newStatus;
            foreach (var ticket in _context.Tickets.Where(t => t.OrderId == orderId))
            {
                ticket.Status = newStatus;
                ticket.PaymentStatus = newStatus;
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đơn hàng #{orderId} đã được cập nhật trạng thái: {newStatus}.";
            return RedirectToAction(nameof(Index));
        }

        // =================== [4] Xóa đơn hàng ===================
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Tickets)
                .Include(o => o.OrderCombos)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
                return NotFound();

            _context.Tickets.RemoveRange(order.Tickets);
            _context.OrderCombos.RemoveRange(order.OrderCombos);
            _context.Orders.Remove(order);

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã xóa đơn hàng #{id}.";

            return RedirectToAction(nameof(Index));
        }
    }
}
