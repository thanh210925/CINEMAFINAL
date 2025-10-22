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
        public IActionResult Index()
        {
            var movies = _context.Movies.ToList();
            return View(movies);
        }
        public IActionResult Details(int id)
        {
            var movie = _context.Movies.FirstOrDefault(m => m.MovieId == id);
            if (movie == null) return NotFound();
            return View(movie);
        }
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Movie movie)
        {
            if (ModelState.IsValid)
            {
                _context.Movies.Add(movie);
                _context.SaveChanges();
                return RedirectToAction(nameof(Index));
            }
            return View(movie);
        }
        [HttpGet]
        public IActionResult Edit(int id)
        {
            var movie = _context.Movies.Find(id);
            if (movie == null) return NotFound();
            return View(movie);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(Movie movie)
        {
            if (ModelState.IsValid)
            {
                _context.Movies.Update(movie);
                _context.SaveChanges();
                return RedirectToAction(nameof(Index));
            }
            return View(movie);
        }
        [HttpGet]
        public IActionResult Delete(int id)
        {
            var movie = _context.Movies.FirstOrDefault(m => m.MovieId == id);
            if (movie == null) return NotFound();
            return View(movie);
        }
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            var movie = _context.Movies
                .Include(m => m.Showtimes)
                    .ThenInclude(s => s.Tickets) // nếu có vé trong suất chiếu
                .FirstOrDefault(m => m.MovieId == id);

            if (movie == null)
                return NotFound();

            // ✅ Xóa tất cả vé thuộc các suất chiếu của phim
            if (movie.Showtimes != null)
            {
                foreach (var showtime in movie.Showtimes)
                {
                    if (showtime.Tickets != null && showtime.Tickets.Any())
                    {
                        _context.Tickets.RemoveRange(showtime.Tickets);
                    }
                }

                // ✅ Sau đó xóa luôn các suất chiếu
                _context.Showtimes.RemoveRange(movie.Showtimes);
            }

            // ✅ Cuối cùng xóa phim
            _context.Movies.Remove(movie);
            _context.SaveChanges();

            TempData["SuccessMessage"] = $"Đã xóa phim \"{movie.Title}\" cùng toàn bộ suất chiếu liên quan.";
            return RedirectToAction(nameof(Index));
        }

    }
}

