using CINEMA.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CINEMA.Controllers
{
    public class GenreController : Controller
    {
        private readonly CinemaContext _context;

        public GenreController(CinemaContext context)
        {
            _context = context;
        }

        // 🟢 Danh sách thể loại + phim
        public IActionResult Index()
        {
            var genres = _context.Genres
                .Include(g => g.Movies)
                .ToList();

            return View(genres);
        }

        // 🟢 Thêm mới
        public IActionResult Create()
        {
            ViewBag.Movies = _context.Movies.ToList();
            return View();
        }

        [HttpPost]
        public IActionResult Create(Genre genre, int[] selectedMovies)
        {
            if (ModelState.IsValid)
            {
                foreach (var movieId in selectedMovies)
                {
                    var movie = _context.Movies.Find(movieId);
                    if (movie != null)
                        genre.Movies.Add(movie);
                }

                _context.Genres.Add(genre);
                _context.SaveChanges();
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Movies = _context.Movies.ToList();
            return View(genre);
        }

        // 🟢 Sửa
        public IActionResult Edit(int id)
        {
            var genre = _context.Genres
                .Include(g => g.Movies)
                .FirstOrDefault(g => g.GenreId == id);

            if (genre == null) return NotFound();

            ViewBag.Movies = _context.Movies.ToList();
            ViewBag.SelectedMovies = genre.Movies.Select(m => m.MovieId).ToList();

            return View(genre);
        }

        [HttpPost]
        public IActionResult Edit(int id, Genre genre, int[] selectedMovies)
        {
            var existing = _context.Genres
                .Include(g => g.Movies)
                .FirstOrDefault(g => g.GenreId == id);

            if (existing == null) return NotFound();

            existing.Name = genre.Name;
            existing.Description = genre.Description;

            existing.Movies.Clear();

            foreach (var movieId in selectedMovies)
            {
                var movie = _context.Movies.Find(movieId);
                if (movie != null)
                    existing.Movies.Add(movie);
            }

            _context.SaveChanges();
            return RedirectToAction(nameof(Index));
        }

        // 🟢 Xóa (GET)
        public IActionResult Delete(int id)
        {
            var genre = _context.Genres
                .Include(g => g.Movies)
                .FirstOrDefault(g => g.GenreId == id);

            if (genre == null) return NotFound();

            return View(genre);
        }

        // 🟢 Xóa (POST)
        [HttpPost, ActionName("Delete")]
        public IActionResult DeleteConfirmed(int id)
        {
            var genre = _context.Genres
                .Include(g => g.Movies)
                .FirstOrDefault(g => g.GenreId == id);

            if (genre != null)
            {
                // 🔥 Xóa quan hệ Many-to-Many trước
                genre.Movies.Clear();
                _context.SaveChanges();

                // 🔥 Sau đó mới xoá thể loại
                _context.Genres.Remove(genre);
                _context.SaveChanges();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
