using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using CINEMA.Models;
using CINEMA.ViewModels;
using CINEMA.Helpers; // VnpayLibrary

namespace CINEMA.Controllers
{
    public class PaymentController : Controller
    {
        private readonly CinemaContext _context;
        private readonly IConfiguration _config;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(CinemaContext context, IConfiguration config, ILogger<PaymentController> logger)
        {
            _context = context;
            _config = config;
            _logger = logger;
        }

        // =================== [1] Trang xác nhận thanh toán ===================
        [HttpPost]
        public IActionResult Index(int MovieId, int ShowtimeId, string[] Seats, int AdultTickets, int ChildTickets, int StudentTickets, decimal TotalPrice)
        {
            var customerId = HttpContext.Session.GetInt32("CustomerId");
            if (customerId == null)
            {
                // 🔹 Lưu dữ liệu tạm để quay lại sau đăng nhập
                TempData["MovieId"] = MovieId;
                TempData["ShowtimeId"] = ShowtimeId;
                TempData["Seats"] = JsonSerializer.Serialize(Seats ?? Array.Empty<string>());
                TempData["AdultTickets"] = AdultTickets;
                TempData["ChildTickets"] = ChildTickets;
                TempData["StudentTickets"] = StudentTickets;
                TempData["TotalPrice"] = TotalPrice.ToString(CultureInfo.InvariantCulture);

                var comboDict = Request.Form.Keys
                    .Where(k => k.StartsWith("Combo_"))
                    .ToDictionary(k => k, k => Request.Form[k].ToString());
                TempData["Combos"] = JsonSerializer.Serialize(comboDict);

                var returnUrl = Url.Action(nameof(ResumePayment), "Payment");
                return RedirectToAction("Login", "Customer", new { ReturnUrl = returnUrl });
            }

            var customer = _context.Customers.Find(customerId);
            if (customer == null)
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login", "Customer", new { message = "Tài khoản không tồn tại." });
            }

            var showtime = _context.Showtimes
                .Include(s => s.Auditorium)
                .Include(s => s.Movie)
                .FirstOrDefault(s => s.ShowtimeId == ShowtimeId);
            if (showtime == null) return NotFound();

            // 🔹 Lấy combo từ form
            var combosVm = new List<ComboViewModel>();
            foreach (var key in Request.Form.Keys.Where(k => k.StartsWith("Combo_")))
            {
                if (int.TryParse(key.Replace("Combo_", ""), out int comboId) &&
                    int.TryParse(Request.Form[key], out int qty) &&
                    qty > 0)
                {
                    var combo = _context.Combos.FirstOrDefault(c => c.ComboId == comboId);
                    if (combo != null)
                    {
                        combosVm.Add(new ComboViewModel
                        {
                            ComboId = combo.ComboId,
                            ComboName = combo.Name,
                            Quantity = qty,
                            Price = combo.Price ?? 0
                        });
                    }
                }
            }

            var vm = new PaymentViewModel
            {
                CustomerName = customer.FullName,
                CustomerEmail = customer.Email,
                CustomerPhone = customer.Phone,
                MovieId = MovieId,
                ShowtimeId = ShowtimeId,
                MovieTitle = showtime.Movie?.Title ?? "Chưa rõ",
                Showtime = showtime.StartTime?.ToString("dd/MM/yyyy HH:mm") ?? "Chưa rõ",
                Auditorium = showtime.Auditorium?.Name ?? "Chưa xác định",
                SelectedSeats = Seats?.ToList() ?? new List<string>(),
                AdultTickets = AdultTickets,
                ChildTickets = ChildTickets,
                StudentTickets = StudentTickets,
                TotalPrice = TotalPrice,
                Combos = combosVm
            };

            return View(vm);
        }

