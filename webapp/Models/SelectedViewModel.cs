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
    
    public string RefinementPrompt => $"- I have this russian description of a photo: \n\n " +
                                      $"{LargeDetails}\n\n" +
                                      $"- In addition to that I have this review of this photo:\n\n {ImprovementWays} \n\n" +
                                      $"- Create for me fully and maximized detailed code for Midjourney photo editing model " +
                                      $"to improve this photo to get commerce potential to 4.8. When you generate code " +
                                      $"do not hesitate to change colors and location of objects on the photo.";
    
    public string[] Tags { get; set; } = Array.Empty<string>();
    public string ImageUrl { get; set; } = string.Empty;
    public string RealUrl { get; set; } = string.Empty;
    public string CommerceMark { get; set; } = string.Empty;
    public string ImprovementWays { get; set; } = string.Empty;

    public int? Width { get; set; }
    public int? Height { get; set; }
    public string[] PublishPlatforms { get; set; } = Array.Empty<string>();
}
