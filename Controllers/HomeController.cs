using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using ImageGallery.Models;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
namespace ImageGallery.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IMongoCollection<ImageModel> _images;
        private readonly IMongoCollection<UserModel> _users;

        public HomeController(ILogger<HomeController> logger, IMongoClient client)
        {
            _logger = logger;
            var database = client.GetDatabase("ImageGallery");
            _images = database.GetCollection<ImageModel>("Images");
            _users = database.GetCollection<UserModel>("Users");
        }


        // Helper method to get the logged-in user's role
        private async Task<string> GetUserRole()
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return "Guest"; // Default to Guest if not logged in

            var user = await _users.Find(u => u.Username == username).FirstOrDefaultAsync();
            return user?.Role ?? "Guest"; // Default to Guest if role is not found
        }


        // Index page, accessible without login
        public async Task<IActionResult> Index()
        {
            var username = User.Identity?.Name;
            if (!string.IsNullOrEmpty(username))
            {
                var user = await _users.Find(u => u.Username == username).FirstOrDefaultAsync();
                ViewBag.UserRole = user?.Role ?? "Guest";  // Default role as Guest
            }
            else
            {
                ViewBag.UserRole = "Guest"; // If not logged in, assume Guest
            }

            var images = _images.Find(image => true).ToList();
            return View(images);
        }



        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        // Create page (requires authentication)
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var role = await GetUserRole();
            if (role == "Guest")
            {
                return Content("<script>alert('You do not have permission to add images. Please contact the admin.'); window.history.back();</script>", "text/html");

            }

            return View();
        }


        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Create(ImageModel image, IFormFile imageFile)
        {
            var role = await GetUserRole();
            if (role == "Guest")
            {
                return Content("<script>alert('You do not have permission to add images. Please contact the admin.'); window.history.back();</script>", "text/html");

            }

            if (imageFile != null && imageFile.Length > 0)
            {
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images", imageFile.FileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(stream);
                }
                image.Url = "/images/" + imageFile.FileName;
            }

            await _images.InsertOneAsync(image);
            return RedirectToAction("Index");
        }

        // Login functionality (authentication)
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(UserModel user)
        {
            if (string.IsNullOrEmpty(user.Password))
            {
                ModelState.AddModelError("Password", "Password is required.");
                return View(user);
            }

            var hashedPassword = HashPassword(user.Password);

            var existingUser = await _users.Find(u => u.Username == user.Username && u.Password == hashedPassword).FirstOrDefaultAsync();

            if (existingUser != null)
            {
                // Set session data for username (Ensure it's not null)
                if (!string.IsNullOrEmpty(existingUser.Username))
                {
                    HttpContext.Session.SetString("Username", existingUser.Username);
                }

                var claims = new List<Claim>
                        {
                            new Claim(ClaimTypes.Name, existingUser.Username ?? "Unknown")
                        };


                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = true
                };

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);

                return RedirectToAction("Index");
            }

            ModelState.AddModelError("", "Invalid username or password.");
            return View();
        }

        // Register functionality with password hashing
        // Register functionality with password hashing
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(UserModel user)
        {
            if (ModelState.IsValid)
            {
                // Username validation: At least 4 characters, must contain at least one number
                if (string.IsNullOrEmpty(user.Username) || user.Username.Length < 4 || !user.Username.Any(char.IsDigit))
                {
                    ModelState.AddModelError("Username", "Username must be at least 4 characters long and contain at least one number.");
                    return View(user);
                }

                // Password validation: At least 8 characters, must contain uppercase, lowercase, special character, and number
                var passwordRegex = new Regex(@"^(?=.*[A-Z])(?=.*[a-z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,15}$");
                if (string.IsNullOrEmpty(user.Password) || !passwordRegex.IsMatch(user.Password))
                {
                    ModelState.AddModelError("Password", "Password must be between 8 and 15 characters long, and contain at least one uppercase letter, one lowercase letter, one special character, and one number.");
                    return View(user);
                }

                // Check if the username already exists in the database
                var existingUser = await _users.Find(u => u.Username == user.Username).FirstOrDefaultAsync();
                if (existingUser != null)
                {
                    ModelState.AddModelError("Username", "Username is already taken.");
                    return View(user);
                }

                // If username is unique, hash the password
                user.Password = HashPassword(user.Password);

                // Set the default role as Guest
                if (string.IsNullOrEmpty(user.Role))
                {
                    user.Role = "Guest";  // Default to "Guest" role if no role is provided
                }

                // Save user to MongoDB
                await _users.InsertOneAsync(user);

                return RedirectToAction("Login");
            }

            return View(user);
        }




        // Logout functionality
        public async Task<IActionResult> Logout()
        {
            HttpContext.Session.Remove("Username"); // Remove the username from session
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme); // Sign out
            return RedirectToAction("Login");
        }

        // Method to hash password using SHA-256
        private string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentException("Password cannot be null or empty", nameof(password));
            }

            using (var sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(bytes);
            }
        }

        // Edit GET: Display the edit form
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            var role = await GetUserRole();
            if (role == "Guest")
            {
                return Content("<script>alert('You do not have permission to Edit images. Please contact the admin.'); window.history.back();</script>", "text/html");

            }


            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            ObjectId objectId;

            // Try to parse the id to ObjectId if necessary
            if (!ObjectId.TryParse(id, out objectId))
            {
                return BadRequest("Invalid ObjectId format");
            }

            var image = await _images.Find(i => i.Id == objectId).FirstOrDefaultAsync();

            if (image == null)
            {
                return NotFound();
            }

            return View(image);
        }

        // Edit POST: Save the edited image data
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Edit(string id, ImageModel image, IFormFile imageFile)
        {
            var role = await GetUserRole();
            if (role == "Guest")
            {
                return Content("<script>alert('You do not have permission to Edit images. Please contact the admin.'); window.history.back();</script>", "text/html");

            }


            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            ObjectId objectId;

            // Try to parse the id to ObjectId if necessary
            if (!ObjectId.TryParse(id, out objectId))
            {
                return BadRequest("Invalid ObjectId format");
            }

            if (objectId != image.Id)
            {
                return BadRequest("Mismatch between id and image Id");
            }

            // Fetch the existing image details
            var existingImage = await _images.Find(i => i.Id == objectId).FirstOrDefaultAsync();

            if (existingImage == null)
            {
                return NotFound();
            }

            // Start with an empty update definition
            var updateDefinition = Builders<ImageModel>.Update.Set(i => i.Id, objectId);

            // If the title has changed, update it
            if (!string.IsNullOrEmpty(image.Title) && image.Title != existingImage.Title)
            {
                updateDefinition = updateDefinition.Set(i => i.Title, image.Title);
            }

            // If the description has changed, update it
            if (!string.IsNullOrEmpty(image.Description) && image.Description != existingImage.Description)
            {
                updateDefinition = updateDefinition.Set(i => i.Description, image.Description);
            }

            // If a new image is uploaded, update the image URL
            if (imageFile != null)
            {

                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images", imageFile.FileName);

                // Save the new image file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(stream);
                }

                // Update the image URL
                updateDefinition = updateDefinition.Set(i => i.Url, "/images/" + imageFile.FileName);
            }

            // Perform the update
            var result = await _images.UpdateOneAsync(
                Builders<ImageModel>.Filter.Eq(i => i.Id, objectId),  // Filter to find the image by ID
                updateDefinition  // Apply the update definition
            );

            if (result.ModifiedCount == 0)
            {
                return NotFound();  // If no documents were updated, show NotFound
            }

            // Redirect to the Index page after successful update
            return RedirectToAction("Index");
        }




        // POST: Confirm deletion and delete the image
        [Authorize]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var role = await GetUserRole();
            if (role != "Admin")
            {
                return Content("<script>alert('You do not have permission to Delete images. Please contact the admin.'); window.history.back();</script>", "text/html");

            }

            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            ObjectId objectId;

            // Try to parse the id to ObjectId if necessary
            if (!ObjectId.TryParse(id, out objectId))
            {
                return BadRequest("Invalid ObjectId format");
            }

            var image = await _images.Find(i => i.Id == objectId).FirstOrDefaultAsync();

            if (image == null)
            {
                return NotFound();
            }

            // Delete the image from MongoDB
            await _images.DeleteOneAsync(i => i.Id == objectId);

            return RedirectToAction("Index");  // Redirect to the index page after deletion
        }

    }
}
