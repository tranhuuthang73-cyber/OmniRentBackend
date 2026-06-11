using System.Collections.Generic;
using OmniRentBackend.Models;

namespace OmniRentBackend.Models.ViewModels
{
    public class HomeViewModel
    {
        public List<Category> Categories { get; set; } = new List<Category>();
        public List<Product> FeaturedProducts { get; set; } = new List<Product>();
    }
}
