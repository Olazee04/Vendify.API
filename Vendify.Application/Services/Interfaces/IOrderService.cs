using Vendify.Application.Common.Models;
using Vendify.Application.DTOs.Order;

namespace Vendify.Application.Services.Interfaces
{
    public interface IOrderService
    {
        Task<ApiResponse<OrderDto>> CreateOrderAsync(
            CreateOrderRequest request, string storeSlug);

        Task<ApiResponse<OrderDto>> GetOrderByIdAsync(
            Guid orderId, Guid storeId);

        Task<ApiResponse<OrderDto>> GetOrderByNumberAsync(string orderNumber);

        Task<ApiResponse<PagedOrdersDto>> GetStoreOrdersAsync(
            Guid storeId, OrderFilterRequest filter);

        Task<ApiResponse<OrderDto>> UpdateOrderStatusAsync(
            Guid orderId, UpdateOrderStatusRequest request, Guid storeId);

        Task<ApiResponse<OrderDto>> UpdatePaymentStatusAsync(
            Guid orderId, string paymentReference, Guid storeId);

        Task<ApiResponse> CancelOrderAsync(Guid orderId, Guid storeId);
    }
}