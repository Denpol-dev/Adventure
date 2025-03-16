﻿using Adventure.Entities.Characters;
using Adventure.Entities.Maps;
using System.Text;

namespace Adventure.Entities.Levels
{
    public class Level1_1 : Level
    {
        public Level1_1() { }

        public override string Name { get; set; } = "N23S55";

        public override Level? TopLevel { get; set; }
        public override Level? BottomLevel { get; set; }
        public override Level? LeftLevel { get; set; }
        public override Level? RightLevel { get; set; }

        public override ConsoleColor LevelColor { get; set; } = ConsoleColor.Black;

        private Map map = new()
        {
            Width = 98,
            Height = 30,
            Cells = GenerateMap(Sprites.Level1_1Sprite)
        };

        public override Map GetMap()
        {
            return map;
        }

        public override void SetMap(Map value)
        {
            map = value;
        }
    }
}
