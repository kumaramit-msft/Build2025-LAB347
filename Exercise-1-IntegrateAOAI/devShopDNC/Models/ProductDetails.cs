using System.ComponentModel.DataAnnotations;

namespace devShopDNC.Models
{
    public class ProductDetails
    {

        [Key] public int ProductId { get; set; }
        public String ProductName { get; set; } = string.Empty;
        public String ProductGender { get; set; } = string.Empty;
        public String ProductImage { get; set; } = string.Empty;

        public int ProductPrice { get; set; } = 0;
        public String ProductDescription { get; set; } = string.Empty;
        public String ProductBrand { get; set; } = string.Empty;
        public String ProductColor { get; set; } = string.Empty;

        public double AverageRating { get; set; } = 0.0;
        public int ReviewCount { get; set; } = 0;
        public string ReviewSummary { get; set; } = string.Empty;
        public string ProductSentiment { get; set; } = string.Empty;
        public string ReviewKeywords { get; set; } = string.Empty;
    }
}
