using Masterpiece.DTO;
using Masterpiece.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Masterpiece.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SaleRequestsController : ControllerBase
    {
        private readonly IEmailService _emailService;
        private readonly MyDbContext _db;

        public SaleRequestsController(MyDbContext db, IEmailService emailService)
        {
            _db = db;
            _emailService = emailService;
        }


        [HttpPost("submit-sale-request")]
        public async Task<IActionResult> SubmitSaleRequest([FromForm] SaleRequestDto requestDto)
        {
            // التحقق من صحة النموذج
            if (!ModelState.IsValid)
            {
                return BadRequest(new { message = "بيانات غير صالحة", errors = ModelState });
            }

            // تحقق من وجود الفئة الفرعية
            var subcategory = await _db.Subcategories
                .Include(sc => sc.Category)  // تأكد من تضمين الفئة الرئيسية
                .FirstOrDefaultAsync(sc => sc.SubcategoryId == requestDto.SubcategoryId);

            if (subcategory == null || subcategory.CategoryId != requestDto.MainCategoryId)
            {
                return BadRequest(new { message = "الفئة الفرعية المختارة لا تتبع الفئة الرئيسية المحددة." });
            }

            // إنشاء كائن المنتج
            var product = new Product
            {
                Name = requestDto.ProductName,
                Description = requestDto.Description,
                Color = requestDto.Color,
                SubcategoryId = requestDto.SubcategoryId,
                Condition = requestDto.Condition,

                IsDonation = requestDto.ActionType == "donation", // إذا كان تبرع، يتم تعيين هذا الحقل
            };

            // إذا كانت العملية تبرع (donation) يتم تعيين السعر إلى 0
            if (requestDto.ActionType == "donation")
            {
                product.Price = 0; // لا يوجد سعر في حالة التبرع
            }
            else
            {
                product.Price = requestDto.ExpectedPrice; // يتم استخدام السعر في حالة البيع
            }

            // تحديد مسار تحميل الصور
            var imagesFolder = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
            if (!Directory.Exists(imagesFolder))
            {
                Directory.CreateDirectory(imagesFolder);
            }

            // معالجة الصورة
            if (requestDto.ProductImage != null && requestDto.ProductImage.Length > 0)
            {
                var fileExtension = Path.GetExtension(requestDto.ProductImage.FileName);
                var uniqueFileName = Guid.NewGuid().ToString() + fileExtension; // اسم فريد للصورة
                var imageFilePath = Path.Combine(imagesFolder, uniqueFileName);
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                if (!allowedExtensions.Contains(fileExtension.ToLower()))
                {
                    return BadRequest(new { message = "الامتداد غير مدعوم للصور." });
                }
                using (var stream = new FileStream(imageFilePath, FileMode.Create))
                {
                    await requestDto.ProductImage.CopyToAsync(stream);  // استخدام await هنا
                }

                product.Image = uniqueFileName; // حفظ اسم الملف الفريد في كائن المنتج
            }
            if (product.IsActive == null)
{
    product.IsActive = false;  // تعيين قيمة افتراضية في حال كان null
}

            // إضافة المنتج أولاً
            _db.Products.Add(product);
            try
            {
                await _db.SaveChangesAsync();  // حفظ المنتج في قاعدة البيانات
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "حدث خطأ أثناء حفظ المنتج.", error = ex.Message });
            }

            // إنشاء طلب البيع
            var saleRequest = new SaleRequest
            {
                UserId = requestDto.UserId,
                ProductId = product.ProductId,  // تأكد من ربط ProductId
                RequestedPrice = requestDto.ExpectedPrice, // سيتم استخدامه فقط في حالة البيع
                RequestDate = DateOnly.FromDateTime(DateTime.Now),
                Status = "Pending"
            };
    
            // إضافة الطلب
            _db.SaleRequests.Add(saleRequest);
            try
            {
                await _db.SaveChangesAsync();  // حفظ الطلب في قاعدة البيانات
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "حدث خطأ أثناء إضافة الطلب.", error = ex.Message });
            }

            return Ok(new { message = "تم تقديم الطلب بنجاح! ستتلقى بريدًا إلكترونيًا بمجرد الموافقة أو الرفض.", saleRequestId = saleRequest.RequestId });
        }





        // GET: api/salerequests
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetAllSaleRequests()
        {
            var saleRequests = await _db.SaleRequests
                .Include(s => s.User)  // تضمين بيانات المستخدم
                .Select(s => new
                {
                    s.RequestId,  // رقم الطلب
                    s.RequestedPrice,  // السعر المطلوب
                    s.RequestDate,  // تاريخ الطلب
                    s.Status,  // حالة الطلب
                    UserName = s.User.UserName,  // اسم المستخدم
                    UserEmail = s.User.Email,  // البريد الإلكتروني للمستخدم
                    UserPhone = s.User.PhoneNumber,  // رقم الهاتف
                    UserAddress = s.User.Address,  // العنوان
                    ProductName = s.Product.Name,  // اسم المنتج
                    ProductDescription = s.Product.Description  // وصف المنتج
                })
                .ToListAsync();

            if (saleRequests == null || !saleRequests.Any())
            {
                return NotFound(new { message = "لا توجد طلبات بيع." });
            }

            return Ok(saleRequests);
        }


        [HttpPut("{id}/change-status")]
        public IActionResult ChangeStatus(int id, [FromBody] ChangeStatusRequest request)
        {
            var saleRequest = _db.SaleRequests.Include(sr => sr.Product).FirstOrDefault(sr => sr.RequestId == id);
            if (saleRequest == null)
            {
                return NotFound(new { message = "طلب البيع غير موجود." });
            }

            if (request.Status.Equals("approve", StringComparison.OrdinalIgnoreCase))
            {
                saleRequest.Status = "Approved"; 
                saleRequest.Product.IsActive = true;
                SendEmailNotification(saleRequest.User.Email, saleRequest.User.UserName, true); // إرسال إشعار بالبريد الإلكتروني
            }
            else if (request.Status.Equals("reject", StringComparison.OrdinalIgnoreCase))
            {
                saleRequest.Status = "Rejected";  // تغيير حالة الطلب إلى "Rejected"
                saleRequest.Product.IsActive = false; // إبقاء حالة isActive للمنتج بـ false في حالة الرفض
                SendEmailNotification(saleRequest.User.Email, saleRequest.User.UserName, false); // إرسال إشعار بالرفض
            }
            else
            {
                return BadRequest(new { message = "حالة غير صالحة. استخدم 'approve' أو 'reject'." });
            }

            _db.SaveChanges();
            return NoContent();
        }




        private void SendEmailNotification(string email, string userName, bool isApproved)
        {
            var subject = isApproved ? "إشعار بالموافقة" : "إشعار بالرفض";
            var message = isApproved
                ? $"عزيزي/عزيزتي {userName},\n\nنحن سعيدون أن نخبرك بأنه تم الموافقة على طلبك لبيع المنتج! 🎉 يمكنك الآن مشاهدة منتجك على الموقع، وعند بيعه، ستتلقى إشعارًا بذلك. شكرًا لكونك جزءًا من مجتمعنا!"
                : $"عزيزي/عزيزتي {userName},\n\nنأسف لإخبارك بأن طلبك لبيع المنتج لم يتم قبوله. لا تتردد في تقديم طلب آخر في المستقبل. نحن هنا لدعمك!";

            _emailService.SendEmail(email, subject, message);
        }








        //[HttpPut("{id}")]
        //public IActionResult ChangeStatus(int id, [FromForm] ChangeStatusRequest request)
        //{
        //    var saleRequest = _db.SaleRequests.Include(sr => sr.User).FirstOrDefault(sr => sr.RequestId == id);
        //    if (saleRequest == null)
        //    {
        //        return NotFound(new { message = "طلب البيع غير موجود." });
        //    }

        //    if (request.Status.Equals("approve", StringComparison.OrdinalIgnoreCase))
        //    {
        //        saleRequest.Status = "Approved";
        //        SendEmailNotification(saleRequest.User.Email, saleRequest.User.UserName, true, null);
        //    }
        //    else if (request.Status.Equals("reject", StringComparison.OrdinalIgnoreCase))
        //    {
        //        saleRequest.Status = "Rejected";
        //        SendEmailNotification(saleRequest.User.Email, saleRequest.User.UserName, false, request.RejectionReason);
        //    }
        //    else
        //    {
        //        return BadRequest(new { message = "حالة غير صالحة. استخدم 'approve' أو 'reject'." });
        //    }

        //    _db.SaveChanges();
        //    return NoContent();
        //}

        //private void SendEmailNotification(string email, string userName, bool isApproved, string rejectionReason)
        //{
        //    var subject = isApproved ? "إشعار بالموافقة" : "إشعار بالرفض";
        //    var message = isApproved
        //        ? $"عزيزي/عزيزتي {userName},\n\nنحن سعيدون أن نخبرك بأنه تم الموافقة على طلبك لبيع المنتج! 🎉 يمكنك الآن مشاهدة منتجك على الموقع، وعند بيعه، ستتلقى إشعارًا بذلك. شكرًا لكونك جزءًا من مجتمعنا!"
        //        : $"عزيزي/عزيزتي {userName},\n\nنأسف لإخبارك بأن طلبك لبيع المنتج لم يتم قبوله. السبب: {rejectionReason}.\nلا تتردد في تقديم طلب آخر في المستقبل. نحن هنا لدعمك!";

        //    _emailService.SendEmail(email, subject, message);
        //}








        // DELETE: api/salerequests/5
        [HttpDelete("{id}")]
        public IActionResult DeleteSaleRequest(int id)
        {
            var saleRequest = _db.SaleRequests.Include(sr => sr.Product).FirstOrDefault(sr => sr.RequestId == id);

            if (saleRequest == null)
            {
                return NotFound();
            }

            // حذف الصورة من الخادم إذا كانت موجودة
            if (!string.IsNullOrEmpty(saleRequest.Product.Image))
            {
                var imageFilePath = Path.Combine(Directory.GetCurrentDirectory(), "Uploads", saleRequest.Product.Image);

                if (System.IO.File.Exists(imageFilePath))
                {
                    System.IO.File.Delete(imageFilePath); // حذف الصورة من المجلد
                }
            }

            // حذف المنتج المرتبط بالطلب
            _db.Products.Remove(saleRequest.Product);
            _db.SaleRequests.Remove(saleRequest); // إزالة الطلب من قاعدة البيانات
            _db.SaveChanges(); // حفظ التغييرات

            return NoContent(); // أرجع حالة 204 (لا يوجد محتوى) بعد الحذف
        }


    }
}