        [HttpPost]
        public IActionResult Confirm(PaymentViewModel model, string method)
        {
            _logger.LogInformation("[Confirm] Phương thức: {Method}", method);
            var customerId = HttpContext.Session.GetInt32("CustomerId");
            if (customerId == null) return RedirectToAction("Login", "Customer");

            using var transaction = _context.Database.BeginTransaction();
            try
            {
                // 🔹 1. Tính tổng tiền combo và vé NGAY TỪ ĐẦU
                decimal comboTotal = model.Combos?.Sum(c => c.Price * c.Quantity) ?? 0;
                decimal ticketOnlyTotal = model.TotalPrice - comboTotal;

                // 🔹 2. Tính giá vé mỗi ghế (KHÔNG cộng combo)
                decimal pricePerTicket = model.SelectedSeats.Count > 0
                    ? ticketOnlyTotal / model.SelectedSeats.Count
                    : 0;

                _logger.LogInformation($"🎫 Vé: {ticketOnlyTotal}, Combo: {comboTotal}, Mỗi ghế: {pricePerTicket}");

                // 🔹 3. Tạo Order (đặt sau khi tính đúng total)
                var order = new Order
                {
                    CustomerId = customerId.Value,
                    CreatedAt = DateTime.Now,
                    TotalAmount = ticketOnlyTotal + comboTotal,
                    Status = (method == "Chuyển khoản") ? "Đang chờ thanh toán" : "Chờ thanh toán",
                    PaymentMethod = method
                };
                _context.Orders.Add(order);
                _context.SaveChanges();

                // 🔹 4. Tạo vé cho từng ghế
                foreach (var seatStr in model.SelectedSeats)
                {
                    var seat = _context.Seats
                        .FirstOrDefault(s => (s.RowLabel + s.SeatNumber.ToString()) == seatStr);

                    if (seat == null)
                        throw new Exception($"Không tìm thấy ghế {seatStr} trong cơ sở dữ liệu.");

                    _context.Tickets.Add(new Ticket
                    {
                        ShowtimeId = model.ShowtimeId,
                        SeatId = seat.SeatId,
                        CustomerId = customerId.Value,
                        OrderId = order.OrderId,
                        Price = pricePerTicket, // ✅ chỉ giá vé, không cộng combo
                        Status = "Đã đặt",
                        PaymentStatus = (method == "Chuyển khoản") ? "Chờ thanh toán" : "Đang chờ thanh toán",
                        BookedAt = DateTime.Now
                    });
                }
                _context.SaveChanges();

                // 🔹 5. Lưu combo nếu có
                if (model.Combos != null && model.Combos.Count > 0)
                {
                    var orderCombos = model.Combos.Select(c => new OrderCombo
                    {
                        OrderId = order.OrderId,
                        ComboId = c.ComboId,
                        Quantity = c.Quantity,
                        UnitPrice = c.Price
                    }).ToList();

                    _context.OrderCombos.AddRange(orderCombos);
                    _context.SaveChanges();

                    int firstTicketId = _context.Tickets
                        .Where(t => t.OrderId == order.OrderId)
                        .OrderBy(t => t.TicketId)
                        .Select(t => t.TicketId)
                        .FirstOrDefault();

                    if (firstTicketId > 0)
                    {
                        var ticketCombos = orderCombos.Select(oc => new TicketCombo
                        {
                            TicketId = firstTicketId,
                            OrderComboId = oc.OrderComboId,
                            Quantity = oc.Quantity
                        }).ToList();

                        _context.TicketCombos.AddRange(ticketCombos);
                        _context.SaveChanges();
                    }
                }

                transaction.Commit();
                _logger.LogInformation("✅ Đơn #{OrderId} tạo thành công", order.OrderId);

                // 🔹 6. Chuyển hướng đến trang kết quả
                if (method == "Tại quầy")
                {
                    ViewBag.PaymentMethod = method;
                    ViewBag.PaymentStatus = "Chờ thanh toán";
                    ViewBag.Total = model.TotalPrice;
                    ViewBag.BookingCode = $"CZ{order.OrderId:D6}";
                    ViewBag.Message = "Vui lòng đến quầy để hoàn tất thanh toán.";
                    return View("Success", model);
                }

                if (method == "Chuyển khoản")
                {
                    var pay = new VnpayLibrary();
                    string baseUrl = _config["Vnpay:BaseUrl"];
                    string returnUrl = $"{Request.Scheme}://{Request.Host}{_config["Vnpay:ReturnUrl"]}";
                    string tmnCode = _config["Vnpay:TmnCode"];
                    string hashSecret = _config["Vnpay:HashSecret"];

                    pay.AddRequestData("vnp_Version", "2.1.0");
                    pay.AddRequestData("vnp_Command", "pay");
                    pay.AddRequestData("vnp_TmnCode", tmnCode);
                    pay.AddRequestData("vnp_Amount", ((long)model.TotalPrice * 100).ToString());
                    pay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
                    pay.AddRequestData("vnp_CurrCode", "VND");
                    pay.AddRequestData("vnp_IpAddr", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1");
                    pay.AddRequestData("vnp_Locale", "vn");
                    pay.AddRequestData("vnp_OrderInfo", $"Thanh toán đơn hàng #{order.OrderId}");
                    pay.AddRequestData("vnp_OrderType", "billpayment");
                    pay.AddRequestData("vnp_ReturnUrl", returnUrl);
                    pay.AddRequestData("vnp_TxnRef", order.OrderId.ToString());

                    string paymentUrl = pay.CreateRequestUrl(baseUrl, hashSecret);
                    _logger.LogInformation("➡️ Redirect đến VNPay: {Url}", paymentUrl);
                    return Redirect(paymentUrl);
                }

                ViewBag.ErrorMessage = "Phương thức thanh toán không hợp lệ.";
                return View("Index", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Lỗi khi xử lý thanh toán: {Message}", ex.InnerException?.Message);
                transaction.Rollback();
                return View("PaymentError");
            }
        }

        // =================== [3] Callback VNPay ===================
        [HttpGet]
        public IActionResult PaymentReturn()
        {
            _logger.LogInformation("📩 Nhận callback từ VNPay");
            string hashSecret = _config["Vnpay:HashSecret"];

            var pay = new VnpayLibrary();
            foreach (var key in Request.Query.Keys)
                if (key.StartsWith("vnp_"))
                    pay.AddResponseData(key, Request.Query[key]);

            string sOrderId = pay.GetResponseData("vnp_TxnRef");
            string responseCode = pay.GetResponseData("vnp_ResponseCode");
            string secureHash = pay.GetResponseData("vnp_SecureHash");
            string amountStr = pay.GetResponseData("vnp_Amount");

            if (!long.TryParse(sOrderId, out long orderId))
                return View("PaymentError", "Mã đơn hàng không hợp lệ.");

            bool validSignature = pay.ValidateSignature(secureHash, hashSecret);
            if (!validSignature)
                return View("PaymentError", "Chữ ký không hợp lệ.");

            var order = _context.Orders.Include(o => o.Tickets).FirstOrDefault(o => o.OrderId == orderId);
            if (order == null) return View("PaymentError", "Không tìm thấy đơn hàng.");

            if (responseCode == "00")
            {
                order.Status = "Đã thanh toán";
                foreach (var t in order.Tickets)
                {
                    t.Status = "Đã thanh toán";
                    t.PaymentStatus = "Đã thanh toán";
                }
                _context.SaveChanges();

                ViewBag.PaymentMethod = "Chuyển khoản";
                ViewBag.PaymentStatus = "Đã thanh toán";
                ViewBag.BookingCode = $"CZ{order.OrderId:D6}";
                ViewBag.Message = "Giao dịch thành công!";
                ViewBag.Total = decimal.TryParse(amountStr, out var amt) ? amt / 100m : order.TotalAmount;

                return View("Success", new PaymentViewModel { TotalPrice = ViewBag.Total });
            }

            order.Status = "Thanh toán thất bại";
            foreach (var t in order.Tickets)
                t.Status = "Thanh toán thất bại";
            _context.SaveChanges();

            ViewBag.Message = $"Giao dịch thất bại. Mã lỗi VNPAY: {responseCode}";
            return View("PaymentError");
        }

        // =================== [4] Khôi phục form thanh toán sau login ===================
        [HttpGet]
        public IActionResult ResumePayment()
        {
            var customerId = HttpContext.Session.GetInt32("CustomerId");
            if (customerId == null) return RedirectToAction("Login", "Customer");

            var customer = _context.Customers.Find(customerId);
            if (customer == null)
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login", "Customer", new { message = "Tài khoản không tồn tại." });
            }

            if (TempData["MovieId"] == null) return RedirectToAction("Index", "Home");

            int movieId = (int)TempData["MovieId"];
            int showtimeId = (int)TempData["ShowtimeId"];
            var seats = JsonSerializer.Deserialize<string[]>((string)TempData["Seats"] ?? "[]");
            int adult = (int)TempData["AdultTickets"];
            int child = (int)TempData["ChildTickets"];
            int student = (int)TempData["StudentTickets"];
            decimal total = decimal.Parse((string)TempData["TotalPrice"], CultureInfo.InvariantCulture);

            TempData.Keep();

            var combos = new List<ComboViewModel>();
            if (TempData["Combos"] != null)
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>((string)TempData["Combos"]);
                foreach (var kv in dict)
                {
                    if (int.TryParse(kv.Key.Replace("Combo_", ""), out int comboId) &&
                        int.TryParse(kv.Value, out int qty) && qty > 0)
                    {
                        var combo = _context.Combos.FirstOrDefault(c => c.ComboId == comboId);
                        if (combo != null)
                        {
                            combos.Add(new ComboViewModel
                            {
                                ComboId = combo.ComboId,
                                ComboName = combo.Name,
                                Quantity = qty,
                                Price = combo.Price ?? 0
                            });
                        }
                    }
                }
            }

            var showtime = _context.Showtimes
                .Include(s => s.Auditorium)
                .Include(s => s.Movie)
                .FirstOrDefault(s => s.ShowtimeId == showtimeId);
            if (showtime == null) return NotFound();

            var vm = new PaymentViewModel
            {
                CustomerName = customer.FullName,
                CustomerEmail = customer.Email,
                CustomerPhone = customer.Phone,
                MovieId = movieId,
                ShowtimeId = showtimeId,
                MovieTitle = showtime.Movie?.Title ?? "Chưa rõ",
                Showtime = showtime.StartTime?.ToString("dd/MM/yyyy HH:mm") ?? "Chưa rõ",
                Auditorium = showtime.Auditorium?.Name ?? "Chưa xác định",
                SelectedSeats = seats?.ToList() ?? new List<string>(),
                AdultTickets = adult,
                ChildTickets = child,
                StudentTickets = student,
                TotalPrice = total,
                Combos = combos
            };

            return View("Index", vm);
        }
    }
}
