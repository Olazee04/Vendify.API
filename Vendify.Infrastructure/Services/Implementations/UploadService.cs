using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Configuration;
using Vendify.Application.Common.Models;
using Vendify.Application.DTOs.Upload;
using Vendify.Application.Services.Interfaces;

namespace Vendify.Infrastructure.Services.Implementations
{
    public class UploadService : IUploadService
    {
        private readonly Cloudinary _cloudinary;

        public UploadService(IConfiguration configuration)
        {
            var cloudName = configuration["Cloudinary:CloudName"]!;
            var apiKey = configuration["Cloudinary:ApiKey"]!;
            var apiSecret = configuration["Cloudinary:ApiSecret"]!;

            var account = new Account(cloudName, apiKey, apiSecret);
            _cloudinary = new Cloudinary(account);
            _cloudinary.Api.Secure = true;
        }

        public async Task<ApiResponse<UploadResultDto>> UploadImageAsync(
            Stream fileStream,
            string fileName,
            string folder)
        {
            try
            {
                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(fileName, fileStream),
                    Folder = $"vendify/{folder}",
                    Transformation = new Transformation()
                        .Quality("auto")
                        .FetchFormat("auto"),
                    UseFilename = false,
                    UniqueFilename = true,
                    Overwrite = false
                };

                var result = await _cloudinary.UploadAsync(uploadParams);

                if (result.Error != null)
                    return ApiResponse<UploadResultDto>.FailureResponse(
                        result.Error.Message);

                return ApiResponse<UploadResultDto>.SuccessResponse(
                    new UploadResultDto
                    {
                        Url = result.SecureUrl.ToString(),
                        PublicId = result.PublicId,
                        Format = result.Format,
                        Width = result.Width,
                        Height = result.Height,
                        Size = result.Bytes
                    }, "Image uploaded successfully");
            }
            catch (Exception ex)
            {
                return ApiResponse<UploadResultDto>.FailureResponse(
                    $"Upload failed: {ex.Message}");
            }
        }

        public async Task<ApiResponse> DeleteImageAsync(string publicId)
        {
            try
            {
                var deleteParams = new DeletionParams(publicId);
                var result = await _cloudinary.DestroyAsync(deleteParams);

                if (result.Result == "ok")
                    return ApiResponse.SuccessResponse(
                        "Image deleted successfully");

                return ApiResponse.FailureResponse(
                    "Failed to delete image");
            }
            catch (Exception ex)
            {
                return ApiResponse.FailureResponse(
                    $"Delete failed: {ex.Message}");
            }
        }
    }
}