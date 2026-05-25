namespace Jellyfin.Plugin.CnNfo.Models;

public class DoubanCelebrity
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Role { get; set; }

    public string? PhotoUrl { get; set; }
}
