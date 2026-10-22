namespace FarmHand.Application.Common.DTOs;

/// <summary>Paginated result wrapper.</summary>
/// <typeparam name="T">Item type.</typeparam>
/// <param name="Items">Items for the current page.</param>
/// <param name="Page">Current page number (1-based).</param>
/// <param name="PageSize">Number of items per page.</param>
/// <param name="TotalCount">Total number of items available.</param>
/// <param name="TotalPages">Total number of pages.</param>
public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
