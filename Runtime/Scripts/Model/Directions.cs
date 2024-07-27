namespace CraftSharp.Resource
{
    public enum FaceDir
    {
        UP, DOWN, NORTH, SOUTH, WEST, EAST
    }

    public enum CullDir
    {
        NONE, UP, DOWN, SOUTH, NORTH, EAST, WEST
    }

    public static class Directions
    {
        public static FaceDir FaceDirFromName(string name)
        {
            return name.ToLower() switch
            {
                "up"    => FaceDir.UP,
                "down"  => FaceDir.DOWN,
                "north" => FaceDir.NORTH,
                "south" => FaceDir.SOUTH,
                "east"  => FaceDir.EAST,
                "west"  => FaceDir.WEST,
                _       => FaceDir.UP
            };
        }

        public static CullDir CullDirFromName(string name)
        {
            return name.ToLower() switch
            {
                "up"    => CullDir.UP,
                "down"  => CullDir.DOWN,
                "north" => CullDir.NORTH,
                "south" => CullDir.SOUTH,
                "east"  => CullDir.EAST,
                "west"  => CullDir.WEST,
                "none"  => CullDir.NONE,
                _       => CullDir.NONE
            };
        }

        public static CullDir CullDirFromFaceDir(FaceDir dir)
        {
            return dir switch
            {
                FaceDir.UP    => CullDir.UP,
                FaceDir.DOWN  => CullDir.DOWN,
                FaceDir.NORTH => CullDir.NORTH,
                FaceDir.SOUTH => CullDir.SOUTH,
                FaceDir.EAST  => CullDir.EAST,
                FaceDir.WEST  => CullDir.WEST,

                _             => CullDir.UP
            };
        }
    }
}
