using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Vendify.Application.Services.Interfaces;

namespace Vendify.API.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/v1/[controller]")]
    public class UploadController : ControllerBase
    {
        private readonly IUploadService _uploadService;
        private readonly IStoreService _storeService;
        private readonly IProductService _productService;

        // Allowed file types
        private readonly string[] _allowedTypes =
        {
            "image/jpeg", "image/jpg", "image/png",
            "image/webp", "image/gif"
        };

        // Max file size — 5MB
        private const long MaxFileSize = 5 * 1024 * 1024;

        public UploadController(
            IUploadService uploadService,
            IStoreService storeService,
            IProductService productService)
        {
            _uploadService = uploadService;
            _storeService = storeService;
            _productService = productService;
        }

        // POST /api/v1/upload/store/logo
        [HttpPost("store/logo")]
        public async Task<IActionResult> UploadStoreLogo(
            IFormFile file)
        {
            var validationError = ValidateFile(file);
            if (validationError != null)
                return BadRequest(new { message = validationError });

            var userId = GetUserId();
            var storeResult = await _storeService.GetMyStoreAsync(userId);
            if (!storeResult.Success)
                return BadRequest(new { message = "Store not found" });

            using var stream = file.OpenReadStream();
            var uploadResult = await _uploadService.UploadImageAsync(
                stream, file.FileName,
                $"stores/{storeResult.Data!.Id}/logo");

            if (!uploadResult.Success)
                return BadRequest(uploadResult);

            // Update store logo
            var updateResult = await _storeService.UploadLogoAsync(
                userId, uploadResult.Data!.Url);

            return updateResult.Success ? Ok(new
            {
                success = true,
                message = "Logo uploaded successfully",
                data = new
                {
                    url = uploadResult.Data.Url,
                    publicId = uploadResult.Data.PublicId
                }
            }) : BadRequest(updateResult);
        }

        // POST /api/v1/upload/store/banner
        [HttpPost("store/banner")]
        public async Task<IActionResult> UploadStoreBanner(
            IFormFile file)
        {
            var validationError = ValidateFile(file);
            if (validationError != null)
                return BadRequest(new { message = validationError });

            var userId = GetUserId();
            var storeResult = await _storeService.GetMyStoreAsync(userId);
            if (!storeResult.Success)
                return BadRequest(new { message = "Store not found" });

            using var stream = file.OpenReadStream();
            var uploadResult = await _uploadService.UploadImageAsync(
                stream, file.FileName,
                $"stores/{storeResult.Data!.Id}/banner");

            if (!uploadResult.Success)
                return BadRequest(uploadResult);

            var updateResult = await _storeService.UploadBannerAsync(
                userId, uploadResult.Data!.Url);

            return updateResult.Success ? Ok(new
            {
                success = true,
                message = "Banner uploaded successfully",
                data = new
                {
                    url = uploadResult.Data.Url,
                    publicId = uploadResult.Data.PublicId
                }
            }) : BadRequest(updateResult);
        }

        // POST /api/v1/upload/products/{productId}/images
        [HttpPost("products/{productId}/images")]
        public async Task<IActionResult> UploadProductImage(
            Guid productId,
            IFormFile file,
            [FromQuery] bool isPrimary = false)
        {
            var validationError = ValidateFile(file);
            if (validationError != null)
                return BadRequest(new { message = validationError });

            var userId = GetUserId();
            var storeResult = await _storeService.GetMyStoreAsync(userId);
            if (!storeResult.Success)
                return BadRequest(new { message = "Store not found" });

            using var stream = file.OpenReadStream();
            var uploadResult = await _uploadService.UploadImageAsync(
                stream, file.FileName,
                $"stores/{storeResult.Data!.Id}/products/{productId}");

            if (!uploadResult.Success)
                return BadRequest(uploadResult);

            // Add image to product
            var addImageResult = await _productService.AddProductImageAsync(
                productId,
                uploadResult.Data!.Url,
                isPrimary,
                storeResult.Data.Id);

            return addImageResult.Success ? Ok(new
            {
                success = true,
                message = "Product image uploaded successfully",
                data = new
                {
                    url = uploadResult.Data.Url,
                    publicId = uploadResult.Data.PublicId,
                    product = addImageResult.Data
                }
            }) : BadRequest(addImageResult);
        }

        // POST /api/v1/upload/general
        [HttpPost("general")]
        public async Task<IActionResult> UploadGeneral(IFormFile file)
        {
            var validationError = ValidateFile(file);
            if (validationError != null)
                return BadRequest(new { message = validationError });

            using var stream = file.OpenReadStream();
            var result = await _uploadService.UploadImageAsync(
                stream, file.FileName, "general");

            return result.Success ? Ok(result) : BadRequest(result);
        }

        // DELETE /api/v1/upload/{publicId}
        [HttpDelete("{publicId}")]
        public async Task<IActionResult> DeleteImage(string publicId)
        {
            var decodedPublicId = Uri.UnescapeDataString(publicId);
            var result = await _uploadService.DeleteImageAsync(
                decodedPublicId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── Private Helpers ──────────────────────────────────
        private Guid GetUserId() =>
            Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private string? ValidateFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return "No file provided";

            if (file.Length > MaxFileSize)
                return "File size cannot exceed 5MB";

            if (!_allowedTypes.Contains(file.ContentType.ToLower()))
                return "Only JPG, PNG, WebP and GIF images are allowed";

            return null;
        }
    }
}