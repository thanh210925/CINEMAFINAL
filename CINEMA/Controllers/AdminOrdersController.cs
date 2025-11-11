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

        // =================== [1] DANH SÁCH ĐƠN HÀNG ===================
        public async Task<IActionResult> Index(string search, string status)
        {
            var orders = _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.Tickets)
                .Include(o => o.OrderCombos)
                    .ThenInclude(oc => oc.Combo)
                .AsQueryable();

            // 🔍 Tìm theo tên khách hàng hoặc mã đơn
            if (!string.IsNullOrWhiteSpace(search))
            {
                orders = orders.Where(o =>
                    (o.Customer != null && o.Customer.FullName.Contains(search)) ||
                    o.OrderId.ToString().Contains(search));
            }

            // 🔍 Lọc theo trạng thái
            if (!string.IsNullOrWhiteSpace(status))
            {
                orders = orders.Where(o => o.Status == status);
            }

            var list = await orders
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return View(list);
        }

        // =================== [2] XEM CHI TIẾT ĐƠN HÀNG ===================
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

        // =================== [3] CẬP NHẬT TRẠNG THÁI (AJAX HOẶC POST FORM) ===================
        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] string status)
        {
            if (string.IsNullOrWhiteSpace(status))
                return BadRequest("⚠️ Trạng thái không hợp lệ.");

            var order = await _context.Orders
                .Include(o => o.Tickets)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
                return NotFound();

            // ✅ Cập nhật trạng thái đơn hàng
            order.Status = status;

            // ✅ Cập nhật trạng thái vé (nếu có)
            foreach (var ticket in order.Tickets)
            {
                ticket.Status = status;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = $"Đã cập nhật trạng thái đơn hàng #{id} thành '{status}'"
            });
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Tickets)
                .Include(o => o.OrderCombos)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
                return NotFound();

            // Xóa vé và combo liên quan
            _context.Tickets.RemoveRange(order.Tickets);
            _context.OrderCombos.RemoveRange(order.OrderCombos);
            _context.Orders.Remove(order);

            await _context.SaveChangesAsync();

            TempData["Success"] = $"✅ Đã xóa đơn hàng #{id}.";
            return RedirectToAction(nameof(Index));
        }
    }
}
