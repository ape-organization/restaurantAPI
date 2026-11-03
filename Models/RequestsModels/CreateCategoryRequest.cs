using Microsoft.AspNetCore.Http;

namespace PharmacyAPI.Models.RequestsModels
{
    public class CreateCategoryRequest
    {
        public string NameAr { get; set; } = string.Empty;
        public string NameEn { get; set; } = string.Empty;

        public string? DescriptionAr { get; set; }
        public string? DescriptionEn { get; set; }

        public IFormFile? Image { get; set; }
    }
}
