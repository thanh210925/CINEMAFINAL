using System.Diagnostics;
using CINEMA.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CINEMA.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly CinemaContext _context;

        public HomeController(ILogger<HomeController> logger, CinemaContext context)
        {
            _logger = logger;
            _context = context;
        }

        // ------------------ TRANG CHỦ ------------------
        public IActionResult Index()
        {
            var movies = _context.Movies
                .Where(m => m.IsActive == true)
                .OrderByDescending(m => m.ReleaseDate)
                .ToList();

            // ✅ Lấy ngày hôm nay dưới dạng DateOnly để so với ReleaseDate (DateOnly?)
            var today = DateOnly.FromDateTime(DateTime.Today);

            // ✅ Phim sắp chiếu: còn active, có ReleaseDate, và ReleaseDate > hôm nay
            var comingSoon = _context.Movies
                .Where(m => m.IsActive == true
                            && m.ReleaseDate.HasValue
                            && m.ReleaseDate.Value > today)
                .OrderBy(m => m.ReleaseDate)
                .ToList();

            ViewBag.ComingSoon = comingSoon;

            return View(movies);
        }


        // ------------------ ĐẶT VÉ (GET) ------------------
        [HttpGet]
        public IActionResult BookTicket(int id, int? showtimeId)
        {
            var movie = _context.Movies
                .Include(m => m.Genres)
                .FirstOrDefault(m => m.MovieId == id);

            if (movie == null)
                return NotFound("Không tìm thấy phim này.");

            if (!showtimeId.HasValue)
            {
                showtimeId = _context.Showtimes
                    .Where(s => s.MovieId == id && s.IsActive == true && s.StartTime.HasValue)
                    .OrderBy(s => s.StartTime)
                    .Select(s => s.ShowtimeId)
                    .FirstOrDefault();
            }

            Showtime? showtime = null;
            if (showtimeId.HasValue)
            {
                showtime = _context.Showtimes
                    .Include(s => s.Auditorium)
                    .FirstOrDefault(s => s.ShowtimeId == showtimeId.Value && s.MovieId == id);
            }

            var seats = new List<Seat>();
            if (showtime?.AuditoriumId != null)
            {
                seats = _context.Seats
                    .Where(s => s.AuditoriumId == showtime.AuditoriumId && s.IsActive == true)
                    .OrderBy(s => s.RowLabel)
                    .ThenBy(s => s.SeatNumber)
                    .ToList();
            }

            var bookedSeats = new List<string>();
            if (showtime != null)
            {
                bookedSeats = _context.Tickets
                    .Where(t => t.ShowtimeId == showtime.ShowtimeId)
                    .Include(t => t.Seat)
                    .Select(t => t.Seat.RowLabel + t.Seat.SeatNumber)
                    .ToList();
            }

            var showtimes = _context.Showtimes
                .Where(s => s.MovieId == id && s.IsActive == true && s.StartTime.HasValue)
                .OrderBy(s => s.StartTime)
                .ToList();

            var combos = _context.Combos
                .Where(c => c.IsActive == true)
                .ToList();

            ViewBag.Showtime = showtime;
            ViewBag.Showtimes = showtimes;
            ViewBag.Combos = combos;
            ViewBag.Seats = seats;
            ViewBag.BookedSeats = bookedSeats;

            return View(movie);
        }

        // ------------------ ĐẶT VÉ (POST) ------------------
        [HttpPost]
        public IActionResult BookTicket(int movieId, int showtimeId)
        {
            var movie = _context.Movies
                .Include(m => m.Genres)
                .FirstOrDefault(m => m.MovieId == movieId);

            var showtime = _context.Showtimes
                .Include(s => s.Auditorium)
                .FirstOrDefault(s => s.ShowtimeId == showtimeId);

            if (movie == null || showtime == null)
                return NotFound("Phim hoặc suất chiếu không tồn tại.");

            var combos = _context.Combos
                .Where(c => c.IsActive == true)
                .ToList();

            var seats = _context.Seats
                .Where(s => s.AuditoriumId == showtime.AuditoriumId && s.IsActive == true)
                .OrderBy(s => s.RowLabel)
                .ThenBy(s => s.SeatNumber)
                .ToList();

            var bookedSeats = _context.Tickets
                .Where(t => t.ShowtimeId == showtimeId)
                .Include(t => t.Seat)
                .Select(t => t.Seat.RowLabel + t.Seat.SeatNumber)
                .ToList();

            ViewBag.Showtime = showtime;
            ViewBag.Combos = combos;
            ViewBag.Seats = seats;
            ViewBag.BookedSeats = bookedSeats;

            return View(movie);
        }

        // ------------------ API: LẤY SUẤT CHIẾU THEO PHIM ------------------
        [HttpGet]
        public IActionResult GetShowtimesByMovie(int movieId)
        {
            var showtimes = _context.Showtimes
                .Where(s => s.MovieId == movieId && s.IsActive == true && s.StartTime.HasValue)
                .OrderBy(s => s.StartTime)
                .Select(s => new
                {
                    s.ShowtimeId,
                    Date = s.StartTime.Value.ToString("yyyy-MM-dd"),
                    Time = s.StartTime.Value.ToString("HH:mm"),
                    Price = s.BasePrice ?? 0,
                    Auditorium = s.Auditorium != null ? s.Auditorium.Name : "Chưa rõ phòng"
                })
                .ToList();

            return Json(showtimes);
        }

        // ------------------ LỊCH CHIẾU ------------------
        [HttpGet]
        public IActionResult Schedule(DateTime? date)
        {
            var selectedDate = date ?? DateTime.Today;

            var movies = _context.Movies
                .Include(m => m.Genres)
                .Include(m => m.Showtimes)
                    .ThenInclude(s => s.Auditorium)
                .Where(m => m.IsActive == true &&
                            m.Showtimes.Any(s => s.StartTime.HasValue &&
                                                 s.StartTime.Value.Date == selectedDate.Date &&
                                                 s.IsActive == true))
                .OrderBy(m => m.Title)
                .ToList();

            ViewBag.SelectedDate = selectedDate;
            return View(movies);
        }

        // ------------------ PRIVACY & ERROR ------------------
        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }

        // ------------------ THANH TOÁN ------------------
        [HttpPost]
        public IActionResult GoToPayment(int movieId, int showtimeId, string selectedSeats, int comboId)
        {
            var movie = _context.Movies.FirstOrDefault(m => m.MovieId == movieId);
            var showtime = _context.Showtimes
                .Include(s => s.Auditorium)
                .FirstOrDefault(s => s.ShowtimeId == showtimeId);
            var combo = _context.Combos.FirstOrDefault(c => c.ComboId == comboId);

            if (movie == null || showtime == null)
                return NotFound("Phim hoặc suất chiếu không tồn tại.");

            int seatCount = selectedSeats.Split(',', StringSplitOptions.RemoveEmptyEntries).Length;
            decimal ticketPrice = (showtime.BasePrice ?? 0) * seatCount;
            decimal comboPrice = combo?.Price ?? 0;
            decimal total = ticketPrice + comboPrice;

            ViewBag.Movie = movie;
            ViewBag.Showtime = showtime;
            ViewBag.SelectedSeats = selectedSeats;
            ViewBag.Combo = combo;
            ViewBag.Total = total;

            return View("Payment");
        }
    }
}
