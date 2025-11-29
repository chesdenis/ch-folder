using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using webapp.Models;

namespace webapp.Controllers;

public class HomeController(ILogger<HomeController> logger) : Controller
{
    public IActionResult Index([FromQuery] string[]? tags)
    {
        // expose selected tags (from model binding) to the view
        ViewBag.SelectedTags = tags ?? Array.Empty<string>();
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}