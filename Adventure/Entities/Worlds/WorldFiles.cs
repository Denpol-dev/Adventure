using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace Adventure.Entities.Worlds
{
    public static class WorldFiles
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            WriteIndented = true
        };

        public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, "Worlds", "default", "world.json");

        public static WorldDefinition Load(string path)
        {
            path = Path.GetFullPath(path);
            var manifest = Read<WorldManifest>(path);
            if (manifest.SchemaVersion != WorldValidator.SchemaVersion)
                throw new InvalidDataException($"Файл '{path}': неподдерживаемая версия мира {manifest.SchemaVersion}.");
            if (manifest.LevelFiles is not { Count: > 0 })
                throw new InvalidDataException($"Файл '{path}': отсутствует список файлов квадрантов.");

            string directory = Path.GetDirectoryName(path)!;
            var levelFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var levels = new List<LevelDefinition>();
            foreach (string file in manifest.LevelFiles)
            {
                if (file == null || !file.StartsWith("levels/", StringComparison.Ordinal) ||
                    !file.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                    file["levels/".Length..].IndexOfAny(['/', '\\', ':']) >= 0)
                {
                    throw new InvalidDataException($"Файл '{path}': некорректный путь к квадранту '{file}'.");
                }
                if (!levelFiles.Add(file))
                    throw new InvalidDataException($"Файл '{path}': повторная ссылка на файл квадранта '{file}'.");

                levels.Add(Read<LevelDefinition>(Path.Combine(directory, file)));
            }

            var world = new WorldDefinition
            {
                Id = manifest.Id,
                SchemaVersion = manifest.SchemaVersion,
                Start = manifest.Start,
                Levels = levels,
                Buildings = manifest.Buildings,
                Metadata = manifest.Metadata
            };
            WorldValidator.Validate(world);
            return world;
        }

        public static void Save(string path, WorldDefinition world)
        {
            WorldValidator.Validate(world);
            path = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(Path.Combine(directory, "levels"));

            var files = new List<string>();
            foreach (var level in world.Levels)
            {
                byte[] json = JsonSerializer.SerializeToUtf8Bytes(level, JsonOptions);
                string hash = Convert.ToHexString(SHA256.HashData(json))[..20].ToLowerInvariant();
                string file = $"levels/{level.Id:N}-{hash}.json";
                WriteAtomically(Path.Combine(directory, file), json);
                files.Add(file);
            }

            var manifest = new WorldManifest
            {
                SchemaVersion = world.SchemaVersion,
                Id = world.Id,
                Start = world.Start,
                LevelFiles = files,
                Buildings = world.Buildings,
                Metadata = world.Metadata
            };
            WriteAtomically(path, JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions));
        }

        private static void WriteAtomically(string path, byte[] json)
        {
            string temporary = Path.Combine(Path.GetDirectoryName(path)!, $".{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllBytes(temporary, json);
                File.Move(temporary, path, overwrite: true);
            }
            finally
            {
                File.Delete(temporary);
            }
        }

        private static T Read<T>(string path) where T : class
        {
            try
            {
                return JsonSerializer.Deserialize<T>(File.ReadAllText(path, Encoding.UTF8), JsonOptions)
                    ?? throw new InvalidDataException($"Файл '{path}': ожидался объект JSON.");
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException($"Некорректный JSON в файле '{path}': {exception.Message}", exception);
            }
            catch (FileNotFoundException exception)
            {
                throw new InvalidDataException($"Файл мира или квадранта не найден: '{path}'.", exception);
            }
            catch (DirectoryNotFoundException exception)
            {
                throw new InvalidDataException($"Файл мира или квадранта не найден: '{path}'.", exception);
            }
        }

        private sealed record WorldManifest
        {
            public required int SchemaVersion { get; init; }
            public required Guid Id { get; init; }
            public required DroneStart Start { get; init; }
            public required List<string> LevelFiles { get; init; }
            public List<BuildingDefinition> Buildings { get; init; } = [];
            public Dictionary<string, JsonElement> Metadata { get; init; } = [];
        }
    }
}
