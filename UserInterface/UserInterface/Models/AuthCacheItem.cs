namespace UserInterface.Models
{
    public class AuthCacheItem
    {
        public string DisplayName { get; set; } = string.Empty;
        public string ConfigKey { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;

        public override string ToString()
        {
            return DisplayName;
        }
    }
}
