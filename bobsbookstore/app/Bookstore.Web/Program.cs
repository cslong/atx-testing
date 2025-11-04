
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data.Entity;
using System.Data.SqlClient;

namespace Bookstore.Web
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container
            builder.Services.AddControllersWithViews();

            // Configure Entity Framework 6
            var connectionString = builder.Configuration.GetValue<string>("ConnectionStrings:BookstoreDatabaseConnection")
                ?? "Server=(localdb)\\MSSQLLocalDB;Initial Catalog=BookStoreClassic;MultipleActiveResultSets=true;Integrated Security=SSPI;";

            // Register Entity Framework 6 dependencies
            builder.Services.AddScoped<DbContext>();

            // Add application settings
            builder.Services.Configure<Dictionary<string, string>>(options =>
            {
                options["Environment"] = builder.Configuration.GetValue<string>("Environment") ?? "Development";
                options["Services:Authentication"] = builder.Configuration.GetValue<string>("Services:Authentication") ?? "local";
                options["Services:Database"] = builder.Configuration.GetValue<string>("Services:Database") ?? "local";
                options["Services:FileService"] = builder.Configuration.GetValue<string>("Services:FileService") ?? "local";
                options["Services:ImageValidationService"] = builder.Configuration.GetValue<string>("Services:ImageValidationService") ?? "local";
                options["Services:LoggingService"] = builder.Configuration.GetValue<string>("Services:LoggingService") ?? "local";
                options["Authentication:Cognito:LocalClientId"] = builder.Configuration.GetValue<string>("Authentication:Cognito:LocalClientId") ?? "";
                options["Authentication:Cognito:AppRunnerClientId"] = builder.Configuration.GetValue<string>("Authentication:Cognito:AppRunnerClientId") ?? "";
                options["Authentication:Cognito:MetadataAddress"] = builder.Configuration.GetValue<string>("Authentication:Cognito:MetadataAddress") ?? "";
                options["Authentication:Cognito:CognitoDomain"] = builder.Configuration.GetValue<string>("Authentication:Cognito:CognitoDomain") ?? "";
                options["Files:BucketName"] = builder.Configuration.GetValue<string>("Files:BucketName") ?? "";
                options["Files:CloudFrontDomain"] = builder.Configuration.GetValue<string>("Files:CloudFrontDomain") ?? "";
            });

            // Add logging
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();
            builder.Logging.AddDebug();

            var app = builder.Build();

            // Configure the HTTP request pipeline
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }
            else
            {
                app.UseDeveloperExceptionPage();
            }

            // Global error handling middleware
            app.Use(async (context, next) =>
            {
                try
                {
                    await next();
                }
                catch (Exception ex)
                {
                    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogError(ex, "An unhandled exception occurred");
                    throw;
                }
            });

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }
    }
}
