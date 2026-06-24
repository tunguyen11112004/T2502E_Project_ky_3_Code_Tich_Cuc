using Bus_ticket.Models;
using Microsoft.AspNetCore.Mvc;

namespace Bus_ticket.Controllers;

public class NewsController : Controller
{
    private static readonly List<NewsArticle> Articles = new()
    {
        new NewsArticle
        {
            Slug = "src-vietnam-x-be-uu-dai-vang",
            Title = "SRC Vietnam X Be: Đi xe sang nhận ưu đãi vàng hấp dẫn",
            Summary = "SRC Travel triển khai chương trình ưu đãi dành cho khách hàng đặt vé tuyến liên tỉnh, giúp hành khách tiết kiệm chi phí nhưng vẫn giữ trải nghiệm xe chất lượng cao.",
            Category = "Ưu đãi",
            ImageUrl = "https://images.unsplash.com/photo-1544620347-c4fd4a3d5957?auto=format&fit=crop&q=80&w=1200",
            PublishedAt = new DateTime(2026, 6, 20),
            ReadingMinutes = 4,
            Highlights = new List<string>
            {
                "Áp dụng cho một số tuyến cố định trong hệ thống SRC Travel.",
                "Khách hàng có thể đặt vé qua tổng đài, website hoặc quầy vé.",
                "Số lượng ưu đãi có giới hạn theo từng khung giờ xuất phát."
            },
            Paragraphs = new List<string>
            {
                "Nhằm tri ân khách hàng đã đồng hành cùng SRC Travel, chương trình ưu đãi mới được triển khai cho các tuyến xe liên tỉnh có nhu cầu di chuyển cao. Khi đặt vé trong thời gian áp dụng, hành khách có cơ hội nhận mức giảm giá trực tiếp trên giá vé hoặc ưu đãi đi kèm tùy từng tuyến.",
                "Chương trình được thiết kế để giúp khách hàng dễ dàng tiếp cận dịch vụ vận tải chất lượng cao với chi phí hợp lý hơn. Các dòng xe được khai thác vẫn đảm bảo tiêu chuẩn về ghế ngồi, điều hòa, wifi, nước uống và đội ngũ phục vụ chuyên nghiệp.",
                "Để không bỏ lỡ ưu đãi, hành khách nên kiểm tra lịch xe sớm, lựa chọn khung giờ phù hợp và hoàn tất đặt vé trước ngày khởi hành. Bộ phận chăm sóc khách hàng của SRC Travel luôn sẵn sàng hỗ trợ khi khách cần thay đổi thông tin chuyến đi."
            }
        },
        new NewsArticle
        {
            Slug = "lich-van-tai-nghi-le-gio-to-hung-vuong",
            Title = "Thông báo lịch vận tải hành khách dịp nghỉ lễ Giỗ tổ Hùng Vương",
            Summary = "SRC Travel tăng cường chuyến xe trong dịp nghỉ lễ để phục vụ nhu cầu di chuyển tăng cao của hành khách trên các tuyến trọng điểm.",
            Category = "Thông báo",
            ImageUrl = "https://images.unsplash.com/photo-1501183007986-d0d080b147f9?auto=format&fit=crop&q=80&w=1200",
            PublishedAt = new DateTime(2026, 6, 18),
            ReadingMinutes = 5,
            Highlights = new List<string>
            {
                "Tăng cường chuyến vào các khung giờ cao điểm.",
                "Khuyến khích hành khách đặt vé sớm để giữ chỗ.",
                "Có hỗ trợ đổi thông tin vé theo quy định của nhà xe."
            },
            Paragraphs = new List<string>
            {
                "Trong thời gian nghỉ lễ, nhu cầu di chuyển giữa các thành phố lớn và các điểm du lịch thường tăng mạnh. SRC Travel chủ động tăng cường thêm chuyến, điều phối xe dự phòng và mở rộng khung giờ phục vụ để hạn chế tình trạng hết vé cục bộ.",
                "Hành khách nên đến điểm đón trước giờ xuất phát tối thiểu 20 đến 30 phút để hoàn tất kiểm tra thông tin vé và sắp xếp hành lý. Với các tuyến đường dài, khách hàng nên chuẩn bị giấy tờ cá nhân, kiểm tra lại số ghế và giữ điện thoại liên lạc trong suốt quá trình di chuyển.",
                "SRC Travel cũng khuyến nghị khách hàng đặt vé qua các kênh chính thức để được đảm bảo quyền lợi, hạn chế rủi ro phát sinh từ vé không hợp lệ hoặc thông tin chuyến đi không chính xác."
            }
        },
        new NewsArticle
        {
            Slug = "mo-rong-tuyen-da-nang-quy-nhon",
            Title = "SRC Travel mở rộng tuyến Đà Nẵng - Quy Nhơn với nhiều khung giờ mới",
            Summary = "Tuyến Đà Nẵng - Quy Nhơn được bổ sung thêm khung giờ xuất phát nhằm đáp ứng nhu cầu đi lại, du lịch và công tác của hành khách miền Trung.",
            Category = "Tuyến mới",
            ImageUrl = "https://quynhontourist.vn/wp-content/uploads/2017/04/thanh-pho-quy-nhon-tuyet-dep.jpg",
            PublishedAt = new DateTime(2026, 6, 15),
            ReadingMinutes = 3,
            Highlights = new List<string>
            {
                "Bổ sung thêm chuyến sáng và chuyến tối.",
                "Phù hợp với khách du lịch, sinh viên và người đi công tác.",
                "Hỗ trợ đặt vé nhanh qua hotline và website."
            },
            Paragraphs = new List<string>
            {
                "Tuyến Đà Nẵng - Quy Nhơn là một trong những tuyến có lượng khách tăng ổn định trong thời gian gần đây. Việc bổ sung khung giờ mới giúp hành khách linh hoạt hơn trong việc lựa chọn thời gian xuất phát, đặc biệt vào cuối tuần và mùa du lịch.",
                "Các chuyến xe trên tuyến được bố trí dòng xe tiện nghi, khoang ngồi thoải mái, hệ thống điều hòa ổn định và đội ngũ tài xế có kinh nghiệm đường dài. SRC Travel đặt mục tiêu mang lại hành trình an toàn, đúng giờ và dễ đặt vé cho khách hàng.",
                "Khách hàng có thể theo dõi bảng giá, lịch chạy và tình trạng ghế trống trên hệ thống hoặc liên hệ trực tiếp tổng đài để được tư vấn tuyến phù hợp."
            }
        },
        new NewsArticle
        {
            Slug = "nang-cap-doi-xe-limousine",
            Title = "Nâng cấp đội xe Limousine phục vụ hành trình đường dài",
            Summary = "SRC Travel tiếp tục đầu tư nâng cấp phương tiện nhằm cải thiện sự thoải mái, an toàn và trải nghiệm tổng thể của hành khách.",
            Category = "Dịch vụ",
            ImageUrl = "https://vielimousine.com/wp-content/uploads/2021/12/DSC6090.jpg",
            PublishedAt = new DateTime(2026, 6, 10),
            ReadingMinutes = 4,
            Highlights = new List<string>
            {
                "Tăng cường kiểm tra kỹ thuật trước mỗi chuyến.",
                "Cải thiện tiện nghi bên trong xe.",
                "Đào tạo lại quy trình phục vụ hành khách."
            },
            Paragraphs = new List<string>
            {
                "Chất lượng phương tiện là một trong những yếu tố quan trọng nhất đối với dịch vụ vận tải hành khách. SRC Travel đã tiến hành rà soát, bảo dưỡng và nâng cấp một số xe Limousine đang khai thác trên các tuyến đường dài.",
                "Các hạng mục nâng cấp tập trung vào sự thoải mái của hành khách như ghế ngồi, rèm che, hệ thống điều hòa, cổng sạc và khoang hành lý. Bên cạnh đó, đội ngũ vận hành cũng tăng tần suất kiểm tra kỹ thuật để đảm bảo xe luôn trong trạng thái sẵn sàng.",
                "SRC Travel mong muốn mỗi chuyến đi không chỉ là việc di chuyển từ điểm A đến điểm B, mà còn là một trải nghiệm an toàn, tiện lợi và đáng tin cậy cho khách hàng."
            }
        },
        new NewsArticle
        {
            Slug = "tuyen-dung-lai-xe-toan-quoc",
            Title = "Tuyển lái xe toàn quốc: Thu nhập hấp dẫn, môi trường chuyên nghiệp",
            Summary = "SRC Travel tuyển dụng lái xe cho nhiều tuyến vận tải hành khách, ưu tiên ứng viên có kinh nghiệm, thái độ phục vụ tốt và tinh thần trách nhiệm cao.",
            Category = "Tuyển dụng",
            ImageUrl = "https://images.unsplash.com/photo-1557425955-df376b5903c8?auto=format&fit=crop&q=80&w=1200",
            PublishedAt = new DateTime(2026, 6, 8),
            ReadingMinutes = 3,
            Highlights = new List<string>
            {
                "Thu nhập cạnh tranh theo năng lực và tuyến khai thác.",
                "Được đào tạo quy trình phục vụ và an toàn vận hành.",
                "Ưu tiên ứng viên có bằng lái phù hợp và kinh nghiệm đường dài."
            },
            Paragraphs = new List<string>
            {
                "Để đáp ứng nhu cầu mở rộng mạng lưới vận tải, SRC Travel tuyển dụng thêm lái xe cho các tuyến liên tỉnh và tuyến du lịch. Ứng viên cần có bằng lái phù hợp, sức khỏe tốt, lịch sử lái xe an toàn và thái độ phục vụ chuyên nghiệp.",
                "Nhân sự trúng tuyển sẽ được hướng dẫn quy trình vận hành, quy định chăm sóc hành khách, tiêu chuẩn an toàn và cách xử lý các tình huống phát sinh trong chuyến đi. Công ty cam kết xây dựng môi trường làm việc rõ ràng, ổn định và có cơ hội phát triển lâu dài.",
                "Ứng viên quan tâm có thể liên hệ bộ phận tuyển dụng qua hotline 090.606.1999 hoặc gửi hồ sơ về email tuyendung@src.vn để được hỗ trợ chi tiết."
            }
        }
    };

    public IActionResult Index()
    {
        var articles = Articles
            .OrderByDescending(article => article.PublishedAt)
            .ToList();

        return View(articles);
    }

    public IActionResult Details(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return RedirectToAction(nameof(Index));
        }

        var article = Articles.FirstOrDefault(item => item.Slug.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (article == null)
        {
            return NotFound();
        }

        ViewBag.RelatedArticles = Articles
            .Where(item => item.Slug != article.Slug)
            .OrderByDescending(item => item.PublishedAt)
            .Take(3)
            .ToList();

        return View(article);
    }
}
