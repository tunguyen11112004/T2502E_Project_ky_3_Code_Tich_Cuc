namespace Bus_ticket.Models;

public class NewsArticle
{
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Author { get; set; } = "SRC Travel";
    public string ImageUrl { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }
    public int ReadingMinutes { get; set; }
    public List<string> Paragraphs { get; set; } = new();
    public List<string> Highlights { get; set; } = new();
}