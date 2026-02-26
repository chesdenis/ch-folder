using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace webapp.Components;

public class TagsSelectorViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(IEnumerable<string> availableTags, IEnumerable<string>? selectedTags = null)
    {
        // If not explicitly provided, try to read selected tags from the current request/model state
        if (selectedTags == null)
        {
            if (HttpContext?.Request?.Query.TryGetValue("tags", out var values) == true)
            {
                selectedTags = values.ToArray();
            }
        }

        ViewBag.AvailableTags =  new HashSet<string>(availableTags, StringComparer.OrdinalIgnoreCase);
        ViewBag.SelectedTags = new HashSet<string>(selectedTags ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        return View();
    }
}
