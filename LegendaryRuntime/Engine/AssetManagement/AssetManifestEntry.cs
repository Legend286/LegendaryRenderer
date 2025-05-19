using System;

namespace LegendaryRenderer.LegendaryRuntime.Engine.AssetManagement
{
    public class AssetManifestEntry
    {
        public string OriginalFilePathHint { get; set; }
        public string BinaryAssetPath { get; set; }
        public DateTime LastCompiledUtc { get; set; }
        public string OriginalFileHash { get; set; } // Hash of the original file's content to detect changes
    }
} 