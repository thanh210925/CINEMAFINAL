using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using CINEMA.Models;
using CINEMA.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.AspNetCore.Http;               // <-- Thêm using này
using Microsoft.Extensions.Configuration;     // <-- Thêm using này
using Microsoft.Extensions.Logging;         // <-- Thêm using này
using CINEMA.Helpers;

// TODO: THAY THẾ using này bằng using thư viện VNPAY bạn đã cài đặt
// Ví dụ: using VnpaySdk;
// Hoặc tên namespace tương ứng với thư viện bạn chọn

namespace CINEMA.Controllers
{
    public class PaymentController : Controller
    {
        private readonly CinemaContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PaymentController> _logger; // <-- Khai báo logger

        // Sửa Constructor để nhận IConfiguration và ILogger
        public PaymentController(CinemaContext context, IConfiguration configuration, ILogger<PaymentController> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger; // <-- Gán logger
        }

        // 🟢 Trang Payment, yêu cầu đăng nhập
        [HttpPost]
        public IActionResult Index(
            int MovieId,
            int ShowtimeId,
            string[] Seats,
            int AdultTickets,
            int ChildTickets,
            int StudentTickets,
            decimal TotalPrice)
        {
            var customerId = HttpContext.Session.GetInt32("CustomerId");
            if (customerId == null)
            {
                // ... (code lưu TempData và redirect sang Login, giữ nguyên) ...
                TempData["MovieId"] = MovieId;
                TempData["ShowtimeId"] = ShowtimeId;
                TempData["Seats"] = JsonSerializer.Serialize(Seats);
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

            var customer = _context.Customers.Find(customerId.Value);
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

            var viewModel = new PaymentViewModel
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
                Combos = new List<ComboViewModel>()
            };

            foreach (var key in Request.Form.Keys.Where(k => k.StartsWith("Combo_")))
            {
                int orderComboId = int.Parse(key.Replace("Combo_", ""));
                int qty = int.Parse(Request.Form[key].ToString());
                if (qty > 0)
                {
                    var combo = _context.OrderCombos.Include(c => c.Combo).FirstOrDefault(c => c.OrderComboId == orderComboId);
                    if (combo != null)
                    {
                        viewModel.Combos.Add(new ComboViewModel
                        {
                            OrderComboId = orderComboId,
                            ComboId = combo.ComboId ?? 0,
                            ComboName = combo.Combo?.Name ?? "Combo",
                            Quantity = qty,
                            Price = combo.UnitPrice ?? 0
                        });
                    }
                }
            }
            return View(viewModel);
        }

