namespace deeplynx.models;

/// <summary>
/// Paginating utilities. Paginating may be used to improve load times by reducing network traffic.
/// </summary>
public class Paginator
{
    /// <summary>
    /// Paginates a query.
    /// </summary>
    /// <typeparam name="T">The query type</typeparam>
    /// <param name="paginated">The paginated request</param>
    /// <param name="values">The query</param>
    /// <param name="map">The mapping to the final paginated value</param>
    /// <returns>The query paginated</returns>
    static public PaginatedResponse<U> Paginate<T, U>(PaginatedRequestDto paginated, IQueryable<T> values, Func<T, U> map)
    {
        return new PaginatedResponse<U>
        {
            Items = values
                    .Skip((paginated.PageNumber - 1) * paginated.PageSize)
                    .Take(paginated.PageSize)
                    .Select(map)
                    .ToList(),
            PageNumber = paginated.PageNumber,
            PageSize = paginated.PageSize,
            TotalCount = values.Count(),
        };
    }
}
