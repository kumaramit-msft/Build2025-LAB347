using System;
using System.ComponentModel.DataAnnotations;

namespace devShopDNC.Models
{
    public class ReviewViewModel
    {
        public int ReviewID { get; set; }

        [Required]
        public int ProductID { get; set; }

        public int? CustomerID { get; set; }

        [Required]
        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")]
        public int Rating { get; set; }

        [Display(Name = "Review")]
        public string ReviewText { get; set; }

        public DateTime ReviewDate { get; set; } = DateTime.Now;

        // Optional: Assign random CustomerID if not provided
        public void AssignRandomCustomerIDIfMissing()
        {
            if (!CustomerID.HasValue)
            {
                var rnd = new Random();
                CustomerID = rnd.Next(1, 101); // 1 to 100 inclusive
            }
        }
    }
}
