using Vendify.Application.DTOs.Reviews;
using Vendify.Application.Common.Models;

namespace Vendify.Application.Services.Interfaces;

public interface IReviewService
{
    Task<ApiResponse<ProductReviewsDto>>
        GetProductReviewsAsync(string productId);
    Task<ApiResponse<ReviewDto>>
        CreateReviewAsync(CreateReviewDto dto);
    Task<ApiResponse<bool>>
        ReplyToReviewAsync(
            string reviewId,
            string merchantId,
            string reply);
    Task<ApiResponse<bool>>
        DeleteReviewAsync(
            string reviewId,
            string merchantId);
}