using Adventure.Entities;
using Adventure.Entities.Actors;
using Adventure.Entities.Levels;
using Adventure.Entities.Worlds;
using NUnit.Framework;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Adventure.Tests
{
    public sealed class WorldFilesTests
    {
        private string _directory = null!;
        private string _path = null!;

        [SetUp]
        public void CreateDirectory()
        {
            _directory = Directory.CreateTempSubdirectory("foxnet-tests-").FullName;
            _path = Path.Combine(_directory, "world.json");
        }

        [TearDown]
        public void RemoveDirectory() => Directory.Delete(_directory, recursive: true);

        [Test]
        public void LoadReportsMissingWorldAndMissingLevelFiles()
        {
            var missingWorld = Assert.Throws<InvalidDataException>(() => WorldFiles.Load(_path))!;
            Assert.That(missingWorld.Message, Does.Contain(_path));

            WorldFiles.Save(_path, World(Level("First", 0, 0, ["..."]), 0, 0));
            string levelPath = Directory.GetFiles(Path.Combine(_directory, "levels")).Single();
            File.Delete(levelPath);

            var missingLevel = Assert.Throws<InvalidDataException>(() => WorldFiles.Load(_path))!;
            Assert.That(missingLevel.Message, Does.Contain(Path.GetFileName(levelPath)));
        }

        [Test]
        public void LoadReportsMalformedJsonWithTheFilePath()
        {
            File.WriteAllText(_path, """{"schemaVersion":""");
            var exception = Assert.Throws<InvalidDataException>(() => WorldFiles.Load(_path))!;
            Assert.That(exception.Message, Does.Contain(_path));
            Assert.That(exception.InnerException, Is.TypeOf<JsonException>());
        }

        [TestCase("duplicate-level-id", "Повторяющийся идентификатор квадранта")]
        [TestCase("duplicate-coordinate", "глобальные координаты")]
        [TestCase("missing-link", "не найден")]
        [TestCase("reversed-link", "отсутствует обратная связь")]
        [TestCase("wrong-size", "не соответствуют размерам")]
        [TestCase("unknown-symbol", "неизвестный символ")]
        [TestCase("unknown-object", "Некорректный JSON")]
        [TestCase("missing-object", "не задан объект нужного типа")]
        [TestCase("duplicate-object", "Повторяющийся идентификатор объекта")]
        [TestCase("invalid-start", "вне карты")]
        [TestCase("blocked-start", "стартовая клетка непроходима")]
        [TestCase("missing-start-level", "Стартовый квадрант")]
        [TestCase("unknown-room", "помещение 'unknown'")]
        [TestCase("unsafe-file", "некорректный путь")]
        public void LoadRejectsInvalidWorldData(string invalidData, string diagnostic)
        {
            var first = Level("First", 0, 0, ["..."]);
            var second = Level("Second", 1, 0, ["..."]);
            first.Neighbors.Add(Vector.Right, second.Id);
            second.Neighbors.Add(Vector.Left, first.Id);
            WorldFiles.Save(_path, World(first, 0, 0, second) with
            {
                Buildings = [new BuildingDefinition { Id = "bunker" }]
            });

            var manifest = JsonNode.Parse(File.ReadAllText(_path))!;
            string firstPath = Path.Combine(_directory, manifest["levelFiles"]![0]!.GetValue<string>());
            string secondPath = Path.Combine(_directory, manifest["levelFiles"]![1]!.GetValue<string>());
            var firstJson = JsonNode.Parse(File.ReadAllText(firstPath))!;
            var secondJson = JsonNode.Parse(File.ReadAllText(secondPath))!;

            switch (invalidData)
            {
                case "duplicate-level-id":
                    secondJson["id"] = first.Id.ToString();
                    break;
                case "duplicate-coordinate":
                    secondJson["globalX"] = first.GlobalX;
                    break;
                case "missing-link":
                    firstJson["neighbors"]!["right"] = Guid.NewGuid().ToString();
                    break;
                case "reversed-link":
                    secondJson["neighbors"] = new JsonObject();
                    break;
                case "wrong-size":
                    firstJson["width"] = 2;
                    break;
                case "unknown-symbol":
                    firstJson["rows"]![0] = ".I.";
                    break;
                case "unknown-object":
                    firstJson["objects"] = JsonNode.Parse("""[{"id":"obj","kind":"spaceship","x":0,"y":0}]""");
                    break;
                case "missing-object":
                    firstJson["rows"]![0] = ".▣.";
                    break;
                case "duplicate-object":
                    firstJson["objects"] = JsonNode.Parse("""[{"id":"same","kind":"chest","x":0,"y":0}]""");
                    secondJson["objects"] = firstJson["objects"]!.DeepClone();
                    break;
                case "invalid-start":
                    manifest["start"]!["x"] = first.Width;
                    break;
                case "blocked-start":
                    firstJson["rows"]![0] = "|..";
                    break;
                case "missing-start-level":
                    manifest["start"]!["levelId"] = Guid.NewGuid().ToString();
                    break;
                case "unknown-room":
                    firstJson["cells"] = JsonNode.Parse("""[{"x":1,"y":0,"buildingId":"bunker","roomId":"unknown"}]""");
                    break;
                case "unsafe-file":
                    manifest["levelFiles"]![0] = "levels/../../other.json";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(invalidData));
            }

            File.WriteAllText(_path, manifest.ToJsonString());
            File.WriteAllText(firstPath, firstJson.ToJsonString());
            File.WriteAllText(secondPath, secondJson.ToJsonString());

            var exception = Assert.Throws<InvalidDataException>(() => WorldFiles.Load(_path))!;
            Assert.That(exception.Message, Does.Contain(diagnostic));
        }

        [Test]
        public void SaveAndLoadPreserveTerrainObjectsAndManualRoomMetadata()
        {
            var first = Level("New world", -3, 8, ["#▢", ". "]) with
            {
                Color = ConsoleColor.DarkBlue,
                Metadata = new() { ["tags"] = JsonSerializer.SerializeToElement(new[] { "research", "ruin" }) },
                Cells = [new CellProperties
                {
                    X = 1, Y = 0, BuildingId = "bunker", RoomId = "lab", BlocksMovement = true, BlocksScanning = false
                }],
                Objects =
                [
                    new ObjectDefinition { Id = "artifact", Kind = ObjectKind.Chest, X = 1, Y = 0, Item = ItemKind.ArtifactY66 },
                    new ObjectDefinition { Id = "sign", Kind = ObjectKind.Sign, X = 1, Y = 0, Message = "Лаборатория" }
                ]
            };
            var world = World(first, 0, 0) with
            {
                Buildings = [new BuildingDefinition { Id = "bunker", Rooms = ["lab"] }],
                Metadata = new() { ["difficulty"] = JsonSerializer.SerializeToElement(2) }
            };
            WorldFiles.Save(_path, world);

            var restored = WorldFiles.Load(_path);
            Assert.That(JsonSerializer.Serialize(restored), Is.EqualTo(JsonSerializer.Serialize(world)));

            var runtime = new InitLevels(restored);
            var cell = runtime.FirstLevel.Map[1, 0];
            Assert.Multiple(() =>
            {
                Assert.That(cell.Symbol, Is.EqualTo('▢'));
                Assert.That(cell.Properties!.RoomId, Is.EqualTo("lab"));
                Assert.That(cell.Properties.BlocksScanning, Is.False);
                Assert.That(cell.CanEnter([]), Is.False);
                Assert.That(cell.Objects.Select(obj => obj.Id), Is.EqualTo(new[] { "artifact", "sign" }));
            });

            ((Chest)cell.Objects[0].Actor).Inventory = null;
            Assert.That(restored.Levels[0].Objects[0].Item, Is.EqualTo(ItemKind.ArtifactY66));
            Assert.That(((Chest)new InitLevels(restored).FirstLevel.Map[1, 0].Objects[0].Actor).Inventory, Is.Not.Null);
        }

        [Test]
        public void EditedWorldCanReplaceTheManifestAfterAddingAndRemovingQuadrants()
        {
            var first = Level("First", 0, 0, ["..."]);
            var removed = Level("Removed", 0, 1, ["..."]);
            var world = World(first, 0, 0, removed);
            WorldFiles.Save(_path, world);

            var edited = WorldFiles.Load(_path);
            edited.Levels.RemoveAt(1);
            var added = Level("Added", -1, 0, ["#.."]);
            added.Neighbors.Add(Vector.Right, first.Id);
            edited.Levels[0].Neighbors.Add(Vector.Left, added.Id);
            edited.Levels.Add(added);
            WorldFiles.Save(_path, edited);

            var reloaded = WorldFiles.Load(_path);
            Assert.That(reloaded.Levels.Select(level => level.Id), Is.EqualTo(new[] { first.Id, added.Id }));
            Assert.That(reloaded.Levels[0].Neighbors[Vector.Left], Is.EqualTo(added.Id));
        }

        internal static LevelDefinition Level(string name, int globalX, int globalY, List<string> rows) => new()
        {
            Id = Guid.NewGuid(), Name = name, GlobalX = globalX, GlobalY = globalY,
            Width = rows[0].Length, Height = rows.Count, Rows = rows
        };

        internal static WorldDefinition World(LevelDefinition first, int startX, int startY, params LevelDefinition[] others) => new()
        {
            SchemaVersion = WorldValidator.SchemaVersion, Id = Guid.NewGuid(), Levels = [first, .. others],
            Start = new DroneStart { LevelId = first.Id, X = startX, Y = startY }
        };
    }
}
