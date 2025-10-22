using CINEMA.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CINEMA.Controllers
{
    public class ShowtimeController : Controller
    {
        private readonly CinemaContext _context;

        public ShowtimeController(CinemaContext context)
        {
            _context = context;
        }

        // GET: Showtime
        public IActionResult Index()
        {
            var showtimes = _context.Showtimes
                .Include(s => s.Movie)
                .Include(s => s.Auditorium)
                .OrderByDescending(s => s.StartTime)
                .ToList();


            return View(showtimes);
        }

        // GET: Showtime/Create
        public IActionResult Create()
        {
            ViewBag.Movies = _context.Movies.ToList();
            ViewBag.Auditoriums = _context.Auditoriums.ToList();
            return View();
        }

        // POST: Showtime/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Showtime showtime)
        {
            if (ModelState.IsValid)
            {
                _context.Showtimes.Add(showtime);
                _context.SaveChanges();
                return RedirectToAction(nameof(Index));
            }
            return View(showtime);
        }

        // GET: Showtime/Edit/5
        public IActionResult Edit(int id)
        {
            var showtime = _context.Showtimes.Find(id);
            if (showtime == null) return NotFound();

            ViewBag.Movies = _context.Movies.ToList();
            ViewBag.Auditoriums = _context.Auditoriums.ToList();
            return View(showtime);
        }

        // POST: Showtime/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(Showtime showtime)
        {
            if (ModelState.IsValid)
            {
                _context.Update(showtime);
                _context.SaveChanges();
                return RedirectToAction(nameof(Index));
            }
            return View(showtime);
        }

        // GET: Showtime/Delete/5
        public IActionResult Delete(int id)
        {
            var showtime = _context.Showtimes
                .Include(s => s.Movie)
                .FirstOrDefault(s => s.ShowtimeId == id);

            if (showtime == null) return NotFound();
            return View(showtime);
        }

        // POST: Showtime/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            var showtime = _context.Showtimes.Find(id);
            if (showtime != null)
            {
                _context.Showtimes.Remove(showtime);
                _context.SaveChanges();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
