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

        static ReviewController()
        {
            // Initialize the Azure Storage Queue client
            string queueName = Environment.GetEnvironmentVariable("QUEUE_NAME") ?? "new-product-reviews";
            string storageAccountName = Environment.GetEnvironmentVariable("STORAGE_ACCOUNT_NAME") ?? "randomstorageaccount";
            string mi_client_id = Environment.GetEnvironmentVariable("USER_ASSIGNED_MI_CLIENT_ID") ?? string.Empty;

            if (!string.IsNullOrEmpty(mi_client_id))
            {
                // User-Assigned Managed Identity should be added to the app and given access to the Storage Account as Storage Queue Data Contributor.
                // Client ID of the User-Assigned Managed Identity should be passed as an AppSetting named USER_ASSIGNED_MI_CLIENT_ID.
                Console.WriteLine($"Connecting to storage queue {queueName} using User-Assigned Managed Identity");
                _queueClient = new QueueClient(
                    new Uri($"https://{storageAccountName}.queue.core.windows.net/{queueName}"),
                    new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(mi_client_id)));
            }
            else
            {
                // System-Assigned Managed Identity should be enabled for the app and given access to the Storage Account as Storage Queue Data Contributor.
                Console.WriteLine($"Connecting to storage queue {queueName} using System-Assigned Managed Identity");
                _queueClient = new QueueClient(
                    new Uri($"https://{storageAccountName}.queue.core.windows.net/{queueName}"),
                    new ManagedIdentityCredential());
            }

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

                var db = new ProductsDB();
                int reviewId = db.AddProductReview(
                    productId: model.ProductID,
                    customerId: model.CustomerID ?? 0,
                    rating: model.Rating,
                    reviewText: model.ReviewText
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
                            reviewId = reviewId
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