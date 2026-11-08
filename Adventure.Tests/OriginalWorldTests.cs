using Adventure.Entities;
using Adventure.Entities.Levels;
using Adventure.Entities.Worlds;
using NUnit.Framework;
using System.Security.Cryptography;
using System.Text;

namespace Adventure.Tests
{
    public sealed class OriginalWorldTests
    {
        [TestCase("N23S55", "c65ccaf05c609c33aef69b02255c65afbab4286d107088bf8151b059124200cd", 0)]
        [TestCase("N23S56", "7383f89bb4e9dd679b67f5f5bf8e95eb725bdea75b2493cc1e7203a62b126638", 19)]
        [TestCase("N23S57", "2d147334362fd63db07e6fe1df71a05340ce929484428f407d556ef6d9306d89", 23)]
        [TestCase("N23S58", "a46e3f34f8fa66a0564188683b6783148855ea18facfbc316a7f85e38bee32fd", 0)]
        [TestCase("N24S55", "a46e3f34f8fa66a0564188683b6783148855ea18facfbc316a7f85e38bee32fd", 0)]
        [TestCase("N24S56", "a46e3f34f8fa66a0564188683b6783148855ea18facfbc316a7f85e38bee32fd", 0)]
        [TestCase("N24S57", "a46e3f34f8fa66a0564188683b6783148855ea18facfbc316a7f85e38bee32fd", 0)]
        [TestCase("N24S58", "c724b3d9fa9827ba083d4bf80660807f0221b3db5c2aee8376d03d0369dc7608", 1)]
        [TestCase("N25S55", "c43a17db82e92ccfa2a9bc2400e9902ea8039803d0967fd399c765f8b27a7cf3", 0)]
        [TestCase("N25S56", "328e17fcf1b0d83199572ec30dd2a3ec78c33dd17794331172bf2150eb7c9909", 0)]
        [TestCase("N25S57", "fefa44c8ff71e90d693d92af4f60e3b05068e5fad491ae9cd2e2f59670b9a3a2", 1)]
        [TestCase("N25S58", "1852851ade09170768881d3e6695592b05924b6bf4dadf75bbbf7f71baa3ccfa", 0)]
        [TestCase("N26S55", "b099af2016fe4b9baddf3941405242efdccda4054d9d05a3f1785c475d17b5fa", 0)]
        [TestCase("N26S56", "b099af2016fe4b9baddf3941405242efdccda4054d9d05a3f1785c475d17b5fa", 0)]
        [TestCase("N26S57", "8784f5ca16f0a61a6fb09f94613b6792fe941f024a9f69cd2874cd5acf312349", 21)]
        [TestCase("N26S58", "1fab15e59cc458f00a448fb12f6606339000912c8ee84a67e23392403c5a4dd0", 0)]
        public void OriginalTerrainAndObjectMarkersArePreserved(string name, string expectedHash, int objectCount)
        {
            var level = WorldFiles.Load(WorldFiles.DefaultPath).Levels.Single(level => level.Name == name);
            string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', level.Rows))));
            Assert.Multiple(() =>
            {
                Assert.That((level.Width, level.Height), Is.EqualTo((100, 28)));
                Assert.That(level.Rows, Has.Count.EqualTo(28));
                Assert.That(level.Rows.Select(row => row.Length), Is.All.EqualTo(100));
                Assert.That(hash, Is.EqualTo(expectedHash).IgnoreCase);
                Assert.That(level.Objects, Has.Count.EqualTo(objectCount));
                Assert.That(level.Objects.Select(obj => (obj.X, obj.Y)), Is.Unique);
            });
        }

        [Test]
        public void DefaultWorldBuildsAllSixteenQuadrantsWithOriginalNeighborsAndStart()
        {
            var definition = WorldFiles.Load(WorldFiles.DefaultPath);
            var world = new InitLevels(definition);
            var levels = world.Levels.Values.ToDictionary(level => (level.GlobalX, level.GlobalY));

            Assert.Multiple(() =>
            {
                Assert.That(world.Levels, Has.Count.EqualTo(16));
                Assert.That(world.Levels.Keys, Is.Unique);
                Assert.That(definition.Levels.SelectMany(level => level.Objects).Select(obj => obj.Id), Is.Unique);
                Assert.That(definition.Levels.Sum(level => level.Objects.Count), Is.EqualTo(65));
                Assert.That(world.FirstLevel.Name, Is.EqualTo("N23S55"));
                Assert.That((world.Start.X, world.Start.Y), Is.EqualTo((1, 4)));
            });

            for (int y = 1; y <= 4; y++)
            {
                for (int x = 1; x <= 4; x++)
                {
                    var level = levels[(x, y)];
                    Assert.Multiple(() =>
                    {
                        Assert.That(level.Name, Is.EqualTo($"N{22 + y}S{54 + x}"));
                        Assert.That(level.GetNeighbor(Vector.Top), Is.SameAs(levels.GetValueOrDefault((x, y - 1))));
                        Assert.That(level.GetNeighbor(Vector.Bottom), Is.SameAs(levels.GetValueOrDefault((x, y + 1))));
                        Assert.That(level.GetNeighbor(Vector.Left), Is.SameAs(levels.GetValueOrDefault((x - 1, y))));
                        Assert.That(level.GetNeighbor(Vector.Right), Is.SameAs(levels.GetValueOrDefault((x + 1, y))));
                    });
                }
            }
        }
    }
}
