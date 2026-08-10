namespace YtDlpGui.Models;

public class VideoFormatOption
{
    public string FormatId { get; set; } = string.Empty;
    public int Height { get; set; }                              // raw height: 1080, 720, etc.
    public string Resolution { get; set; } = string.Empty;      // "1080p", "720p"
    public string ResolutionLabel { get; set; } = string.Empty;  // "1080p FHD", "720p HD"
    public int Fps { get; set; }                                 // 30, 60
    public string Codec { get; set; } = string.Empty;           // raw vcodec string e.g. "avc1.640028"
    public double Bitrate { get; set; }                          // kbps (tbr or vbr)
    
    public long FileSize { get; set; } // in bytes
    public string FileSizeStr { get; set; } = string.Empty;
}