        // 🟢 Xác nhận thanh toán: Lưu DB và Chuyển hướng nếu cần
        [HttpPost]
        public IActionResult Confirm(PaymentViewModel model, string method)
        {
            Console.WriteLine($"[DEBUG] Method nhận được: '{method}'"); // 🟢 Thêm dòng này để debug
            var customerId = HttpContext.Session.GetInt32("CustomerId");
            if (customerId == null) return RedirectToAction("Login", "Customer");

            List<Ticket> createdTickets = new List<Ticket>();
            int firstTicketId = 0; // Sẽ dùng làm mã tham chiếu (TxnRef) cho VNPAY

            using (var transaction = _context.Database.BeginTransaction())
            {
                try
                {
                    decimal pricePerTicket = (model.SelectedSeats.Count > 0) ? model.TotalPrice / model.SelectedSeats.Count : 0;
                    string initialStatus = "Chờ thanh toán"; // Luôn bắt đầu là chờ

                    foreach (var seatStr in model.SelectedSeats)
                    {
                        // Ghế truyền từ view thường có dạng "C4" (hàng C, số 4)
                        // nên ta phải tìm SeatId tương ứng trong database, không thể int.Parse
                        var seat = _context.Seats
                            .FirstOrDefault(s => (s.RowLabel + s.SeatNumber.ToString()) == seatStr);

                        if (seat == null)
                        {
                            throw new Exception($"Không tìm thấy ghế {seatStr} trong cơ sở dữ liệu!");
                        }

                        var ticket = new Ticket
                        {
                            CustomerId = customerId.Value,
                            ShowtimeId = model.ShowtimeId,
                            SeatId = seat.SeatId,              // ✅ Dùng SeatId thật trong DB
                            Price = pricePerTicket,
                            Status = initialStatus,
                            PaymentStatus = method,
                            BookedAt = DateTime.Now
                        };
                        _context.Tickets.Add(ticket);
                        createdTickets.Add(ticket);
                    }


                    _context.SaveChanges(); // Lưu vé để lấy ID

                    if (createdTickets.Any()) firstTicketId = createdTickets[0].TicketId; // Lấy ID vé đầu tiên

                    if (model.Combos != null && model.Combos.Count > 0 && firstTicketId > 0)
                    {
                        var ticketCombos = new List<TicketCombo>();
                        foreach (var combo in model.Combos)
                        {
                            ticketCombos.Add(new TicketCombo
                            {
                                TicketId = firstTicketId, // Gắn vào vé đầu tiên
                                OrderComboId = combo.OrderComboId,
                                Quantity = combo.Quantity
                            });
                        }
                        _context.TicketCombos.AddRange(ticketCombos);
                        _context.SaveChanges();
                    }

                    transaction.Commit(); // Hoàn tất lưu CSDL

                    // Xóa TempData sau khi commit thành công
                    TempData.Remove("MovieId");
                    TempData.Remove("ShowtimeId");
                    TempData.Remove("Seats");
                    TempData.Remove("AdultTickets");
                    TempData.Remove("ChildTickets");
                    TempData.Remove("StudentTickets");
                    TempData.Remove("TotalPrice");
                    TempData.Remove("Combos");
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    _logger.LogError(ex, "Lỗi khi lưu vé/combo vào CSDL cho đơn hàng thử {OrderId}", firstTicketId);
                    ViewBag.ErrorMessage = "Đã có lỗi xảy ra khi tạo vé. Vui lòng thử lại.";
                    // Có thể trả về View("Index", model) với ViewBag.ErrorMessage
                    return RedirectToAction("Index", "Home"); // Hoặc trang lỗi chung
                }
            }

            // --- Xử lý dựa trên phương thức thanh toán ---

            if (method == "Tại quầy")
            {
                _logger.LogInformation("Đơn hàng {OrderId} chọn thanh toán tại quầy.", firstTicketId);
                // Chuyển đến trang Success với trạng thái chờ
                model.PaymentMethod = method; // Gán lại để View Success hiển thị đúng
                ViewBag.PaymentMethod = method;
                ViewBag.PaymentStatus = "Chờ thanh toán";
                ViewBag.Total = model.TotalPrice;
                ViewBag.BookingCode = firstTicketId > 0 ? $"CZ{firstTicketId:D6}" : "N/A";
                ViewBag.Message = "Vui lòng đến quầy để hoàn tất thanh toán.";

                return View("Success", model);
            }
            else if (method == "Chuyển khoản")
            {
                _logger.LogInformation("Đơn hàng {OrderId} chọn thanh toán chuyển khoản VNPAY.", firstTicketId);
                try
                {
                    string vnp_Url = _configuration["Vnpay:Url"];
                    string vnp_Returnurl = _configuration["Vnpay:ReturnUrl"];
                    string fullReturnUrl = $"{Request.Scheme}://{Request.Host}{vnp_Returnurl}";
                    string vnp_TmnCode = _configuration["Vnpay:TmnCode"];
                    string vnp_HashSecret = _configuration["Vnpay:HashSecret"];

                    // TODO: Thay thế PayLib bằng class từ thư viện VNPAY thực tế
                    // PayLib pay = new PayLib();
                    var pay = new VnpayLibrary(); // Ví dụ tên class thư viện

                    pay.AddRequestData("vnp_Version", "2.1.0");
                    pay.AddRequestData("vnp_Command", "pay");
                    pay.AddRequestData("vnp_TmnCode", vnp_TmnCode);
                    pay.AddRequestData("vnp_Amount", ((long)model.TotalPrice * 100).ToString());
                    pay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
                    pay.AddRequestData("vnp_CurrCode", "VND");
                    pay.AddRequestData("vnp_IpAddr", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1");
                    pay.AddRequestData("vnp_Locale", "vn");
                    pay.AddRequestData("vnp_OrderInfo", $"Thanh toan don hang {firstTicketId}"); // Thông tin hiển thị trên VNPAY
                    pay.AddRequestData("vnp_OrderType", "other"); // Hoặc mã loại hàng hóa phù hợp
                    pay.AddRequestData("vnp_ReturnUrl", fullReturnUrl);
                    pay.AddRequestData("vnp_TxnRef", firstTicketId.ToString()); // Mã đơn hàng của bạn

                    string paymentUrl = pay.CreateRequestUrl(vnp_Url, vnp_HashSecret);
                    _logger.LogInformation("Tạo URL VNPAY thành công cho Order ID {OrderId}. Đang chuyển hướng...", firstTicketId);

                    return Redirect(paymentUrl);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi tạo URL VNPAY cho Order ID {OrderId}", firstTicketId);
                    ViewBag.ErrorMessage = "Không thể tạo yêu cầu thanh toán. Vui lòng thử lại hoặc chọn phương thức khác.";
                    // Cần load lại ViewModel và trả về View("Index", model);
                    return View("Index", model); // Quay lại trang thanh toán với lỗi
                }
            }
            else
            {
                _logger.LogWarning("Phương thức thanh toán không hợp lệ '{Method}' được chọn cho Order ID thử {OrderId}", method, firstTicketId);
                ViewBag.ErrorMessage = "Phương thức thanh toán không hợp lệ.";
                // Cần load lại ViewModel và trả về View("Index", model);
                return View("Index", model); // Quay lại trang thanh toán với lỗi
            }
        }

        // 🟢 Action mới: Xử lý khi VNPAY trả về (Callback)
        [HttpGet]
        public IActionResult PaymentReturn()
        {
            _logger.LogInformation("Nhận Payment Return từ VNPAY.");
            string vnp_HashSecret = _configuration["Vnpay:HashSecret"];
            var vnpayData = HttpContext.Request.Query;

            // TODO: Thay thế PayLib bằng class từ thư viện VNPAY thực tế
            // PayLib pay = new PayLib();
            var pay = new VnpayLibrary(); // Ví dụ tên class thư viện

            // Đưa dữ liệu query string vào đối tượng pay để xử lý
            foreach (string s in vnpayData.Keys)
            {
                if (!string.IsNullOrEmpty(s) && s.StartsWith("vnp_"))
                {
                    pay.AddResponseData(s, vnpayData[s]);
                }
            }

            long orderId = Convert.ToInt64(pay.GetResponseData("vnp_TxnRef")); // Lấy lại mã đơn hàng
            string vnp_ResponseCode = pay.GetResponseData("vnp_ResponseCode"); // Mã kết quả
            string vnp_SecureHash = pay.GetResponseData("vnp_SecureHash"); // Chuỗi hash VNPAY gửi về
            string vnp_Amount = pay.GetResponseData("vnp_Amount"); // Số tiền * 100

            // TODO: Sử dụng phương thức ValidateSignature của thư viện VNPAY thực tế
            bool checkSignature = pay.ValidateSignature(vnp_SecureHash, vnp_HashSecret);

            if (checkSignature)
            {
                _logger.LogInformation("Chữ ký VNPAY hợp lệ cho Order ID: {OrderId}", orderId);
                if (vnp_ResponseCode == "00") // Mã 00 là thành công
                {
                    _logger.LogInformation("Thanh toán VNPAY THÀNH CÔNG cho Order ID: {OrderId}", orderId);

                    // Tìm vé đầu tiên của đơn hàng
                    var firstTicket = _context.Tickets.FirstOrDefault(t => t.TicketId == orderId);
                    bool updated = false;

                    if (firstTicket != null && firstTicket.Status == "Chờ thanh toán")
                    {
                        // Tìm tất cả vé và combo liên quan đến vé đầu tiên
                        var ticketsToUpdate = _context.Tickets
                                                 .Where(t => t.TicketId == orderId
                                                          || _context.TicketCombos.Any(tc => tc.TicketId == orderId && tc.TicketId == t.TicketId))
                                                 .ToList();

                        foreach (var ticket in ticketsToUpdate)
                        {
                            ticket.Status = "Đã thanh toán"; // Cập nhật trạng thái
                        }
                        try
                        {
                            _context.SaveChanges();
                            updated = true;
                            _logger.LogInformation("CSDL đã cập nhật 'Đã thanh toán' cho Order ID: {OrderId}", orderId);
                            // TODO: Gửi email xác nhận đơn hàng cho khách tại đây
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Lỗi khi cập nhật trạng thái vé cho Order ID: {OrderId}", orderId);
                            // Xử lý lỗi DB, có thể cần thông báo cho admin
                        }
                    }
                    else if (firstTicket != null && firstTicket.Status == "Đã thanh toán")
                    {
                        _logger.LogWarning("Order ID: {OrderId} đã được đánh dấu 'Đã thanh toán'. Có thể đã xử lý bởi IPN.", orderId);
                        updated = true; // Vẫn coi là thành công để hiển thị
                    }
                    else if (firstTicket == null)
                    {
                        _logger.LogError("Order ID: {OrderId} không tìm thấy trong CSDL khi PaymentReturn.", orderId);
                    }

                    if (updated)
                    {
                        ViewBag.PaymentMethod = "Chuyển khoản";
                        ViewBag.PaymentStatus = "Đã thanh toán";
                        ViewBag.BookingCode = $"CZ{orderId:D6}";
                        ViewBag.Message = "Giao dịch được thực hiện thành công!";
                        ViewBag.Total = Convert.ToDecimal(vnp_Amount) / 100; // Chia lại cho 100

                        // Tạo ViewModel tối thiểu cho trang Success nếu cần
                        var successViewModel = new PaymentViewModel { TotalPrice = ViewBag.Total };
                        return View("Success", successViewModel);
                    }
                    else
                    {
                        ViewBag.Message = "Không tìm thấy đơn hàng hoặc có lỗi khi cập nhật trạng thái.";
                        return View("PaymentError"); // Cần tạo View này: Views/Payment/PaymentError.cshtml
                    }
                }
                else // Thanh toán thất bại từ VNPAY (vnp_ResponseCode != "00")
                {
                    _logger.LogError("Thanh toán VNPAY THẤT BẠI cho Order ID: {OrderId}. Mã lỗi: {ResponseCode}", orderId, vnp_ResponseCode);
                    // Có thể cần cập nhật trạng thái vé thành "Thanh toán thất bại" nếu muốn
                    // var ticketsToUpdate = _context.Tickets.Where(...);
                    // foreach (var ticket in ticketsToUpdate) { ticket.Status = "Thanh toán thất bại"; }
                    // _context.SaveChanges();
                    ViewBag.Message = $"Giao dịch không thành công. Mã lỗi VNPAY: {vnp_ResponseCode}";
                    return View("PaymentError"); // Cần tạo View này: Views/Payment/PaymentError.cshtml
                }
            }
            else // Chữ ký không hợp lệ
            {
                _logger.LogError("CHỮ KÝ VNPAY KHÔNG HỢP LỆ cho Order ID thử: {OrderId}", orderId);
                ViewBag.Message = "Có lỗi xảy ra trong quá trình xử lý: Chữ ký không hợp lệ.";
                return View("PaymentError"); // Cần tạo View này: Views/Payment/PaymentError.cshtml
            }
        }

        // 🟢 Khởi tạo dữ liệu Payment sau login nếu có TempData
        [HttpGet]
        public IActionResult ResumePayment() // <-- Đã thêm lại method này
        {
            var customerId = HttpContext.Session.GetInt32("CustomerId");
            if (customerId == null) return RedirectToAction("Login", "Customer");

            var customer = _context.Customers.Find(customerId.Value);
            if (customer == null)
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login", "Customer", new { message = "Tài khoản không tồn tại." });
            }

            if (TempData["MovieId"] == null) return RedirectToAction("Index", "Home");

            int movieId = (int)TempData["MovieId"];
            int showtimeId = (int)TempData["ShowtimeId"];
            var seats = JsonSerializer.Deserialize<string[]>((string)TempData["Seats"]);
            int adult = (int)TempData["AdultTickets"];
            int child = (int)TempData["ChildTickets"];
            int student = (int)TempData["StudentTickets"];
            decimal total = decimal.Parse((string)TempData["TotalPrice"], CultureInfo.InvariantCulture);

            TempData.Keep("MovieId");
            TempData.Keep("ShowtimeId");
            TempData.Keep("Seats");
            TempData.Keep("AdultTickets");
            TempData.Keep("ChildTickets");
            TempData.Keep("StudentTickets");
            TempData.Keep("TotalPrice");
            TempData.Keep("Combos");

            List<ComboViewModel> combos = new List<ComboViewModel>();
            if (TempData["Combos"] != null)
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>((string)TempData["Combos"]);
                foreach (var kv in dict)
                {
                    int orderComboId = int.Parse(kv.Key.Replace("Combo_", ""));
                    int qty = int.Parse(kv.Value);
                    if (qty > 0)
                    {
                        var combo = _context.OrderCombos.Include(c => c.Combo).FirstOrDefault(c => c.OrderComboId == orderComboId);
                        if (combo != null)
                        {
                            combos.Add(new ComboViewModel { /* ... */ });
                        }
                    }
                }
            }

            var showtime = _context.Showtimes.Include(s => s.Auditorium).Include(s => s.Movie).FirstOrDefault(s => s.ShowtimeId == showtimeId);
            if (showtime == null) return NotFound();

            var viewModel = new PaymentViewModel
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

            return View("Index", viewModel);
        }
    }

    
    
}