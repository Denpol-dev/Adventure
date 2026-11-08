using Adventure.Entities;
using Adventure.Entities.Actors;
using Adventure.Entities.Characters;
using Adventure.Entities.Items;
using Adventure.Entities.Levels;
using Adventure.Entities.Worlds;
using NUnit.Framework;

namespace Adventure.Tests
{
    public sealed class GameplayTests
    {
        [TestCase(Vector.Left, Vector.Right, -1, 0, 0, 1, 2, 1)]
        [TestCase(Vector.Right, Vector.Left, 1, 0, 2, 1, 0, 1)]
        [TestCase(Vector.Top, Vector.Bottom, 0, -1, 1, 0, 1, 2)]
        [TestCase(Vector.Bottom, Vector.Top, 0, 1, 1, 2, 1, 0)]
        public void MovementCrossesOnlyAfterReachingTheEdgeAndKeepsTheOtherCoordinate(
            Vector direction, Vector reverse, int dx, int dy, int edgeX, int edgeY, int arrivalX, int arrivalY)
        {
            var (world, first, second) = TwoLevels(direction, reverse, dx, dy);
            var drone = new Character(new InitLevels(world));
            Assert.That(drone.Move(direction), Is.EqualTo(MoveResult.Moved));
            Assert.That((drone.Level.Id, drone.X, drone.Y), Is.EqualTo((first.Id, edgeX, edgeY)));

            Assert.That(drone.Move(direction), Is.EqualTo(MoveResult.ChangedLevel));
            Assert.That((drone.Level.Id, drone.X, drone.Y), Is.EqualTo((second.Id, arrivalX, arrivalY)));

            Assert.That(drone.Move(reverse), Is.EqualTo(MoveResult.ChangedLevel));
            Assert.That((drone.Level.Id, drone.X, drone.Y), Is.EqualTo((first.Id, edgeX, edgeY)));
        }

        [Test]
        public void ObstaclesAndWorldLimitsLeaveTheDroneInPlace()
        {
            var (world, first, second) = TwoLevels(Vector.Right, Vector.Left, 1, 0);
            first.Rows[0] = "8|.";
            second.Rows[1] = "+..";
            var drone = new Character(new InitLevels(world));

            Assert.That(drone.Move(Vector.Top), Is.EqualTo(MoveResult.Blocked));
            Assert.That(drone.Move(Vector.Left), Is.EqualTo(MoveResult.Moved));
            Assert.That(drone.Move(Vector.Top), Is.EqualTo(MoveResult.Blocked));
            Assert.That(drone.Move(Vector.Left), Is.EqualTo(MoveResult.NoNeighbor));
            Assert.That((drone.Level.Id, drone.X, drone.Y), Is.EqualTo((first.Id, 0, 1)));

            Assert.That(drone.Move(Vector.Right), Is.EqualTo(MoveResult.Moved));
            Assert.That(drone.Move(Vector.Right), Is.EqualTo(MoveResult.Moved));
            Assert.That(drone.Move(Vector.Right), Is.EqualTo(MoveResult.Blocked));
            Assert.That((drone.Level.Id, drone.X, drone.Y), Is.EqualTo((first.Id, 2, 1)));
        }

        [Test]
        public void NeighborWithShorterEdgeDoesNotWrapOrClampTheEntry()
        {
            var (world, first, second) = TwoLevels(Vector.Right, Vector.Left, 1, 0);
            world.Levels[1] = second with { Height = 1, Rows = ["..."] };
            var drone = new Character(new InitLevels(world));

            Assert.That(drone.Move(Vector.Right), Is.EqualTo(MoveResult.Moved));
            Assert.That(drone.Move(Vector.Right), Is.EqualTo(MoveResult.Blocked));
            Assert.That((drone.Level.Id, drone.X, drone.Y), Is.EqualTo((first.Id, 2, 1)));
        }

        [Test]
        public void ForceFieldsRequireTheirConfiguredItems()
        {
            var world = new InitLevels(WorldFiles.Load(WorldFiles.DefaultPath));
            var blue = world.Levels.Values.Single(level => level.Name == "N23S57").Map;
            var red = world.Levels.Values.Single(level => level.Name == "N26S57").Map;
            var artifactGate = (from y in Enumerable.Range(0, blue.Height)
                                from x in Enumerable.Range(0, blue.Width)
                                where blue[x, y].Symbol == '≈'
                                select blue[x, y]).First();
            var passGate = red[21, 16];

            Assert.Multiple(() =>
            {
                Assert.That(artifactGate.CanEnter([]), Is.False);
                Assert.That(artifactGate.CanEnter([new ArtifactY66()]), Is.True);
                Assert.That(artifactGate.CanEnter([new UniversalPass()]), Is.False);
                Assert.That(passGate.CanEnter([]), Is.False);
                Assert.That(passGate.CanEnter([new UniversalPass()]), Is.True);
                Assert.That(passGate.CanEnter([new ArtifactY66()]), Is.False);
            });
        }

