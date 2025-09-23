using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace MyApp.Host.Controllers
{
    public class LanguageController : Controller
    {
        [HttpPost]
        public IActionResult ChangeLanguage(string culture, string returnUrl)
        {
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    HttpOnly = true,
                    SameSite = SameSiteMode.Lax
                }
            );

            return LocalRedirect(returnUrl ?? "/");
        }

        [HttpGet]
        public IActionResult GetSupportedLanguages()
        {
            var languages = new[]
            {
                new { Code = "en-US", Name = "English", Flag = "🇺🇸" },
                new { Code = "pt-PT", Name = "Português", Flag = "🇵🇹" },
                new { Code = "es-ES", Name = "Español", Flag = "🇪🇸" },
                new { Code = "fr-FR", Name = "Français", Flag = "🇫🇷" }
            };

            return Json(languages);
        }
    }
}
