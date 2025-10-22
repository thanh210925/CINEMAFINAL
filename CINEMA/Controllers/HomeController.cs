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
        // Hiển thị danh sách phim đang chiếu
        public IActionResult Index()
        {
            var movies = _context.Movies
                .Where(m => m.IsActive == true)
                .OrderByDescending(m => m.ReleaseDate)
                .ToList();

            return View(movies);
        }

        // ------------------ ĐẶT VÉ ------------------
        // GET: /Home/BookTicket/{id}?showtimeId=xxx
        [HttpGet]
        public IActionResult BookTicket(int id, int? showtimeId)
        {
            // ✅ 1. Lấy thông tin phim
            var movie = _context.Movies
                .Include(m => m.Genres)
                .FirstOrDefault(m => m.MovieId == id);

            if (movie == null)
                return NotFound("Không tìm thấy phim này.");

            // ✅ 2. Lấy suất chiếu cụ thể (nếu có)
            Showtime? showtime = null;
            if (showtimeId.HasValue)
            {
                showtime = _context.Showtimes
                    .Include(s => s.Auditorium)
                    .FirstOrDefault(s => s.ShowtimeId == showtimeId.Value && s.MovieId == id);
            }

            // ✅ 3. Lấy ghế theo phòng chiếu của suất chiếu
            var seats = new List<Seat>();
            if (showtime?.AuditoriumId != null)
            {
                seats = _context.Seats
                    .Where(s => s.AuditoriumId == showtime.AuditoriumId && s.IsActive == true)
                    .OrderBy(s => s.RowLabel)
                    .ThenBy(s => s.SeatNumber)
                    .ToList();
            }

            // ✅ 4. Lấy danh sách ghế đã đặt cho suất chiếu
            var bookedSeats = new List<string>();
            if (showtime != null)
            {
                bookedSeats = _context.Tickets
                    .Where(t => t.ShowtimeId == showtime.ShowtimeId)
                    .Include(t => t.Seat)
                    .Select(t => t.Seat.RowLabel + t.Seat.SeatNumber)
                    .ToList();
            }

            // ✅ 5. Lấy danh sách các suất chiếu khác của phim
            var showtimes = _context.Showtimes
                .Where(s => s.MovieId == id && s.IsActive == true && s.StartTime.HasValue)
                .OrderBy(s => s.StartTime)
                .ToList();

            // ✅ 6. Lấy danh sách combo đang hoạt động
            var combos = _context.Combos
                .Where(c => c.IsActive == true)
                .ToList();

            // ✅ 7. Truyền dữ liệu sang View
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
            // ✅ Lấy phim và suất chiếu
            var movie = _context.Movies
                .Include(m => m.Genres)
                .FirstOrDefault(m => m.MovieId == movieId);

            var showtime = _context.Showtimes
                .Include(s => s.Auditorium)
                .FirstOrDefault(s => s.ShowtimeId == showtimeId);

            if (movie == null || showtime == null)
                return NotFound("Phim hoặc suất chiếu không tồn tại.");

            // ✅ Lấy danh sách combo
            var combos = _context.Combos
                .Where(c => c.IsActive == true)
                .ToList();

            // ✅ Lấy ghế trong phòng chiếu
            var seats = _context.Seats
                .Where(s => s.AuditoriumId == showtime.AuditoriumId && s.IsActive == true)
                .OrderBy(s => s.RowLabel)
                .ThenBy(s => s.SeatNumber)
                .ToList();

            // ✅ Lấy danh sách ghế đã đặt
            var bookedSeats = _context.Tickets
                .Where(t => t.ShowtimeId == showtimeId)
                .Include(t => t.Seat)
                .Select(t => t.Seat.RowLabel + t.Seat.SeatNumber)
                .ToList();

            // ✅ Truyền sang view
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
            // ✅ 1. Ngày được chọn hoặc mặc định là hôm nay
            var selectedDate = date ?? DateTime.Today;

            // ✅ 2. Lấy danh sách phim đang chiếu có suất chiếu trong ngày đó
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

            // ✅ 3. Gửi ngày được chọn sang view
            ViewBag.SelectedDate = selectedDate;

            // ✅ 4. Trả danh sách phim (model chính)
            return View(movies);
        }

        // ------------------ PRIVACY & ERROR ------------------
        public IActionResult Privacy()
        {
            return View();
        }

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
            // Lấy thông tin phim, suất chiếu, combo
            var movie = _context.Movies.FirstOrDefault(m => m.MovieId == movieId);
            var showtime = _context.Showtimes.Include(s => s.Auditorium).FirstOrDefault(s => s.ShowtimeId == showtimeId);
            var combo = _context.Combos.FirstOrDefault(c => c.ComboId == comboId);

            if (movie == null || showtime == null)
                return NotFound("Phim hoặc suất chiếu không tồn tại.");

            // Tính tổng tiền
            int seatCount = selectedSeats.Split(',').Length;
            decimal ticketPrice = (showtime.BasePrice ?? 0) * seatCount;
            decimal comboPrice = combo?.Price ?? 0;
            decimal total = ticketPrice + comboPrice;

            // Gửi dữ liệu sang View Payment
            ViewBag.Movie = movie;
            ViewBag.Showtime = showtime;
            ViewBag.SelectedSeats = selectedSeats;
            ViewBag.Combo = combo;
            ViewBag.Total = total;

            return View("Payment");
        }

    }
}
