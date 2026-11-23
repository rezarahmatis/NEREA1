using System.Security.Cryptography;
using System.Text;
using TagLib;
using File = TagLib.File;

namespace NEREA.Core.Services;

public sealed record TrackMetadata
{
    public string Title { get; init; } = "";
    public string Artist { get; init; } = "";
    public string Album { get; init; } = "";
    public string AlbumArtist { get; init; } = "";
    public string Genre { get; init; } = "";
    public int TrackNumber { get; init; }
    public int Year { get; init; }
    public TimeSpan Duration { get; init; }
    public string ArtworkPath { get; init; } = "";
}

/// <summary>
/// Reads file metadata (title, artist, album, artwork, duration) via TagLib#.
/// A single bad file never throws into the import pipeline.
/// </summary>
public sealed class TagMetadataReader
{
    private readonly string _artworkCacheDir;

    public TagMetadataReader(string artworkCacheDir)
    {
        _artworkCacheDir = artworkCacheDir;
        Directory.CreateDirectory(_artworkCacheDir);
    }

    public TrackMetadata Read(string filePath)
    {
        try
        {
            using var file = File.Create(filePath);
            var tag = file.Tag;
            var title = string.IsNullOrWhiteSpace(tag.Title) ? Path.GetFileNameWithoutExtension(filePath) : tag.Title;
            string artwork = "";
            try
            {
                var pic = tag.Pictures?.FirstOrDefault(p => p?.Data?.Data is { Length: > 0 });
                if (pic?.Data?.Data is { Length: > 0 } bytes)
                {
                    var ext = pic.MimeType?.Contains("png", StringComparison.OrdinalIgnoreCase) == true ? "png" : "jpg";
                    artwork = Path.Combine(_artworkCacheDir, HashOf(filePath) + "." + ext);
                    if (!System.IO.File.Exists(artwork))
                        System.IO.File.WriteAllBytes(artwork, bytes);
                }
            }
            catch
            {
                artwork = "";
            }

            return new TrackMetadata
            {
                Title = title!,
                Artist = tag.FirstPerformer ?? "",
                Album = tag.Album ?? "",
                AlbumArtist = tag.AlbumArtists?.FirstOrDefault() ?? tag.FirstPerformer ?? "",
                Genre = tag.FirstGenre ?? "",
                TrackNumber = (int)tag.Track,
                Year = (int)tag.Year,
                Duration = file.Properties?.Duration ?? TimeSpan.Zero,
                ArtworkPath = artwork
            };
        }
        catch
        {
            return new TrackMetadata
            {
                Title = Path.GetFileNameWithoutExtension(filePath),
                Duration = TimeSpan.Zero
            };
        }
    }

    private static string HashOf(string s)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(s).ToLowerInvariant()));
        return Convert.ToHexString(bytes)[..16];
    }
}
