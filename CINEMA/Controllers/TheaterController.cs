using CINEMA.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CINEMA.Controllers
{
    public class TheaterController : Controller
    {
        private readonly CinemaContext _context;

        public TheaterController(CinemaContext context)
        {
            _context = context;
        }

        // 📋 Hiển thị danh sách rạp chiếu
        public IActionResult Index()
        {
            var theaters = _context.Theaters
                .OrderBy(t => t.TheaterId)
                .ToList();
            return View(theaters);
        }

        // ➕ Trang thêm mới
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Theater theater)
        {
            if (ModelState.IsValid)
            {
                theater.CreatedAt = DateTime.Now;
                theater.IsActive = true;

                _context.Theaters.Add(theater);
                _context.SaveChanges();
                return RedirectToAction(nameof(Index));
            }
            return View(theater);
        }

        // ✏️ Trang chỉnh sửa
        public IActionResult Edit(int id)
        {
            var theater = _context.Theaters.Find(id);
            if (theater == null)
                return NotFound();

            return View(theater);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(Theater theater)
        {
            if (ModelState.IsValid)
            {
                _context.Theaters.Update(theater);
                _context.SaveChanges();
                return RedirectToAction(nameof(Index));
            }
            return View(theater);
        }

        // 🗑️ Trang xác nhận xóa
        public IActionResult Delete(int id)
        {
            var theater = _context.Theaters.FirstOrDefault(t => t.TheaterId == id);
            if (theater == null)
                return NotFound();

            return View(theater);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            var theater = _context.Theaters.Find(id);
            if (theater != null)
            {
                _context.Theaters.Remove(theater);
                _context.SaveChanges();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
