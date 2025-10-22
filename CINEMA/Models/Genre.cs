using System.Collections.Generic;

namespace CINEMA.Models
{
    public partial class Genre
    {
        public int GenreId { get; set; }

        public string? Name { get; set; }

        public string? Description { get; set; }

        // Liên kết nhiều-nhiều với Movie
        public virtual ICollection<Movie> Movies { get; set; } = new List<Movie>();
    }
}
