using CINEMA.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CINEMA.Controllers
{
    public class MovieController : Controller
    {
        private readonly CinemaContext _context;

        public MovieController(CinemaContext context)
        {
            _context = context;
        }

        // ------------------ DANH SÁCH PHIM ------------------
        public IActionResult Index()
        {
            var movies = _context.Movies
                .OrderByDescending(m => m.ReleaseDate)
                .ToList();

            return View(movies);
        }

        // ------------------ CHI TIẾT ------------------
        public IActionResult Details(int id)
        {
            var movie = _context.Movies.FirstOrDefault(m => m.MovieId == id);
            if (movie == null) return NotFound();
            return View(movie);
        }

        // ------------------ THÊM PHIM (GET) ------------------
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // ------------------ THÊM PHIM (POST) ------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Movie movie, IFormFile? PosterImage)
        {
            if (!ModelState.IsValid)
                return View(movie);

            // ✅ Upload ảnh poster nếu có
            if (PosterImage != null && PosterImage.Length > 0)
            {
                var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "movies");
                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                var fileName = Path.GetFileName(PosterImage.FileName);
                var filePath = Path.Combine(folderPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    PosterImage.CopyTo(stream);
                }

                movie.PosterUrl = "/images/movies/" + fileName;
            }

            movie.IsActive = true; // luôn hoạt động
            _context.Movies.Add(movie);
            _context.SaveChanges();

            TempData["SuccessMessage"] = "🎉 Thêm phim mới thành công!";
            return RedirectToAction(nameof(Index));
        }

        // ------------------ SỬA PHIM (GET) ------------------
        [HttpGet]
        public IActionResult Edit(int id)
        {
            var movie = _context.Movies.Find(id);
            if (movie == null) return NotFound();
            return View(movie);
        }

        // ------------------ SỬA PHIM (POST) ------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(Movie movie, IFormFile? PosterImage)
        {
            if (!ModelState.IsValid)
                return View(movie);

            // ✅ Nếu có ảnh mới thì cập nhật
            if (PosterImage != null && PosterImage.Length > 0)
            {
                var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "movies");
                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                var fileName = Path.GetFileName(PosterImage.FileName);
                var filePath = Path.Combine(folderPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    PosterImage.CopyTo(stream);
                }

                movie.PosterUrl = "/images/movies/" + fileName;
            }

            _context.Movies.Update(movie);
            _context.SaveChanges();

            TempData["SuccessMessage"] = "✏️ Cập nhật thông tin phim thành công!";
            return RedirectToAction(nameof(Index));
        }

        // ------------------ XÓA PHIM (GET) ------------------
        [HttpGet]
        public IActionResult Delete(int id)
        {
            var movie = _context.Movies.FirstOrDefault(m => m.MovieId == id);
            if (movie == null) return NotFound();
            return View(movie);
        }

        // ------------------ XÓA PHIM (POST) ------------------
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            var movie = _context.Movies
                .Include(m => m.Showtimes)
                    .ThenInclude(s => s.Tickets)
                .FirstOrDefault(m => m.MovieId == id);

            if (movie == null)
                return NotFound();

            // Xóa vé và suất chiếu trước
            if (movie.Showtimes != null)
            {
                foreach (var showtime in movie.Showtimes)
                {
                    if (showtime.Tickets?.Any() == true)
                        _context.Tickets.RemoveRange(showtime.Tickets);
                }

                _context.Showtimes.RemoveRange(movie.Showtimes);
            }

            _context.Movies.Remove(movie);
            _context.SaveChanges();

            TempData["SuccessMessage"] = $"🗑️ Đã xóa phim \"{movie.Title}\" cùng toàn bộ suất chiếu.";
            return RedirectToAction(nameof(Index));
        }
    }
}
