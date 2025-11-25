using CINEMA.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CINEMA.Controllers
{
    public class AuditoriumController : Controller
    {
        private readonly CinemaContext _context;

        public AuditoriumController(CinemaContext context)
        {
            _context = context;
        }

        // 📋 Danh sách phòng chiếu
        public IActionResult Index()
        {
            var auditoriums = _context.Auditoriums
                .Include(a => a.Theater)  // 🔹 Load thông tin rạp liên kết
                .OrderBy(a => a.AuditoriumId)
                .ToList();

            return View(auditoriums);
        }


        // ➕ Trang thêm mới
        public IActionResult Create()
        {
            ViewBag.Theaters = _context.Theaters.ToList();
            return View();
        }

        [HttpPost]
        public IActionResult Create(Auditorium auditorium)
        {
            if (ModelState.IsValid)
            {
                _context.Auditoriums.Add(auditorium);
                _context.SaveChanges();

                // ✅ Sinh ghế tự động
                int rows = auditorium.SeatRows ?? 0;
                int cols = auditorium.SeatCols ?? 0;
                for (int r = 0; r < rows; r++)
                {
                    char rowLabel = (char)('A' + r);
                    for (int c = 1; c <= cols; c++)
                    {
                        _context.Seats.Add(new Seat
                        {
                            AuditoriumId = auditorium.AuditoriumId,
                            RowLabel = rowLabel.ToString(),
                            SeatNumber = c,
                            SeatType = "Thường",
                            IsActive = true
                        });
                    }
                }
                _context.SaveChanges();

                return RedirectToAction(nameof(Index));
            }

            ViewBag.Theaters = _context.Theaters.ToList();
            return View(auditorium);
        }


        // ✏️ Sửa
        public IActionResult Edit(int id)
        {
            var auditorium = _context.Auditoriums.Find(id);
            if (auditorium == null) return NotFound();

            ViewBag.Theaters = _context.Theaters.ToList();
            return View(auditorium);
        }

        [HttpPost]
        public IActionResult Edit(Auditorium auditorium)
        {
            if (ModelState.IsValid)
            {
                _context.Auditoriums.Update(auditorium);
                _context.SaveChanges();
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Theaters = _context.Theaters.ToList();
            return View(auditorium);
        }

        // 🗑️ Xóa
        public IActionResult Delete(int id)
        {
            var auditorium = _context.Auditoriums
                .Include(a => a.Theater)
                .FirstOrDefault(a => a.AuditoriumId == id);

            if (auditorium == null) return NotFound();
            return View(auditorium);
        }

        [HttpPost, ActionName("Delete")]
        public IActionResult DeleteConfirmed(int id)
        {
            var auditorium = _context.Auditoriums
                .Include(a => a.Seats)
                .Include(a => a.Showtimes)
                    .ThenInclude(s => s.Tickets)
                .FirstOrDefault(a => a.AuditoriumId == id);

            if (auditorium == null)
                return NotFound();

            // 1️⃣ XÓA vé trước
            foreach (var showtime in auditorium.Showtimes)
            {
                if (showtime.Tickets.Any())
                    _context.Tickets.RemoveRange(showtime.Tickets);
            }
            _context.SaveChanges();  // ⚡ BẮT BUỘC

            // 2️⃣ XÓA suất chiếu
            if (auditorium.Showtimes.Any())
            {
                _context.Showtimes.RemoveRange(auditorium.Showtimes);
                _context.SaveChanges();   // ⚡ BẮT BUỘC
            }

            // 3️⃣ XÓA ghế
            if (auditorium.Seats.Any())
            {
                _context.Seats.RemoveRange(auditorium.Seats);
                _context.SaveChanges();   // ⚡ BẮT BUỘC
            }

            // 4️⃣ XÓA phòng chiếu
            _context.Auditoriums.Remove(auditorium);
            _context.SaveChanges();

            TempData["SuccessMessage"] = "🗑️ Đã xóa phòng chiếu và toàn bộ dữ liệu liên quan.";
            return RedirectToAction(nameof(Index));
        }

    }
}
