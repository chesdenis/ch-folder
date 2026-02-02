using Microsoft.AspNetCore.Mvc;

namespace webapp.Components;

public class MultiSelectorViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(
        string id, 
        string parameterName, 
        IEnumerable<string> availableValues, 
        IEnumerable<string>? selectedValues = null, 
        string placeholder = "Add...")
    {
        ViewBag.Id = id;
        ViewBag.ParameterName = parameterName;
        ViewBag.AvailableValues = new HashSet<string>(availableValues.Select(v => v.ToLowerInvariant()), StringComparer.OrdinalIgnoreCase);
        ViewBag.SelectedValues = new HashSet<string>((selectedValues ?? Array.Empty<string>()).Select(v => v.ToLowerInvariant()), StringComparer.OrdinalIgnoreCase);
        ViewBag.Placeholder = placeholder;
        
        return View();
    }
}
