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
            var auditorium = _context.Auditoriums.Find(id);
            if (auditorium != null)
            {
                _context.Auditoriums.Remove(auditorium);
                _context.SaveChanges();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
