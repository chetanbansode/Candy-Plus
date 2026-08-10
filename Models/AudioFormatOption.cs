namespace YtDlpGui.Models;

public class AudioFormatOption
{
    public string FormatId { get; set; } = string.Empty;
    public string Codec { get; set; } = string.Empty;       // lowercase: "opus", "aac", "mp3"
    public string CodecDisplay { get; set; } = string.Empty; // uppercase: "OPUS", "AAC", "MP3"
    public double Bitrate { get; set; }                       // kbps
    public string QualityLabel { get; set; } = string.Empty;  // "Low", "Medium", "High"
    public bool IsRecommended { get; set; }
    public long FileSize { get; set; } // in bytes
    public string FileSizeStr { get; set; } = string.Empty;
    public string DisplayText => $"{CodecDisplay} ({QualityLabel} @{Bitrate:0}kbps)";
}
