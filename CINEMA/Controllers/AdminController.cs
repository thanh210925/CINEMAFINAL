using CINEMA.Models;
using Microsoft.AspNetCore.Mvc;

namespace CINEMA.Controllers
{
    public class AdminController : Controller
    {
        private readonly CinemaContext _context;

        public AdminController(CinemaContext context)
        {
            _context = context;
        }
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Register(string fullName, string email, string password, string? phone)
        {
            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Vui lòng nhập đầy đủ thông tin!";
                return View();
            }

            var existAdmin = _context.Admins.FirstOrDefault(a => a.Email == email);
            if (existAdmin != null)
            {
                ViewBag.Error = "Email đã tồn tại!";
                return View();
            }

            var admin = new Admin
            {
                FullName = fullName,
                Email = email,
                PasswordHash = password,
                Phone = phone,
                CreatedAt = DateTime.Now
            };

            _context.Admins.Add(admin);
            _context.SaveChanges();

            return RedirectToAction("Login", "Admin");
        }
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Login(string email, string password)
        {
            var admin = _context.Admins.FirstOrDefault(a => a.Email == email && a.PasswordHash == password);
            if (admin != null)
            {
                HttpContext.Session.SetString("Role", "Admin");
                HttpContext.Session.SetString("Name", admin.FullName);

                admin.LastLogin = DateTime.Now;
                _context.SaveChanges();

                return RedirectToAction("Dashboard", "Admin");
            }

            ViewBag.Error = "Sai tài khoản hoặc mật khẩu!";
            return View();
        }
        public IActionResult Dashboard()
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
            {
                return RedirectToAction("Login", "Admin");
            }

            ViewBag.Name = HttpContext.Session.GetString("Name");
            return View();
        }
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Admin");
        }
    }
}
