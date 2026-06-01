namespace Microsoft.Azure.Cirrus.Extensions.Resources.Models
{
    /// <summary>
    /// Wire shape of an ARM list response: a page that carries the items in
    /// <see cref="Value"/> and an optional <see cref="NextLink"/> to the next page.
    /// </summary>
    public interface IPagedResult<TItem>
    {
        TItem[]? Value { get; }
        string? NextLink { get; }
    }
}
