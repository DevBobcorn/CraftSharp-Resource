using System;
using System.IO;

namespace CraftSharp.Resource.BedrockEntity
{
    public readonly struct BedrockVersion : IComparable
    {
        public readonly int Major;
        public readonly int Minor;
        public readonly int Build;

        public BedrockVersion(int major, int minor, int build)
        {
            Major = major;
            Minor = minor;
            Build = build;
        }

        public int CompareTo(object obj)
        {
            if (obj is BedrockVersion ver)
            {
                if (Major == ver.Major)
                {
                    if (Minor == ver.Minor)
                    {
                        if (Build == ver.Build)
                        {
                            return 0;
                        }
                        return Build > ver.Build ? 1 : -1;
                    }
                    return Minor > ver.Minor ? 1 : -1;
                }
                return Major > ver.Major ? 1 : -1;
            }
            throw new InvalidDataException("Trying to compare a bedrock object to unknown object!");
        }
    
        public static BedrockVersion FromString(string version)
        {
            var nums = version.Split(".");
            if (nums.Length == 3)
            {
                return new(int.Parse(nums[0]), int.Parse(nums[1]), int.Parse(nums[2]));
            }
            throw new InvalidDataException($"Malformed version string: {version}");
        }

        public static bool operator >(BedrockVersion n1, BedrockVersion n2) => n1.CompareTo(n2) > 0;
        public static bool operator >=(BedrockVersion n1, BedrockVersion n2) => n1.CompareTo(n2) >= 0;
        public static bool operator <(BedrockVersion n1, BedrockVersion n2) => n1.CompareTo(n2) < 0;
        public static bool operator <=(BedrockVersion n1, BedrockVersion n2) => n1.CompareTo(n2) <= 0;

        public override bool Equals(object obj)
        {
            if (obj is not BedrockVersion version) return false;
            return Equals(version);
        }

        public bool Equals(BedrockVersion other)
        {
            return other.Major == Major && other.Minor == Minor && other.Build == Build;
        }

        public override int GetHashCode()
        {
            return Major.GetHashCode() ^ Minor.GetHashCode() ^ Build.GetHashCode();
        }

        public override string ToString()
        {
            return $"[ {Major}, {Minor}, {Build} ]";
        }
    }
}