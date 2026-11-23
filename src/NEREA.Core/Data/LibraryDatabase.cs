using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using NEREA.Core.Models;

namespace NEREA.Core.Data;

/// <summary>
/// SQLite-backed music library storage. All access is serialized through a
/// single connection; the database lives in the local application data folder.
/// A corrupt or unreadable database is quarantined and recreated instead of
/// blocking startup.
/// </summary>
public sealed class LibraryDatabase : IDisposable
{
    private SqliteConnection _connection;
    private readonly object _sync = new();

    public LibraryDatabase(string databasePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        try
        {
            _connection = OpenAndInitialize(databasePath);
        }
        catch (SqliteException)
        {
            // Corrupt, locked or unreadable database: never block startup.
            // Quarantine the old file and start with a fresh library.
            QuarantineDatabase(databasePath);
            _connection = OpenAndInitialize(databasePath);
        }
    }

    private static SqliteConnection OpenAndInitialize(string databasePath)
    {
        var connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();
        try
        {
            Initialize(connection);
        }
        catch
        {
            connection.Dispose();
            throw;
        }
        return connection;
    }

    private static void Initialize(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS songs (
                id TEXT PRIMARY KEY,
                path TEXT UNIQUE NOT NULL,
                title TEXT NOT NULL,
                artist TEXT NOT NULL DEFAULT '',
                album TEXT NOT NULL DEFAULT '',
                album_artist TEXT NOT NULL DEFAULT '',
                genre TEXT NOT NULL DEFAULT '',
                track_number INTEGER NOT NULL DEFAULT 0,
                year INTEGER NOT NULL DEFAULT 0,
                duration_seconds REAL NOT NULL DEFAULT 0,
                file_size INTEGER NOT NULL DEFAULT 0,
                artwork_path TEXT NOT NULL DEFAULT '',
                favorite INTEGER NOT NULL DEFAULT 0,
                play_count INTEGER NOT NULL DEFAULT 0,
                last_played_utc TEXT,
                added_utc TEXT,
                mtime_utc TEXT
            );
            CREATE INDEX IF NOT EXISTS idx_songs_title ON songs(title);
            CREATE INDEX IF NOT EXISTS idx_songs_artist ON songs(artist);
            CREATE INDEX IF NOT EXISTS idx_songs_album ON songs(album);
            CREATE TABLE IF NOT EXISTS playlists (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                created_utc TEXT
            );
            CREATE TABLE IF NOT EXISTS playlist_items (
                playlist_id TEXT NOT NULL,
                track_id TEXT NOT NULL,
                position INTEGER NOT NULL,
                PRIMARY KEY (playlist_id, position)
            );
            CREATE TABLE IF NOT EXISTS folders (
                path TEXT PRIMARY KEY,
                added_utc TEXT
            );
            """;
        cmd.ExecuteNonQuery();
    }

    private static void QuarantineDatabase(string databasePath)
    {
        try
        {
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            if (File.Exists(databasePath))
                File.Move(databasePath, $"{databasePath}.corrupt-{stamp}.bak");
            foreach (var suffix in new[] { "-wal", "-shm" })
                if (File.Exists(databasePath + suffix))
                    File.Move(databasePath + suffix, $"{databasePath}{suffix}.corrupt-{stamp}.bak");
        }
        catch
        {
            // If the quarantine fails, the original error surfaces on retry.
        }
    }

    public static string TrackIdForPath(string path)
    {
        var norm = Path.GetFullPath(path).ToLowerInvariant();
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(norm));
        return Convert.ToHexString(bytes)[..24];
    }

    public void UpsertTrack(Track t)
    {
        lock (_sync)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO songs (id, path, title, artist, album, album_artist, genre, track_number, year,
                                   duration_seconds, file_size, artwork_path, favorite, play_count,
                                   last_played_utc, added_utc, mtime_utc)
                VALUES ($id, $path, $title, $artist, $album, $albumArtist, $genre, $track, $year,
                        $duration, $size, $art, $fav, $plays, $last, $added, $mtime)
                ON CONFLICT (id) DO UPDATE SET
                    path=$path, title=$title, artist=$artist, album=$album, album_artist=$albumArtist,
                    genre=$genre, track_number=$track, year=$year, duration_seconds=$duration,
                    file_size=$size, artwork_path=$art, mtime_utc=$mtime
                """;
            Fill(cmd, t);
            cmd.ExecuteNonQuery();
        }
    }

    private static void Fill(SqliteCommand cmd, Track t)
    {
        cmd.Parameters.AddWithValue("$id", t.Id);
        cmd.Parameters.AddWithValue("$path", t.Path);
        cmd.Parameters.AddWithValue("$title", t.Title);
        cmd.Parameters.AddWithValue("$artist", t.Artist);
        cmd.Parameters.AddWithValue("$album", t.Album);
        cmd.Parameters.AddWithValue("$albumArtist", t.AlbumArtist);
        cmd.Parameters.AddWithValue("$genre", t.Genre);
        cmd.Parameters.AddWithValue("$track", t.TrackNumber);
        cmd.Parameters.AddWithValue("$year", t.Year);
        cmd.Parameters.AddWithValue("$duration", t.Duration.TotalSeconds);
        cmd.Parameters.AddWithValue("$size", t.FileSize);
        cmd.Parameters.AddWithValue("$art", t.ArtworkPath);
        cmd.Parameters.AddWithValue("$fav", t.Favorite ? 1 : 0);
        cmd.Parameters.AddWithValue("$plays", t.PlayCount);
        cmd.Parameters.AddWithValue("$last", t.LastPlayedUtc?.ToString("o"));
        cmd.Parameters.AddWithValue("$added", t.AddedUtc.ToString("o"));
        cmd.Parameters.AddWithValue("$mtime", t.ModifiedUtc.ToString("o"));
    }

    public List<Track> GetAllTracks()
    {
        lock (_sync)
        {
            var list = new List<Track>();
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM songs";
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(Read(r));
            return list;
        }
    }

    public Track? GetTrack(string id)
    {
        lock (_sync)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM songs WHERE id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            using var r = cmd.ExecuteReader();
            return r.Read() ? Read(r) : null;
        }
    }

    public bool ContainsPath(string path)
    {
        lock (_sync)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(1) FROM songs WHERE path = $p";
            cmd.Parameters.AddWithValue("$p", Path.GetFullPath(path));
            return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
        }
    }

    public void DeleteTrack(string id)
    {
        lock (_sync)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "DELETE FROM songs WHERE id = $id; DELETE FROM playlist_items WHERE track_id = $id;";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }
    }

    public void SetFavorite(string id, bool favorite)
    {
        lock (_sync)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "UPDATE songs SET favorite = $v WHERE id = $id";
            cmd.Parameters.AddWithValue("$v", favorite ? 1 : 0);
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }
    }

    public void RecordPlayback(string id)
    {
        lock (_sync)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "UPDATE songs SET play_count = play_count + 1, last_played_utc = $t WHERE id = $id";
            cmd.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("o"));
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }
    }

    // ── Playlists ────────────────────────────────────────────────────────────

    public List<Playlist> GetPlaylists()
    {
        lock (_sync)
        {
            var result = new List<Playlist>();
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = "SELECT id, name, created_utc FROM playlists ORDER BY created_utc";
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    result.Add(new Playlist
                    {
                        Id = r.GetString(0),
                        Name = r.GetString(1),
                        CreatedUtc = DateTime.TryParse(r.IsDBNull(2) ? null : r.GetString(2), out var d) ? d : DateTime.UtcNow
                    });
                }
            }
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = "SELECT playlist_id, track_id, position FROM playlist_items ORDER BY playlist_id, position";
                using var r = cmd.ExecuteReader();
                var map = result.ToDictionary(p => p.Id);
                while (r.Read())
                {
                    var pid = r.GetString(0);
                    if (map.TryGetValue(pid, out var pl))
                        pl.TrackIds.Add(r.GetString(1));
                }
            }
            return result;
        }
    }

    public void SavePlaylist(Playlist p)
    {
        lock (_sync)
        {
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = """
                    INSERT INTO playlists (id, name, created_utc) VALUES ($id, $name, $created)
                    ON CONFLICT (id) DO UPDATE SET name = $name
                    """;
                cmd.Parameters.AddWithValue("$id", p.Id);
                cmd.Parameters.AddWithValue("$name", p.Name);
                cmd.Parameters.AddWithValue("$created", p.CreatedUtc.ToString("o"));
                cmd.ExecuteNonQuery();
            }
            using (var del = _connection.CreateCommand())
            {
                del.CommandText = "DELETE FROM playlist_items WHERE playlist_id = $id";
                del.Parameters.AddWithValue("$id", p.Id);
                del.ExecuteNonQuery();
            }
            using (var ins = _connection.CreateCommand())
            {
                ins.CommandText = "INSERT INTO playlist_items (playlist_id, track_id, position) VALUES ($p, $t, $pos)";
                var pp = ins.Parameters.Add("$p", SqliteType.Text);
                var tt = ins.Parameters.Add("$t", SqliteType.Text);
                var pos = ins.Parameters.Add("$pos", SqliteType.Integer);
                for (int i = 0; i < p.TrackIds.Count; i++)
                {
                    pp.Value = p.Id;
                    tt.Value = p.TrackIds[i];
                    pos.Value = i;
                    ins.ExecuteNonQuery();
                }
            }
        }
    }

    public void DeletePlaylist(string id)
    {
        lock (_sync)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "DELETE FROM playlists WHERE id = $id; DELETE FROM playlist_items WHERE playlist_id = $id;";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }
    }

    // ── Folder roots ─────────────────────────────────────────────────────────

    public void AddFolderRoot(string path)
    {
        lock (_sync)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO folders (path, added_utc) VALUES ($p, $t)";
            cmd.Parameters.AddWithValue("$p", Path.GetFullPath(path));
            cmd.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("o"));
            cmd.ExecuteNonQuery();
        }
    }

    public List<string> GetFolderRoots()
    {
        lock (_sync)
        {
            var list = new List<string>();
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT path FROM folders ORDER BY added_utc";
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(r.GetString(0));
            return list;
        }
    }

    public void RemoveFolderRoot(string path)
    {
        lock (_sync)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "DELETE FROM folders WHERE path = $p";
            cmd.Parameters.AddWithValue("$p", Path.GetFullPath(path));
            cmd.ExecuteNonQuery();
        }
    }

    private static Track Read(SqliteDataReader r) => new()
    {
        Id = r.GetString(r.GetOrdinal("id")),
        Path = r.GetString(r.GetOrdinal("path")),
        Title = r.GetString(r.GetOrdinal("title")),
        Artist = r.GetString(r.GetOrdinal("artist")),
        Album = r.GetString(r.GetOrdinal("album")),
        AlbumArtist = r.GetString(r.GetOrdinal("album_artist")),
        Genre = r.GetString(r.GetOrdinal("genre")),
        TrackNumber = Convert.ToInt32(r["track_number"]),
        Year = Convert.ToInt32(r["year"]),
        Duration = TimeSpan.FromSeconds(Convert.ToDouble(r["duration_seconds"])),
        FileSize = Convert.ToInt64(r["file_size"]),
        ArtworkPath = r.GetString(r.GetOrdinal("artwork_path")),
        Favorite = Convert.ToInt32(r["favorite"]) != 0,
        PlayCount = Convert.ToInt32(r["play_count"]),
        LastPlayedUtc = DateTime.TryParse(r.IsDBNull(r.GetOrdinal("last_played_utc")) ? null : r.GetString(r.GetOrdinal("last_played_utc")), out var lp) ? lp : null,
        AddedUtc = DateTime.TryParse(r.IsDBNull(r.GetOrdinal("added_utc")) ? null : r.GetString(r.GetOrdinal("added_utc")), out var ad) ? ad : DateTime.UtcNow,
        ModifiedUtc = DateTime.TryParse(r.IsDBNull(r.GetOrdinal("mtime_utc")) ? null : r.GetString(r.GetOrdinal("mtime_utc")), out var mt) ? mt : DateTime.UtcNow
    };

    public void Dispose() => _connection.Dispose();
}
