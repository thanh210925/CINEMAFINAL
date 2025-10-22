using CINEMA.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using Microsoft.AspNetCore.Http;
using CINEMA.ViewModels;

namespace CINEMA.Controllers
{
    public class CustomerController : Controller
    {
        private readonly CinemaContext _context;

        public CustomerController(CinemaContext context)
        {
            _context = context;
        }

        // ------------------ 🟢 ĐĂNG KÝ ------------------
        [HttpGet]
        public IActionResult Register()
        {
            return View(new RegisterViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // Kiểm tra email trùng
            var exist = _context.Customers.FirstOrDefault(c => c.Email == model.Email);
            if (exist != null)
            {
                ViewBag.Error = "Email đã tồn tại!";
                return View(model);
            }

            // Tạo mới khách hàng
            var customer = new Customer
            {
                FullName = model.FullName,
                Email = model.Email,
                Phone = model.Phone,
                BirthDate = model.BirthDate,
                Gender = model.Gender,
                CreatedAt = DateTime.Now,
                PasswordHash = model.Password // ❗ Bé đang không mã hóa
            };

            _context.Customers.Add(customer);
            _context.SaveChanges();

            // Sau khi đăng ký → về trang Login
            TempData["Success"] = "Đăng ký thành công! Hãy đăng nhập để tiếp tục.";
            return RedirectToAction("Login", "Customer");
        }

        // ------------------ 🟢 ĐĂNG NHẬP ------------------
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            // Giữ returnUrl để sau đăng nhập xong quay lại trang trước
            var model = new LoginViewModel { ReturnUrl = returnUrl ?? Url.Action("Index", "Home") };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Error = "Vui lòng nhập đầy đủ thông tin.";
                return View(model);
            }

            // Tìm khách hàng
            var customer = _context.Customers
                .FirstOrDefault(c => c.Email == model.Email && c.PasswordHash == model.Password);

            if (customer == null)
            {
                ViewBag.Error = "Sai tài khoản hoặc mật khẩu!";
                return View(model);
            }

            // 🟩 Lưu thông tin session
            HttpContext.Session.SetInt32("CustomerId", customer.CustomerId);
            HttpContext.Session.SetString("CustomerName", customer.FullName);
            HttpContext.Session.SetString("CustomerEmail", customer.Email);

            // 🟩 Điều hướng
            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return Redirect(model.ReturnUrl);
            else
                return RedirectToAction("Index", "Home");
        }

        // ------------------ 🟢 QUÊN MẬT KHẨU ------------------
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ForgotPassword(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                ViewBag.Error = "Vui lòng nhập email.";
                return View();
            }

            var customer = _context.Customers.FirstOrDefault(c => c.Email == email);
            if (customer == null)
            {
                ViewBag.Message = $"Nếu email {email} tồn tại, chúng tôi đã gửi hướng dẫn đặt lại mật khẩu.";
                return View();
            }

            ViewBag.Message = $"Hướng dẫn đặt lại mật khẩu đã được gửi đến {email}.";
            return View();
        }

        // ------------------ 🟢 ĐĂNG XUẤT ------------------
        [HttpGet]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Customer");
        }

        // ------------------ 🟢 HỒ SƠ CÁ NHÂN ------------------
        [HttpGet]
        public IActionResult Profile()
        {
            var customerId = HttpContext.Session.GetInt32("CustomerId");
            if (customerId == null)
                return RedirectToAction("Login", "Customer");

            var customer = _context.Customers.FirstOrDefault(c => c.CustomerId == customerId);
            if (customer == null)
                return RedirectToAction("Login", "Customer");

            return View(customer);
        }
    }
}
