using devShopDNC.Models;
using Microsoft.AspNetCore.Mvc;

using Azure.Identity;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using System.Text.Json;

namespace devShopDNC.Controllers
{
    public class ReviewController : Controller
    {

        private static QueueClient? _queueClient;
        private static int _reviewCount = 5000;


        static ReviewController()
        {
            // Initialize the Azure Storage Queue client
            string storageAccountName = Environment.GetEnvironmentVariable("STORAGE_ACCOUNT_NAME") ?? "randomstorageaccount";
            string queueName = Environment.GetEnvironmentVariable("QUEUE_NAME") ?? "randomqueue"; // Get queue name from environment variable

            // Instantiate a QueueClient to create and interact with the queue
            _queueClient = new QueueClient(
                new Uri($"https://{storageAccountName}.queue.core.windows.net/{queueName}"),
                new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(
                    Environment.GetEnvironmentVariable("USER_ASSIGNED_CLIENT_ID") ?? "randomclientid")));

            try
            {
                _queueClient.CreateIfNotExists();
                Console.WriteLine($"Queue '{queueName}' initialized successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize queue '{queueName}': {ex.Message}");
            }
        }

        [HttpGet("/Review")]
        public IActionResult Review(int productId)
        {
            var model = new ReviewViewModel
            {
                ProductID = productId
            };
            return View(model); // Will look for Views/Review/Review.cshtml
        }

        [HttpPost]
        public IActionResult Submit(ReviewViewModel model)
        {
            if (ModelState.IsValid)
            {
                model.AssignRandomCustomerIDIfMissing();
                model.ReviewID = ++_reviewCount; // Increment the review count for each new review

                var db = new ProductsDB();
                db.AddProductReview(
                    productId: model.ProductID,
                    customerId: model.CustomerID ?? 0,
                    rating: model.Rating,
                    reviewText: model.ReviewText,
                    reviewId: model.ReviewID

                );

                if (_queueClient == null)
                {
                    Console.WriteLine($"Skipped adding this product with product-id '{model.ProductID}' to queue as queue is not initialized");
                }
                else
                {
                    try
                    {
                        var messagePayload = new
                        {
                            productId = model.ProductID,
                            reviewId = model.ReviewID
                        };

                        string messageText = JsonSerializer.Serialize(messagePayload);
                        _queueClient.SendMessage(messageText);
                        Console.WriteLine($"Product and review added to queue: {messageText}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to add product with product-id '{model.ProductID}' to queue: {ex.Message}");
                    }
                }

                return Ok(); // For AJAX success
            }

            return BadRequest();
        }

    }
}