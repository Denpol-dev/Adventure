namespace Adventure.Entities.Levels
{
    public class InitLevels
    {
        public Level1_1 Level11 { get; set; }
        public Level1_2 Level12 { get; set; }
        public Level1_3 Level13 { get; set; }
        public Level1_4 Level14 { get; set; }

        public Level2_1 Level21 { get; set; }
        public Level2_2 Level22 { get; set; }
        public Level2_3 Level23 { get; set; }
        public Level2_4 Level24 { get; set; }

        public Level3_1 Level31 { get; set; }
        public Level3_2 Level32 { get; set; }
        public Level3_3 Level33 { get; set; }
        public Level3_4 Level34 { get; set; }

        public Level4_1 Level41 { get; set; }
        public Level4_2 Level42 { get; set; }
        public Level4_3 Level43 { get; set; }
        public Level4_4 Level44 { get; set; }

        public InitLevels()
        {
            Level11 = new Level1_1();
            Level12 = new Level1_2();
            Level13 = new Level1_3();
            Level14 = new Level1_4();

            Level21 = new Level2_1();
            Level22 = new Level2_2();
            Level23 = new Level2_3();
            Level24 = new Level2_4();

            Level31 = new Level3_1();
            Level32 = new Level3_2();
            Level33 = new Level3_3();
            Level34 = new Level3_4();

            Level41 = new Level4_1();
            Level42 = new Level4_2();
            Level43 = new Level4_3();
            Level44 = new Level4_4();

            Level11.InitDirections(null,    Level21, null,    Level12);
            Level12.InitDirections(null,    Level22, Level11, Level13);
            Level13.InitDirections(null,    Level23, Level12, Level14);
            Level14.InitDirections(null,    Level24, Level13, null   );
            Level21.InitDirections(Level11, Level31, null,    Level22);
            Level22.InitDirections(Level12, Level32, Level21, Level23);
            Level23.InitDirections(Level13, Level33, Level22, Level24);
            Level24.InitDirections(Level14, Level34, Level23, null   );
            Level31.InitDirections(Level21, Level41, null,    Level32);
            Level32.InitDirections(Level22, Level42, Level31, Level33);
            Level33.InitDirections(Level23, Level43, Level32, Level34);
            Level34.InitDirections(Level24, Level44, Level33, null   );
            Level41.InitDirections(Level31, null,    null,    Level42);
            Level42.InitDirections(Level32, null,    Level41, Level43);
            Level43.InitDirections(Level33, null,    Level42, Level44);
            Level44.InitDirections(Level34, null,    Level43, null   );
        }
    }
}
