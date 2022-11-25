namespace Konamiman.Nestor80.Assembler
{
    internal class StringCaseInsensitiveComparer : IEqualityComparer<string>
    {
        public static readonly StringCaseInsensitiveComparer Instance = new();

        public bool Equals(string x, string y) => x.Equals(y, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(string obj) => obj.GetHashCode();
    }
}
