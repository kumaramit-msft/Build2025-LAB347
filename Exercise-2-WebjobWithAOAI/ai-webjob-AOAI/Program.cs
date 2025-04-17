using Azure.Identity;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Identity.Client;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ai_webjob
{
    internal class Program
    {
        static async Task Main()
        {
            SQLitePCL.Batteries.Init();
            var dbConnectionString = @"Data Source=/home/site/wwwroot/Data/devshopdb.db";

            using var connection = new SqliteConnection(dbConnectionString);
            connection.Open();

            string productIdStr = await ReadProductIdFromQueueAsync();
            if (!int.TryParse(productIdStr, out int productId))
            {
                Console.WriteLine($"Invalid ProductID in queue: '{productIdStr}'");
                return;
            }

            try
            {
                var (reviews, ratings) = GetReviewsAndRatings(connection, productId);
                if (reviews.Count == 0)
                {
                    Console.WriteLine($"[SKIP] Product {productId} has no valid reviews.");
                    return;
                }

                var avgRating = ratings.Count > 0 ? ratings.Average() : 0.0;
                var reviewCount = reviews.Count;

                var useLocalSLM = Environment.GetEnvironmentVariable("USE_LOCAL_SLM")?.ToLowerInvariant() == "true";

                var (summary, sentiment) = useLocalSLM
                    ? await GetSummaryAndSentimentFromLocalSLM(reviews, $"Product ID: {productId}")
                    : await GetSummaryAndSentimentFromAzureOpenAI(reviews, $"Product ID: {productId}");

                UpdateProductSummary(connection, productId, reviewCount, avgRating, summary, sentiment);

                Console.WriteLine($"Updated Product {productId}: {reviewCount} reviews, Avg Rating {avgRating:F1}, Sentiment: {sentiment}, Summary: {summary}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing Product {productId}: {ex.Message}");
            }

            Console.WriteLine("Done.");
        }

        static async Task<string> ReadProductIdFromQueueAsync()
        {

            string queueName = Environment.GetEnvironmentVariable("QUEUE_NAME") ?? "new-product-reviews";
            string storageAccountName = Environment.GetEnvironmentVariable("STORAGE_ACCOUNT_NAME") ?? "randomstorageaccount";
            string mi_client_id = Environment.GetEnvironmentVariable("USER_ASSIGNED_CLIENT_ID");
            string connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");

            QueueClient? queueClient = null;

            if (!string.IsNullOrEmpty(mi_client_id))
            {
                queueClient = new QueueClient(
                    new Uri($"https://{Environment.GetEnvironmentVariable("STORAGE_ACCOUNT_NAME")}.queue.core.windows.net/{queueName}"),
                    new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(mi_client_id)));
            }
            else if (!string.IsNullOrEmpty(connectionString))
            {
                queueClient = new QueueClient(connectionString, queueName);
            }

            var messagesResponse = await queueClient.ReceiveMessagesAsync(maxMessages: 1, visibilityTimeout: TimeSpan.FromSeconds(5));
            var messages = messagesResponse.Value;
            if (messages.Length == 0)
            {
                Console.WriteLine("No messages found in queue.");
                return "";
            }
            if (!await queueClient.ExistsAsync())
            {
                Console.WriteLine("Queue does not exist.");
                return "";
            }
 
            string productId = messages[0].MessageText;

            await queueClient.DeleteMessageAsync(messages[0].MessageId, messages[0].PopReceipt);

            return productId;
        }

        static (List<string> Reviews, List<int> Ratings) GetReviewsAndRatings(SqliteConnection connection, int productId)
        {
            var reviews = new List<string>();
            var ratings = new List<int>();

            var sql = @"
                SELECT review_text, rating 
                FROM DevShop_Product_Reviews 
                WHERE ProductID = @ProductID 
                  AND review_text IS NOT NULL 
                  AND TRIM(review_text) <> ''";

            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@ProductID", productId);
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                var text = reader.GetString(0);
                var rating = reader.GetInt32(1);
                reviews.Add(text);
                ratings.Add(rating);
            }

            return (reviews, ratings);
        }

        static async Task<(string Summary, string Sentiment)> GetSummaryAndSentimentFromAzureOpenAI(List<string> reviews, string context)
        {
            var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
            var key = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY");
            var deployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT");

            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(key) || string.IsNullOrEmpty(deployment))
                throw new InvalidOperationException("Azure OpenAI environment variables are not properly set.");

            var url = $"{endpoint}/openai/deployments/{deployment}/chat/completions?api-version=2025-01-01-preview";

            var formattedReviews = string.Join("\n", reviews.Select(r => $"[{r}]"));

            var payload = new
            {
                messages = new[]
                {
                    new {
                        role = "system",
                        content = new[] { new { type = "text", text = "You are an assistant that summarizes user reviews and analyzes sentiment." } }
                    },
                    new {
                        role = "user",
                        content = new[]
                        {
                            new { type = "text", text = "Below are individual product reviews, separated by []." },
                            new { type = "text", text = "Please respond in the following format:" },
                            new { type = "text", text = "Summary: <summary>\\nSentiment: <positive/mixed/negative>" },
                            new { type = "text", text = $"\nContext: {context}" },
                            new { type = "text", text = formattedReviews }
                        }
                    }
                },
                max_tokens = 800,
                temperature = 0.3,
                top_p = 0.95,
                frequency_penalty = 0,
                presence_penalty = 0,
                stream = false
            };

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("api-key", key);

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseBody);
            var messageContent = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            var summaryLine = messageContent.Split('\n').FirstOrDefault(l => l.StartsWith("Summary:", StringComparison.OrdinalIgnoreCase));
            var sentimentLine = messageContent.Split('\n').FirstOrDefault(l => l.StartsWith("Sentiment:", StringComparison.OrdinalIgnoreCase));

            string summary = summaryLine?.Replace("Summary:", "").Trim() ?? "";
            string sentiment = sentimentLine?.Replace("Sentiment:", "").Trim().ToLowerInvariant() ?? "unknown";

            return (summary, sentiment);
        }

        static async Task<(string Summary, string Sentiment)> GetSummaryAndSentimentFromLocalSLM(List<string> reviews, string context)
        {
            var url = "http://localhost:11434/v1/chat/completions";

            var formattedReviews = string.Join("\n", reviews.Select(r => $"[{r}]"));

            var requestPayload = new
            {
                messages = new[]
                           {
                                      new {
                                          role = "system",
                                          content = "You are an assistant that summarizes user reviews and analyzes sentiment."
                                      },
                                      new {
                                          role = "user",
                                          content = $"Below are individual product reviews, separated by [].\nPlease respond in the following format:\nSummary: <summary>\\nSentiment: <positive/mixed/negative>\nContext: {context}\n{formattedReviews}"
                                      }
                                  },
                stream = true,
                cache_prompt = false,
                n_predict = 500
            };

            using var httpClient = new HttpClient();

            var json = JsonSerializer.Serialize(requestPayload);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            var stringBuilder = new StringBuilder();

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                line = line?.Replace("data: ", string.Empty).Trim();
                if (!string.IsNullOrEmpty(line) && line != "[DONE]")
                {
                    var jsonObject = JsonNode.Parse(line);
                    var responseContent = jsonObject?["choices"]?[0]?["delta"]?["content"]?.ToString();
                    if (!string.IsNullOrEmpty(responseContent))
                    {
                        stringBuilder.Append(responseContent);
                    }
                }
            }

            var messageContent = stringBuilder.ToString();

            var summaryLine = messageContent.Split('\n').FirstOrDefault(l => l.Trim().StartsWith("Summary:", StringComparison.OrdinalIgnoreCase));
            var sentimentLine = messageContent.Split('\n').FirstOrDefault(l => l.Trim().StartsWith("Sentiment:", StringComparison.OrdinalIgnoreCase));

            string summary = summaryLine?.Replace("Summary:", "").Trim() ?? "";
            string sentiment = sentimentLine?.Replace("Sentiment:", "").Trim().ToLowerInvariant() ?? "unknown";

            return (summary, sentiment);
        }

        static void UpdateProductSummary(SqliteConnection connection, int productId, int reviewCount, double avgRating, string summary, string sentiment)
        {
            var sql = @"
                UPDATE DevShop_Product_Master
                SET review_count = @ReviewCount,
                    average_rating = @AvgRating,
                    review_summary = @Summary,
                    product_sentiment = @Sentiment
                WHERE ProductID = @ProductID";

            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@ReviewCount", reviewCount);
            command.Parameters.AddWithValue("@AvgRating", avgRating);
            command.Parameters.AddWithValue("@Summary", summary);
            command.Parameters.AddWithValue("@Sentiment", sentiment);
            command.Parameters.AddWithValue("@ProductID", productId);

            var affected = command.ExecuteNonQuery();

            if (affected == 0)
            {
                throw new Exception("Update failed. Product may not exist in Product_Master.");
            }
        }
    }
}
