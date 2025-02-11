using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using System;

var builder = WebApplication.CreateBuilder(args);

// ✅ Read MongoDB connection string from appsettings.json
string mongoConnectionString = builder.Configuration.GetConnectionString("MongoDb") ?? "mongodb://localhost:27017";
string databaseName = builder.Configuration["DatabaseName"] ?? "ImageGallery";

// ✅ Register MongoDB services
builder.Services.AddSingleton<IMongoClient>(sp => new MongoClient(mongoConnectionString));
builder.Services.AddSingleton<IMongoDatabase>(sp => 
    sp.GetRequiredService<IMongoClient>().GetDatabase(databaseName));

// ✅ Add Cookie-based Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Home/Login";  // Redirect unauthorized users to login
        options.LogoutPath = "/Home/Logout";
        options.AccessDeniedPath = "/Home/Login";
    });

// ✅ Add Session Services
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Set session timeout
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddControllersWithViews();

var app = builder.Build();

// ✅ Enable Static Files
app.UseStaticFiles();

// ✅ Enable Routing
app.UseRouting();

// ✅ Authentication & Session Middleware
app.UseSession();
app.UseAuthentication();  // Ensure this is called before UseAuthorization()
app.UseAuthorization();  // Ensure this is placed after UseAuthentication() and before endpoints

// ✅ Define Routes
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
