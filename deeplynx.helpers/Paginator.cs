using deeplynx.models;
using Microsoft.EntityFrameworkCore;

namespace deeplynx.helpers;

/// <summary>
/// Paginating utilities. Paginating may be used to improve load times by reducing network traffic.
/// </summary>
public static class Paginator
{
    /// <summary>
    /// Paginates a query.
    /// </summary>
    /// <typeparam name="T">The query type</typeparam>
    /// <param name="paginated">The paginated request</param>
    /// <param name="values">The query</param>
    /// <returns>The query paginated</returns>
    static public Task<PaginatedResponse<T>> ToPaginatedAsync<T>(this IQueryable<T> values, PaginatedRequestDto paginated)
    {
        if (paginated.PageSize == -1)
        {
            return PaginateAll(values);
        }

        return Paginate(values, paginated);
    }

    /// <summary>
    /// Paginates a query.
    /// </summary>
    /// <typeparam name="T">The query type</typeparam>
    /// <param name="paginated">The paginated request</param>
    /// <param name="values">The query</param>
    /// <returns>The query paginated</returns>
    static private async Task<PaginatedResponse<T>> Paginate<T>(IQueryable<T> values, PaginatedRequestDto paginated)
    {
        return new PaginatedResponse<T>
        {
            Items = await values
                    .Skip((paginated.PageNumber - 1) * paginated.PageSize)
                    .Take(paginated.PageSize)
                    .ToListAsync(),
            PageNumber = paginated.PageNumber,
            PageSize = paginated.PageSize,
            TotalCount = await values.CountAsync(),
        };
    }

    /// <summary>
    /// Paginates a query by including all entities.
    /// </summary>
    /// <typeparam name="T">The query type</typeparam>
    /// <param name="values">The query</param>
    /// <returns>The query paginated</returns>
    static private async Task<PaginatedResponse<T>> PaginateAll<T>(IQueryable<T> values)
    {
        var items = await values.ToListAsync();
        return new PaginatedResponse<T>
        {
            Items = items,
            PageNumber = 1,
            PageSize = items.Count,
            TotalCount = items.Count,
        };
    }
}
