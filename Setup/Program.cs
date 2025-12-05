using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BusyWatcherSetup
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Busy Accounting Stock Monitor Setup");
            Console.WriteLine("===================================");
            Console.WriteLine();

            // Get the path to the main service appsettings
            string serviceDirectory = Path.Combine(Directory.GetCurrentDirectory(), "..");
            string configPath = Path.Combine(serviceDirectory, "appsettings.json");

            if (!File.Exists(configPath))
            {
                Console.WriteLine($"Error: Configuration file not found at {configPath}");
                Console.WriteLine("Please ensure you're running this setup from the correct location.");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("Setting up Busy Accounting Stock Monitor...");
            Console.WriteLine($"Service configuration file: {configPath}");
            Console.WriteLine();

            // Gather database configuration
            var dbConfig = GetDatabaseConfiguration();

            // Gather API configuration
            var apiConfig = GetApiConfiguration();

            // Gather polling configuration
            var pollingConfig = GetPollingConfiguration();

            // Update the configuration file
            UpdateConfiguration(configPath, dbConfig, apiConfig, pollingConfig);

            Console.WriteLine();
            Console.WriteLine("Configuration completed successfully!");
            Console.WriteLine();
            Console.WriteLine("Next steps:");
            Console.WriteLine("1. Ensure the Busy database is accessible at the provided IP and port");
            Console.WriteLine("2. Verify the API endpoint is accessible and configured to receive data");
            Console.WriteLine("3. Run the installation script to install the service");
            Console.WriteLine();
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }

        static (string server, string database, string userId, string password) GetDatabaseConfiguration()
        {
            Console.WriteLine("Database Configuration:");
            Console.WriteLine("-----------------------");

            Console.Write("Database Server IP Address and Port (e.g., 192.168.1.100,3011): ");
            string server = Console.ReadLine()?.Trim() ?? "";

            if (string.IsNullOrEmpty(server))
            {
                Console.Write("Enter server (default: localhost,3011): ");
                server = Console.ReadLine()?.Trim() ?? "localhost,3011";
            }

            Console.Write("Database Name (default: BusyDatabase): ");
            string database = Console.ReadLine()?.Trim() ?? "";
            if (string.IsNullOrEmpty(database))
            {
                database = "BusyDatabase";
            }

            Console.Write("Database User ID (default: sa): ");
            string userId = Console.ReadLine()?.Trim() ?? "";
            if (string.IsNullOrEmpty(userId))
            {
                userId = "sa";
            }

            Console.Write("Database Password: ");
            string password = ReadPassword();

            Console.WriteLine();

            // Test the connection
            Console.WriteLine("Testing database connection...");
            bool isConnected = TestDatabaseConnection(server, database, userId, password);

            if (!isConnected)
            {
                Console.WriteLine("Warning: Could not verify database connection. Please ensure the details are correct.");
                Console.Write("Continue anyway? (y/N): ");
                string response = Console.ReadLine()?.ToLower() ?? "n";

                if (response != "y" && response != "yes")
                {
                    Console.WriteLine("Setup cancelled.");
                    Environment.Exit(1);
                }
            }
            else
            {
                Console.WriteLine("Database connection verified successfully!");
            }

            return (server, database, userId, password);
        }

        static (string endpoint, string dummy) GetApiConfiguration()
        {
            Console.WriteLine();
            Console.WriteLine("API Configuration:");
            Console.WriteLine("------------------");

            Console.Write("API Endpoint URL (e.g., http://localhost:5000/api/stock/update): ");
            string endpoint = Console.ReadLine()?.Trim() ?? "";

            if (string.IsNullOrEmpty(endpoint))
            {
                endpoint = "http://localhost:5000/api/stock/update";
            }

            return (endpoint, ""); // dummy value added to make tuple have two elements
        }

        static (int seconds, string dummy) GetPollingConfiguration()
        {
            Console.WriteLine();
            Console.WriteLine("Polling Configuration:");
            Console.WriteLine("----------------------");

            Console.Write("Polling Interval in Seconds (default 30): ");
            string input = Console.ReadLine()?.Trim() ?? "30";

            if (!int.TryParse(input, out int seconds) || seconds <= 0)
            {
                seconds = 30;
            }

            Console.WriteLine($"Using polling interval of {seconds} seconds");
            return (seconds, ""); // dummy value added to make tuple have two elements
        }

        static string ReadPassword()
        {
            string password = "";
            ConsoleKeyInfo key;

            do
            {
                key = Console.ReadKey(true);
                if (key.Key != ConsoleKey.Backspace && key.Key != ConsoleKey.Enter)
                {
                    password += key.KeyChar;
                    Console.Write("*");
                }
                else if (key.Key == ConsoleKey.Backspace && password.Length > 0)
                {
                    password = password.Substring(0, password.Length - 1);
                    Console.Write("\b \b");
                }
            }
            while (key.Key != ConsoleKey.Enter);

            Console.WriteLine();
            return password;
        }

        static bool TestDatabaseConnection(string server, string database, string userId, string password)
        {
            try
            {
                string connectionString = $"Server={server};Database={database};User Id={userId};Password={password};Connection Timeout=5;TrustServerCertificate=True;";

                using var connection = new System.Data.SqlClient.SqlConnection(connectionString);
                connection.Open();

                // Test with a simple query
                using var command = new System.Data.SqlClient.SqlCommand("SELECT 1", connection);
                var result = command.ExecuteScalar();

                return result != null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Connection test failed: {ex.Message}");
                return false;
            }
        }

        static void UpdateConfiguration(string configPath,
            (string server, string database, string userId, string password) dbConfig,
            (string endpoint, string dummy1) apiConfig,
            (int seconds, string dummy2) pollingConfig)
        {
            // Read the existing appsettings.json
            string jsonContent = File.ReadAllText(configPath);

            // Parse the JSON
            JObject configObj = JObject.Parse(jsonContent);

            // Update connection string
            string connectionString = $"Server={dbConfig.server};Database={dbConfig.database};User Id={dbConfig.userId};Password={dbConfig.password};TrustServerCertificate=True;";
            configObj["ConnectionStrings"]["DefaultConnection"] = connectionString;

            // Update API settings
            configObj["ApiSettings"]["Endpoint"] = apiConfig.endpoint;

            // Update polling interval
            configObj["PollingInterval"]["Seconds"] = pollingConfig.seconds;

            // Write the updated configuration back to the file
            string updatedJson = configObj.ToString(Formatting.Indented);
            File.WriteAllText(configPath, updatedJson);
        }
    }
}