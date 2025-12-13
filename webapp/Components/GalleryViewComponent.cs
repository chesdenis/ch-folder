using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;

namespace webapp.Components
{
    public class GalleryItem
    {
        public string FullUrl { get; set; } = string.Empty; // Large/original URL
        public int FullWidth { get; set; }
        public int FullHeight { get; set; }

        public string Alt { get; set; } = string.Empty;

        // Helper: builds thumbnail URL for a requested width
        public string GetThumbUrl(int width)
        {
            // Unsplash supports width param `w=`
            var separator = FullUrl.Contains("?") ? "&" : "?";
            return $"{FullUrl}{separator}w={width}";
        }
    }

    public class GalleryModel
    {
        public IReadOnlyList<GalleryItem> Items { get; set; } = Array.Empty<GalleryItem>();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int ThumbSize { get; set; }
    }

    public class GalleryViewComponent : ViewComponent
    {
        // Sample/demo data removed: component now renders only real items provided by callers.

        public IViewComponentResult Invoke(
            int page = 1,
            int pageSize = 12,
            int thumbSize = 256,
            IReadOnlyList<GalleryItem>? items = null,
            int? totalItems = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 12;

            // Clamp thumbSize to allowed steps
            var allowed = new[] { 16, 32, 64, 128, 256, 512, 2000 };
            if (!allowed.Contains(thumbSize))
            {
                // pick nearest
                thumbSize = allowed.OrderBy(a => Math.Abs(a - thumbSize)).First();
            }

            // Use only provided items (real data). If none provided, render empty state.
            var all = (items != null) ? items.ToList() : new List<GalleryItem>();
            var total = totalItems ?? all.Count;

            var pageItems = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var model = new GalleryModel
            {
                Items = pageItems,
                Page = page,
                PageSize = pageSize,
                TotalItems = total,
                ThumbSize = thumbSize
            };

            return View(model);
        }
    }
}
