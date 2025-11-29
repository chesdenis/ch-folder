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
        private static readonly string[] SampleUrls = new[]
        {
            "https://images.unsplash.com/photo-1581833971358-2c8b550f87b3", // cat
            "https://images.unsplash.com/photo-1504208434309-cb69f4fe52b0",
            "https://images.unsplash.com/photo-1507149833265-60c372daea22",
            "https://images.unsplash.com/photo-1494790108377-be9c29b29330",
            "https://images.unsplash.com/photo-1500530855697-b586d89ba3ee",
            "https://images.unsplash.com/photo-1472214103451-9374bd1c798e",
            "https://images.unsplash.com/photo-1529626455594-4ff0802cfb7e",
            "https://images.unsplash.com/photo-1520975928316-56c43a27b33a",
            "https://images.unsplash.com/photo-1544005313-94ddf0286df2",
            "https://images.unsplash.com/photo-1517841905240-472988babdf9",
            "https://images.unsplash.com/photo-1517423440428-a5a00ad493e8",
            "https://images.unsplash.com/photo-1503023345310-bd7c1de61c7d",
            "https://images.unsplash.com/photo-1519681393784-d120267933ba",
            "https://images.unsplash.com/photo-1441974231531-c6227db76b6e",
            "https://images.unsplash.com/photo-1503023345310-bd7c1de61c7d"
        };

        private static IEnumerable<GalleryItem> BuildSampleItems()
        {
            // Assume 1600x1067 as base for demo
            var rnd = new Random(1234);
            foreach (var (url, idx) in SampleUrls.Select((u, i) => (u, i)))
            {
                var w = 1400 + rnd.Next(200);
                var h = 900 + rnd.Next(300);
                yield return new GalleryItem
                {
                    FullUrl = url,
                    FullWidth = w,
                    FullHeight = h,
                    Alt = $"Image {idx + 1}"
                };
            }
        }

        public IViewComponentResult Invoke(int page = 1, int pageSize = 12, int thumbSize = 256)
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

            var all = BuildSampleItems().ToList();
            var total = all.Count;

            var items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var model = new GalleryModel
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalItems = total,
                ThumbSize = thumbSize
            };

            return View(model);
        }
    }
}