        [Test]
        public void DroneCanRecoverTheDiskReadItAndUploadTheData()
        {
            var world = new InitLevels(WorldFiles.Load(WorldFiles.DefaultPath));
            var drone = new Character(world);

            Navigate(drone, "N23S56", 47, 11);
            Assert.That(drone.Interact().Result, Is.EqualTo(InteractionResult.InventoryChanged));
            Assert.That(drone.Inventory.Items.Single(), Is.TypeOf<HardDriveXD23>());
            Assert.That(drone.Interact().Result, Is.EqualTo(InteractionResult.EmptyChest));

            Navigate(drone, "N23S57", 57, 2);
            Assert.That(drone.Interact().Result, Is.EqualTo(InteractionResult.InventoryChanged));
            Assert.That(drone.Inventory.Items, Has.Some.TypeOf<ArtifactY66>());

            Navigate(drone, "N23S57", 29, 15);
            var alien = drone.Interact();
            Assert.That(alien.Result, Is.EqualTo(InteractionResult.InventoryChanged));
            Assert.That(alien.Actor, Is.TypeOf<Terminal>());
            Assert.That(drone.Inventory.Items, Has.Some.TypeOf<HardDriveXD23Full>());
            Assert.That(drone.Inventory.Items, Has.None.TypeOf<HardDriveXD23>());
            Assert.That(drone.Interact().Result, Is.EqualTo(InteractionResult.TerminalOff));

            Navigate(drone, "N24S58", 45, 15);
            Assert.That(drone.Interact().Result, Is.EqualTo(InteractionResult.InventoryChanged));
            Assert.That(drone.Inventory.Items, Has.Some.TypeOf<UniversalPass>());

            Navigate(drone, "N25S57", 72, 22);
            Assert.That(drone.Interact().Result, Is.EqualTo(InteractionResult.Completed));
            Assert.That(drone.Inventory.Items, Has.Some.TypeOf<HardDriveXD23>());
            Assert.That(drone.Inventory.Items, Has.None.TypeOf<HardDriveXD23Full>());
            Assert.That(drone.Interact().Result, Is.EqualTo(InteractionResult.None));
        }

        private static (WorldDefinition World, LevelDefinition First, LevelDefinition Second) TwoLevels(
            Vector direction, Vector reverse, int dx, int dy)
        {
            var first = WorldFilesTests.Level("First", 0, 0, ["...", "...", "..."]);
            var second = WorldFilesTests.Level("Second", dx, dy, ["...", "...", "..."]);
            first.Neighbors.Add(direction, second.Id);
            second.Neighbors.Add(reverse, first.Id);

            return (WorldFilesTests.World(first, 1, 1, second), first, second);
        }

        private static void Navigate(Character drone, string name, int x, int y)
        {
            var start = (drone.Level, drone.X, drone.Y);
            var visited = new HashSet<(Level, int, int)> { start };
            var queue = new Queue<((Level Level, int X, int Y) Cell, List<Vector> Path)>();
            queue.Enqueue((start, []));
            (Vector Direction, int X, int Y)[] steps =
                [(Vector.Left, -1, 0), (Vector.Right, 1, 0), (Vector.Top, 0, -1), (Vector.Bottom, 0, 1)];

            while (queue.TryDequeue(out var entry))
            {
                var (level, currentX, currentY) = entry.Cell;
                if (level.Name == name && currentX == x && currentY == y)
                {
                    foreach (var direction in entry.Path)
                        Assert.That(drone.Move(direction), Is.EqualTo(MoveResult.Moved).Or.EqualTo(MoveResult.ChangedLevel));
                    Assert.That((drone.Level.Name, drone.X, drone.Y), Is.EqualTo((name, x, y)));
                    return;
                }

                foreach (var (direction, dx, dy) in steps)
                {
                    int nextX = currentX + dx;
                    int nextY = currentY + dy;
                    var next = level;
                    if (!level.Map.Contains(nextX, nextY))
                    {
                        if (level.GetNeighbor(direction) is not Level neighbor)
                            continue;
                        next = neighbor;
                        nextX = (nextX + next.Map.Width) % next.Map.Width;
                        nextY = (nextY + next.Map.Height) % next.Map.Height;
                    }

                    if (next.Map[nextX, nextY].CanEnter(drone.Inventory.Items) && visited.Add((next, nextX, nextY)))
                        queue.Enqueue(((next, nextX, nextY), [.. entry.Path, direction]));
                }
            }

            Assert.Fail($"No route from {start.Level.Name} to {name} ({x}, {y}).");
        }
    }
}
