using Microsoft.AspNetCore.Mvc;

namespace webapp.Components
{
    public class PaginationModel
    {
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
    }

    public class PaginationViewComponent : ViewComponent
    {
        public IViewComponentResult Invoke(int currentPage, int totalPages)
        {
            if (totalPages < 1)
            {
                totalPages = 1;
            }

            if (currentPage < 1)
            {
                currentPage = 1;
            }
            else if (currentPage > totalPages)
            {
                currentPage = totalPages;
            }

            var model = new PaginationModel
            {
                CurrentPage = currentPage,
                TotalPages = totalPages
            };

            return View(model);
        }
    }
}