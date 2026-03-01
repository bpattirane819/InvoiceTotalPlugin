using Microsoft.Extensions.Configuration;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.PowerPlatform.Dataverse.Client.Extensions;
using System;

namespace TestHarness
{
    internal class Program
    {
        static void Main(string[] args)
        {
            IConfiguration configuration;

            try
            {
                var basePath = AppDomain.CurrentDomain.BaseDirectory;

                configuration = new ConfigurationBuilder()
                    .SetBasePath(basePath)
                    .AddJsonFile("appsettings.local.json", optional: false)
                    .Build();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to load appsettings.local.json");
                Console.WriteLine(ex.Message);
                PauseAndExit();
                return;
            }

            var connectionString = configuration["Dataverse:ConnectionString"];
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Console.WriteLine("Missing configuration value: Dataverse:ConnectionString");
                PauseAndExit();
                return;
            }

            Console.WriteLine("Connecting to Dataverse...");
            using var serviceClient = new ServiceClient(connectionString);

            if (!serviceClient.IsReady)
            {
                Console.WriteLine("Dataverse connection failed:");
                Console.WriteLine(serviceClient.LastError);
                PauseAndExit();
                return;
            }

            Console.WriteLine($"Connected to: {serviceClient.ConnectedOrgFriendlyName}");
            Console.WriteLine($"User: {serviceClient.GetMyUserId()}");
            Console.WriteLine();

            // ==========================================================
            // TODO: Plugin / business logic here

            // ==========================================================

            Console.WriteLine("Harness ready. Add calls above.");
            PauseAndExit();
        }

        private static void PauseAndExit()
        {
            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
