namespace YtDlpGui.Models;

public class ManualFormatOption
{
    public string FormatId { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public string Resolution { get; set; } = string.Empty;     // e.g. "1080p", "audio only"
    public string Fps { get; set; } = string.Empty;            // e.g. "60", "30"
    public string Codec { get; set; } = string.Empty;           // e.g. "avc1", "opus", "vp9"
    public double Bitrate { get; set; }                          // kbps
    public string FileSize { get; set; } = string.Empty;        // e.g. "~50MB"
    public long FileSizeRaw { get; set; }                       // in bytes
    public string Type { get; set; } = string.Empty;            // "video", "audio", "video+audio"
    public string Note { get; set; } = string.Empty;            // e.g. "DASH video", "Default"
    public bool IsSelected { get; set; }

    public bool IsSelectedVideo { get; set; }
    public bool IsSelectedAudio { get; set; }
    public bool IsVideoFormat => Type.Contains("video") || (!Type.Contains("audio") && Resolution != "audio only");
    public bool IsAudioFormat => (Type.Contains("audio") || Resolution == "audio only") && !Type.Contains("video");

    public string DisplayText
    {
        get
        {
            var parts = new System.Collections.Generic.List<string>();
            parts.Add($"[{FormatId}]");
            if (!string.IsNullOrEmpty(Extension)) parts.Add(Extension);
            if (!string.IsNullOrEmpty(Resolution)) parts.Add(Resolution);
            if (!string.IsNullOrEmpty(Codec)) parts.Add(Codec);
            if (Bitrate > 0) parts.Add($"{Bitrate:0}k");
            if (!string.IsNullOrEmpty(FileSize)) parts.Add(FileSize);
            if (!string.IsNullOrEmpty(Note)) parts.Add($"({Note})");
            return string.Join("  │  ", parts);
        }
    }
}
