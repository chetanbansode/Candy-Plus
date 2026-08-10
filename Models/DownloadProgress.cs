namespace YtDlpGui.Models;

public class DownloadProgress
{
    public double Percentage { get; set; }
    public double SpeedBytesPerSec { get; set; }
    public string Eta { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // "downloading", "postprocessing", "finished"
    
    public string SpeedDisplay
    {
        get
        {
            double kbps = SpeedBytesPerSec / 1024.0;
            if (kbps >= 1000)
                return $"{kbps / 1024.0:F1} MB/s";
            return $"{kbps:F1} KB/s";
        }
    }
}
