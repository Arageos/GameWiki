using GameWiki.Models;
using System.ComponentModel.DataAnnotations;

namespace GameWiki.DTOs.Article
{
    public class BlockInputDto
    {
        public ArticleBlockType Type { get; set; }
        public string? TextContent { get; set; }
        public IFormFile? ImageFile { get; set; }
        public string? ExistingImageUrl { get; set; }  // przy edycji
    }

    public class CreateArticleDto
    {
        public int GameId { get; set; }

        [Required(ErrorMessage = "Tytuł jest wymagany.")]
        [MaxLength(200)]
        public string Title { get; set; }

        public IFormFile? CoverImage { get; set; }

        public List<BlockInputDto>? Blocks { get; set; }
    }

    public class EditArticleDto
    {
        [Required(ErrorMessage = "Tytuł jest wymagany.")]
        [MaxLength(200)]
        public string Title { get; set; }

        public IFormFile? CoverImage { get; set; }

        public List<BlockInputDto>? Blocks { get; set; }
    }
}