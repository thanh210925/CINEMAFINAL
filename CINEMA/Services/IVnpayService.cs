using CINEMA.Helpers;
using CINEMA.Models;
using CINEMA.Helpers;

namespace CINEMA.Services
{
    public interface IVnpayService
    {
        string CreatePaymentUrl(Order model, HttpContext context);
    }
    public class VnpayService : IVnpayService
    {
        private readonly IConfiguration _config;

        public VnpayService(IConfiguration config)
        {
            _config = config;
        }

        public string CreatePaymentUrl(Order order, HttpContext context)
        {
            var timeZoneById = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            var timeNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZoneById);

            // Lấy các thông số từ appsettings.json
            var pay = new VnpayLibrary();
            var urlCallBack = _config["Vnpay:ReturnUrl"];
            var tmnCode = _config["Vnpay:TmnCode"];
            var hashSecret = _config["Vnpay:HashSecret"];

            // Thêm các tham số cần thiết vào VnpayLibrary
            pay.AddRequestData("vnp_Version", "2.1.0");
            pay.AddRequestData("vnp_Command", "pay");
            pay.AddRequestData("vnp_TmnCode", tmnCode);
            pay.AddRequestData("vnp_Amount", ((long)order.TotalAmount * 100).ToString()); // Số tiền * 100 vì VNPay yêu cầu đơn vị là đồng
            pay.AddRequestData("vnp_CreateDate", timeNow.ToString("yyyyMMddHHmmss"));
            pay.AddRequestData("vnp_CurrCode", "VND");
            pay.AddRequestData("vnp_IpAddr", pay.GetIpAddress(context));
            pay.AddRequestData("vnp_Locale", "vn");
            pay.AddRequestData("vnp_OrderInfo", $"Thanh toan don hang {order.OrderId}");
            pay.AddRequestData("vnp_OrderType", "other");
            pay.AddRequestData("vnp_ReturnUrl", urlCallBack);
            pay.AddRequestData("vnp_TxnRef", order.OrderId.ToString()); // Mã tham chiếu của giao dịch. Chính là Mã đơn hàng

            // Tạo URL thanh toán
            var paymentUrl = pay.CreateRequestUrl(_config["Vnpay:BaseUrl"], hashSecret);

            return paymentUrl;
        }
    }
}