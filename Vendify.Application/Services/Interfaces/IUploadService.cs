using Vendify.Application.Common.Models;
using Vendify.Application.DTOs.Upload;

namespace Vendify.Application.Services.Interfaces
{
    public interface IUploadService
    {
        Task<ApiResponse<UploadResultDto>> UploadImageAsync(
            Stream fileStream,
            string fileName,
            string folder);

        Task<ApiResponse> DeleteImageAsync(string publicId);
    }
}