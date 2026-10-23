using System.ComponentModel.DataAnnotations;
using Npgsql;

namespace PiratePatch.Api.Configuration;

public sealed class DatabaseOptions
{
    public const string Section = "Database";

    /// <summary>Hostname, or a directory for a Unix socket (the NixOS default, with peer authentication).</summary>
    [Required]
    public string Host { get; init; } = "/run/postgresql";

    [Range(1, 65535)]
    public int Port { get; init; } = 5432;

    [Required]
    public string Name { get; init; } = "piratepatch";

    [Required]
    public string User { get; init; } = "piratepatch";

    /// <summary>Not needed for socket peer authentication.</summary>
    public string? Password { get; init; }

    public string ToConnectionString() => new NpgsqlConnectionStringBuilder
    {
        Host = Host,
        Port = Port,
        Database = Name,
        Username = User,
        Password = Password,
        ApplicationName = "piratepatch",
        MaxPoolSize = 20,
    }.ConnectionString;
}
