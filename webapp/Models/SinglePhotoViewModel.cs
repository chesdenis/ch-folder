namespace webapp.Models;

public sealed class SinglePhotoViewModel
{
    public SelectedItemViewModel Photo { get; set; } = null!;
    public List<SelectedItemViewModel> SimilarPhotos { get; set; } = new();
}
