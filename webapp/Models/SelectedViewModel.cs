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
    public string[] Tags { get; set; } = Array.Empty<string>();
    public string ImageUrl { get; set; } = string.Empty;
}
