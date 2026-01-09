namespace webapp.Models;

public sealed class SelectedViewModel
{
    public Guid SessionId { get; set; }
    public List<SelectedItemViewModel> Items { get; set; } = new();
}

public sealed class SelectedItemViewModel
{
    public string Md5 { get; set; } = string.Empty;
    public string ShortDetails { get; set; } = string.Empty;
    public string LargeDetails { get; set; } = string.Empty;
    public string[] Tags { get; set; } = Array.Empty<string>();
    public string ImageUrl { get; set; } = string.Empty;
    public string RealUrl { get; set; } = string.Empty;
    public string CommerceMark { get; set; } = string.Empty;
    public string ImprovementWays { get; set; } = string.Empty;

    public int? Width { get; set; }
    public int? Height { get; set; }
}
