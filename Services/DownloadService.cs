using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using YtDlpGui.Models;

namespace YtDlpGui.Services;

public class DownloadService
{
    private Process? _currentProcess;
    
    public static bool IsFFmpegAvailable()
    {
        var executableName = "ffmpeg.exe";
        var toolsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", executableName);
        if (File.Exists(toolsPath)) return true;

        var basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, executableName);
        if (File.Exists(basePath)) return true;
        
        var currentDir = Path.Combine(Directory.GetCurrentDirectory(), executableName);
        if (File.Exists(currentDir)) return true;

        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (pathEnv != null)
        {
            foreach (var path in pathEnv.Split(Path.PathSeparator))
            {
                try
                {
                    if (File.Exists(Path.Combine(path, executableName))) return true;
                }
                catch { }
            }
        }
        return false;
    }

    private string GetExecutablePath(string executableName)
    {
        var toolsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", executableName);
        if (File.Exists(toolsPath))
            return toolsPath;

        var basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, executableName);
        if (File.Exists(basePath))
            return basePath;
            
        var currentDir = Path.Combine(Directory.GetCurrentDirectory(), executableName);
        if (File.Exists(currentDir))
            return currentDir;
            
        return executableName;
    }

    public async Task<List<AudioFormatOption>> GetAudioFormatsAsync(string url)
    {
        var ytDlpPath = GetExecutablePath("yt-dlp.exe");
        var ffmpegPath = GetExecutablePath("ffmpeg.exe"); string ffmpegArg = ffmpegPath != "ffmpeg.exe" ? $" --ffmpeg-location \"{Path.GetDirectoryName(ffmpegPath)}\"" : "";

        var startInfo = new ProcessStartInfo
        {
            FileName = ytDlpPath,
            Arguments = $"--dump-json --no-warnings{ffmpegArg} \"{url}\"",
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = new Process { StartInfo = startInfo };
        
        try
        {
            process.Start();
            
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            
            await Task.WhenAll(stdoutTask, stderrTask);
            await process.WaitForExitAsync();
            
            var json = stdoutTask.Result;
            if (string.IsNullOrWhiteSpace(json))
            {
                var err = stderrTask.Result;
                if (!string.IsNullOrWhiteSpace(err))
                    throw new Exception(ParseErrorMessage(err));
                throw new Exception("Failed to extract video information (no output).");
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("formats", out var formatsProp) || formatsProp.ValueKind != JsonValueKind.Array)
                return new List<AudioFormatOption>();

            var options = new List<AudioFormatOption>();

            foreach (var format in formatsProp.EnumerateArray())
            {
                if (format.TryGetProperty("acodec", out var acodecProp))
                {
                    var acodec = acodecProp.GetString();
                    if (acodec == "none" || string.IsNullOrEmpty(acodec))
                        continue;


                    if (format.TryGetProperty("video_ext", out var vExtProp))
                    {
                        if (vExtProp.ValueKind == JsonValueKind.String)
                        {
                            var videoExt = vExtProp.GetString();
                            if (videoExt != "none" && !string.IsNullOrEmpty(videoExt))
                                continue;
                        }
                    }

                    var formatId = format.TryGetProperty("format_id", out var idProp) ? idProp.GetString() ?? "" : "";
                    
                    double bitrate = 0;
                    if (format.TryGetProperty("abr", out var abrProp) && abrProp.ValueKind == JsonValueKind.Number)
                    {
                        bitrate = abrProp.GetDouble();
                    }
                    if (bitrate == 0 && format.TryGetProperty("tbr", out var tbrProp) && tbrProp.ValueKind == JsonValueKind.Number)
                    {
                        bitrate = tbrProp.GetDouble();
                    }

                    string codec = "unknown";
                    if (acodec.Contains("opus", StringComparison.OrdinalIgnoreCase)) codec = "opus";
                    else if (acodec.Contains("mp4a", StringComparison.OrdinalIgnoreCase) || acodec.Contains("aac", StringComparison.OrdinalIgnoreCase)) codec = "aac";
                    else if (acodec.Contains("mp3", StringComparison.OrdinalIgnoreCase)) codec = "mp3";
                    else codec = acodec;

                    string quality = "Medium";
                    if (bitrate <= 70) quality = "Low";
                    else if (bitrate > 160) quality = "High";

                    long fileSize = 0;
                    if (format.TryGetProperty("filesize", out var fsProp) && fsProp.ValueKind == JsonValueKind.Number)
                        fileSize = fsProp.GetInt64();
                    else if (format.TryGetProperty("filesize_approx", out var fsaProp) && fsaProp.ValueKind == JsonValueKind.Number)
                        fileSize = fsaProp.GetInt64();

                    string fileSizeStr = "";
                    if (fileSize > 0)
                    {
                        fileSizeStr = fileSize >= 1_073_741_824 ? $"~{fileSize / 1_073_741_824.0:F1}GB" :
                                      fileSize >= 1_048_576 ? $"~{fileSize / 1_048_576.0:F1}MB" :
                                      $"~{fileSize / 1024.0:F0}KB";
                    }

                    options.Add(new AudioFormatOption
                    {
                        FormatId = formatId,
                        Codec = codec,
                        CodecDisplay = codec.ToUpper(),
                        Bitrate = bitrate,
                        QualityLabel = quality,
                        FileSize = fileSize,
                        FileSizeStr = fileSizeStr
                    });
                }
            }

            // Set recommended
            var bestOpus = options.Where(o => o.Codec == "opus").OrderByDescending(o => o.Bitrate).FirstOrDefault();
            if (bestOpus != null)
                bestOpus.IsRecommended = true;

            return options
                .OrderBy(o => o.Codec == "opus" ? 0 : 1)
                .ThenByDescending(o => o.Bitrate)
                .ToList();
        }
        catch (Exception ex)
        {
            // If it's already a parsed exception, it'll just carry over
            throw new Exception(ex.Message);
        }
    }

    private static (string Resolution, string ResolutionLabel) GetResolutionInfo(int height) => height switch
    {
        >= 4320 => ("4320p", "8K"),
        >= 2160 => ("2160p", "4K"),
        >= 1440 => ("1440p", "2K"),
        >= 1080 => ("1080p", "1080p FHD"),
        >= 720 => ("720p", "720p HD"),
        >= 480 => ("480p", "480p SD"),
        >= 360 => ("360p", "360p SD"),
        >= 240 => ("240p", "240p LQ"),
        _ => ($"{height}p", "144p LQ"),
    };

    public async Task<(List<VideoFormatOption> VideoFormats, string BestAudioFormatId)> GetVideoFormatsAsync(string url)
    {
        var ytDlpPath = GetExecutablePath("yt-dlp.exe");
        var ffmpegPath = GetExecutablePath("ffmpeg.exe"); string ffmpegArg = ffmpegPath != "ffmpeg.exe" ? $" --ffmpeg-location \"{Path.GetDirectoryName(ffmpegPath)}\"" : "";

        var startInfo = new ProcessStartInfo
        {
            FileName = ytDlpPath,
            Arguments = $"--dump-json --no-warnings{ffmpegArg} \"{url}\"",
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = new Process { StartInfo = startInfo };
        
        try
        {
            process.Start();
            
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            
            await Task.WhenAll(stdoutTask, stderrTask);
            await process.WaitForExitAsync();
            
            var json = stdoutTask.Result;
            if (string.IsNullOrWhiteSpace(json))
            {
                var err = stderrTask.Result;
                if (!string.IsNullOrWhiteSpace(err))
                    throw new Exception(ParseErrorMessage(err));
                throw new Exception("Failed to extract video information (no output).");
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("formats", out var formatsProp) || formatsProp.ValueKind != JsonValueKind.Array)
                return (new List<VideoFormatOption>(), "");

            var options = new List<VideoFormatOption>();
            string bestAudioFormatId = "";
            double bestAudioBitrateDist = double.MaxValue;
            string backupAudioFormatId = "";

            foreach (var format in formatsProp.EnumerateArray())
            {
                string? vcodec = null;
                if (format.TryGetProperty("vcodec", out var vProp) && vProp.ValueKind == JsonValueKind.String)
                    vcodec = vProp.GetString();
                if (string.IsNullOrEmpty(vcodec) && format.TryGetProperty("video_ext", out var vExtProp) && vExtProp.ValueKind == JsonValueKind.String)
                    vcodec = vExtProp.GetString();
                if (string.IsNullOrEmpty(vcodec)) vcodec = "none";

                string? acodec = null;
                if (format.TryGetProperty("acodec", out var aProp) && aProp.ValueKind == JsonValueKind.String)
                    acodec = aProp.GetString();
                if (string.IsNullOrEmpty(acodec)) acodec = "none";

                var formatId = format.TryGetProperty("format_id", out var idProp) ? idProp.GetString() ?? "" : "";

                double bitrate = 0;
                if (format.TryGetProperty("tbr", out var tbrProp) && tbrProp.ValueKind == JsonValueKind.Number)
                    bitrate = tbrProp.GetDouble();
                else if (format.TryGetProperty("vbr", out var vbrProp) && vbrProp.ValueKind == JsonValueKind.Number)
                    bitrate = vbrProp.GetDouble();
                else if (format.TryGetProperty("abr", out var abrProp) && abrProp.ValueKind == JsonValueKind.Number)
                    bitrate = abrProp.GetDouble();

                // Video-only formats
                if (vcodec != "none" && !string.IsNullOrEmpty(vcodec) && (acodec == "none" || string.IsNullOrEmpty(acodec)))
                {
                    int height = 0;
                    if (format.TryGetProperty("height", out var hProp) && hProp.ValueKind == JsonValueKind.Number)
                        height = hProp.GetInt32();

                    if (height == 0) continue;

                    int fps = 0;
                    if (format.TryGetProperty("fps", out var fProp) && fProp.ValueKind == JsonValueKind.Number)
                        fps = (int)fProp.GetDouble();

                    var (resolution, resLabel) = GetResolutionInfo(height);

                    long fileSize = 0;
                    if (format.TryGetProperty("filesize", out var fsProp) && fsProp.ValueKind == JsonValueKind.Number)
                        fileSize = fsProp.GetInt64();
                    else if (format.TryGetProperty("filesize_approx", out var fsaProp) && fsaProp.ValueKind == JsonValueKind.Number)
                        fileSize = fsaProp.GetInt64();

                    string fileSizeStr = "";
                    if (fileSize > 0)
                    {
                        fileSizeStr = fileSize >= 1_073_741_824 ? $"~{fileSize / 1_073_741_824.0:F1}GB" :
                                      fileSize >= 1_048_576 ? $"~{fileSize / 1_048_576.0:F1}MB" :
                                      $"~{fileSize / 1024.0:F0}KB";
                    }

                    options.Add(new VideoFormatOption
                    {
                        FormatId = formatId,
                        Height = height,
                        Resolution = resolution,
                        ResolutionLabel = resLabel,
                        Fps = fps,
                        Codec = vcodec,
                        Bitrate = bitrate,
                        FileSize = fileSize,
                        FileSizeStr = fileSizeStr
                    });
                }

                // Best audio format
                if (acodec != "none" && !string.IsNullOrEmpty(acodec) && (vcodec == "none" || string.IsNullOrEmpty(vcodec)))
                {
                    if (string.IsNullOrEmpty(backupAudioFormatId))
                        backupAudioFormatId = formatId;

                    if (acodec.Contains("mp4a", StringComparison.OrdinalIgnoreCase) || acodec.Contains("aac", StringComparison.OrdinalIgnoreCase))
                    {
                        double dist = Math.Abs(bitrate - 128);
                        if (dist < bestAudioBitrateDist)
                        {
                            bestAudioBitrateDist = dist;
                            bestAudioFormatId = formatId;
                        }
                    }
                    else if (string.IsNullOrEmpty(bestAudioFormatId))
                    {
                        backupAudioFormatId = formatId;
                    }
                }
            }

            if (string.IsNullOrEmpty(bestAudioFormatId))
                bestAudioFormatId = backupAudioFormatId;

            return (options
                .OrderByDescending(o => o.Height)
                .ThenByDescending(o => o.Fps)
                .ThenByDescending(o => o.Bitrate)
                .ToList(), bestAudioFormatId);
        }
        catch (Exception ex)
        {
            throw new Exception(ex.Message);
        }
    }

    public async Task<List<ManualFormatOption>> GetAllFormatsAsync(string url)
    {
        var ytDlpPath = GetExecutablePath("yt-dlp.exe");
        var ffmpegPath = GetExecutablePath("ffmpeg.exe"); string ffmpegArg = ffmpegPath != "ffmpeg.exe" ? $" --ffmpeg-location \"{Path.GetDirectoryName(ffmpegPath)}\"" : "";

        var startInfo = new ProcessStartInfo
        {
            FileName = ytDlpPath,
            Arguments = $"--dump-json --no-warnings{ffmpegArg} \"{url}\"",
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            await Task.WhenAll(stdoutTask, stderrTask);
            await process.WaitForExitAsync();

            var json = stdoutTask.Result;
            if (string.IsNullOrWhiteSpace(json))
            {
                var err = stderrTask.Result;
                if (!string.IsNullOrWhiteSpace(err))
                    throw new Exception(ParseErrorMessage(err));
                throw new Exception("Failed to extract video information (no output).");
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("formats", out var formatsProp) || formatsProp.ValueKind != JsonValueKind.Array)
                return new List<ManualFormatOption>();

            var options = new List<ManualFormatOption>();

            foreach (var format in formatsProp.EnumerateArray())
            {
                var formatId = format.TryGetProperty("format_id", out var idProp) ? idProp.GetString() ?? "" : "";
                var ext = format.TryGetProperty("ext", out var extProp) ? extProp.GetString() ?? "" : "";

                string? vcodec = null;
                if (format.TryGetProperty("vcodec", out var vProp) && vProp.ValueKind == JsonValueKind.String)
                    vcodec = vProp.GetString();
                if (string.IsNullOrEmpty(vcodec)) vcodec = "none";

                string? acodec = null;
                if (format.TryGetProperty("acodec", out var aProp) && aProp.ValueKind == JsonValueKind.String)
                    acodec = aProp.GetString();
                if (string.IsNullOrEmpty(acodec)) acodec = "none";

                // Determine type
                bool hasVideo = vcodec != "none";
                bool hasAudio = acodec != "none";
                string type = hasVideo && hasAudio ? "video+audio" : hasVideo ? "video" : hasAudio ? "audio" : "unknown";
                if (type == "unknown") continue;

                // Resolution
                string resolution = "audio only";
                string fpsStr = "";
                if (hasVideo)
                {
                    int height = 0;
                    if (format.TryGetProperty("height", out var hProp) && hProp.ValueKind == JsonValueKind.Number)
                        height = hProp.GetInt32();
                    int fps = 0;
                    if (format.TryGetProperty("fps", out var fProp) && fProp.ValueKind == JsonValueKind.Number)
                        fps = (int)fProp.GetDouble();
                    resolution = height > 0 ? $"{height}p" : "unknown";
                    if (fps > 0) fpsStr = fps.ToString();
                }

                // Codec display
                string codec = hasVideo ? vcodec : acodec;

                // Bitrate
                double bitrate = 0;
                if (format.TryGetProperty("tbr", out var tbrProp) && tbrProp.ValueKind == JsonValueKind.Number)
                    bitrate = tbrProp.GetDouble();
                else if (format.TryGetProperty("vbr", out var vbrProp) && vbrProp.ValueKind == JsonValueKind.Number)
                    bitrate = vbrProp.GetDouble();
                else if (format.TryGetProperty("abr", out var abrProp) && abrProp.ValueKind == JsonValueKind.Number)
                    bitrate = abrProp.GetDouble();

                // File size
                string fileSize = "";
                long fileSizeRaw = 0;
                if (format.TryGetProperty("filesize", out var fsProp) && fsProp.ValueKind == JsonValueKind.Number)
                {
                    var bytes = fsProp.GetInt64();
                    fileSizeRaw = bytes;
                    fileSize = bytes >= 1_073_741_824 ? $"~{bytes / 1_073_741_824.0:F1}GB" :
                               bytes >= 1_048_576 ? $"~{bytes / 1_048_576.0:F1}MB" :
                               $"~{bytes / 1024.0:F0}KB";
                }
                else if (format.TryGetProperty("filesize_approx", out var fsaProp) && fsaProp.ValueKind == JsonValueKind.Number)
                {
                    var bytes = fsaProp.GetInt64();
                    fileSizeRaw = bytes;
                    fileSize = bytes >= 1_073_741_824 ? $"~{bytes / 1_073_741_824.0:F1}GB" :
                               bytes >= 1_048_576 ? $"~{bytes / 1_048_576.0:F1}MB" :
                               $"~{bytes / 1024.0:F0}KB";
                }

                // Note
                string note = "";
                if (format.TryGetProperty("format_note", out var noteProp) && noteProp.ValueKind == JsonValueKind.String)
                    note = noteProp.GetString() ?? "";

                options.Add(new ManualFormatOption
                {
                    FormatId = formatId,
                    Extension = ext,
                    Resolution = resolution,
                    Fps = fpsStr,
                    Codec = codec,
                    Bitrate = bitrate,
                    FileSize = fileSize,
                    FileSizeRaw = fileSizeRaw,
                    Type = type,
                    Note = note
                });
            }

            return options;
        }
        catch (Exception ex)
        {
            throw new Exception(ex.Message);
        }
    }

    public async Task DownloadVideoAsync(string url, string videoFormatId, string audioFormatId, string outputPath, string container, IProgress<DownloadProgress> progress, CancellationToken cancellationToken)
    {
        var settings = new SettingsService().Load();
        
        var ytDlpPath = GetExecutablePath("yt-dlp.exe");
        var ffmpegPath = GetExecutablePath("ffmpeg.exe");
        string ffmpegArg = ffmpegPath != "ffmpeg.exe" ? $" --ffmpeg-location \"{Path.GetDirectoryName(ffmpegPath)}\"" : "";

        outputPath = outputPath.TrimEnd('\\', '/');
        var tempPath = Path.Combine(Path.GetTempPath(), "Candy", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempPath);

        string ytHomePath = tempPath; // Always download to temp to protect original file on cancel
        string extraArgs = "";

        var args = $"--newline --progress -f {videoFormatId}+{audioFormatId} --merge-output-format {container.ToLower()} -P \"home:{ytHomePath}\" -P \"temp:{tempPath}\" -o \"%(title)s.%(ext)s\"{ffmpegArg}{extraArgs}";

        if (settings.SpeedLimit == "2MB/s") args += " --limit-rate 2M";
        else if (settings.SpeedLimit == "5MB/s") args += " --limit-rate 5M";
        else if (settings.SpeedLimit == "10MB/s") args += " --limit-rate 10M";

        args += $" \"{url}\"";

        var startInfo = new ProcessStartInfo
        {
            FileName = ytDlpPath,
            Arguments = args,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        _currentProcess = new Process { StartInfo = startInfo };

        string? destinationPath = null;

        using var ctr = cancellationToken.Register(() => Cancel());

        try
        {
            _currentProcess.Start();

            string stderrOutput = "";
            _ = Task.Run(async () =>
            {
                try { stderrOutput = await _currentProcess.StandardError.ReadToEndAsync(); } catch { }
            });

            var regex = new Regex(@"\[download\]\s+([\d.]+)%\s+of\s+\S+\s+at\s+([\d.]+)(\w+)/s\s+ETA\s+(\S+)");
            var extractingRegex = new Regex(@"\[ExtractAudio\]");
            var destRegex = new Regex(@"\[(?:download|ExtractAudio|Merger)\] (?:Destination: |Merging formats into "")([^""]+)");

            string fullStdout = "";

            await Task.Run(async () =>
            {
                var reader = _currentProcess.StandardOutput;
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    fullStdout += line + "\n";
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    var destMatch = destRegex.Match(line);
                    if (destMatch.Success)
                    {
                        destinationPath = destMatch.Groups[1].Value.Trim();
                    }

                    var match = regex.Match(line);
                    if (match.Success)
                    {
                        if (double.TryParse(match.Groups[1].Value, out double percent) &&
                            double.TryParse(match.Groups[2].Value, out double speedValue))
                        {
                            string unit = match.Groups[3].Value;
                            double speedBytes = speedValue;
                            if (unit.Equals("KiB", StringComparison.OrdinalIgnoreCase)) speedBytes *= 1024;
                            else if (unit.Equals("MiB", StringComparison.OrdinalIgnoreCase)) speedBytes *= 1024 * 1024;
                            else if (unit.Equals("GiB", StringComparison.OrdinalIgnoreCase)) speedBytes *= 1024 * 1024 * 1024;

                            progress?.Report(new DownloadProgress
                            {
                                Percentage = percent,
                                SpeedBytesPerSec = speedBytes,
                                Eta = match.Groups[4].Value,
                                Status = "downloading"
                            });
                        }
                    }
                    else if (line.Contains("[download] 100%"))
                    {
                        progress?.Report(new DownloadProgress
                        {
                            Percentage = 100,
                            SpeedBytesPerSec = 0,
                            Eta = "00:00",
                            Status = "downloading"
                        });
                    }
                    else if (extractingRegex.IsMatch(line) || line.Contains("Post-process") || line.Contains("Destination:") || line.Contains("[Merger]"))
                    {
                        progress?.Report(new DownloadProgress
                        {
                            Percentage = 100,
                            SpeedBytesPerSec = 0,
                            Eta = "00:00",
                            Status = "postprocessing"
                        });
                    }
                }
            });

            await _currentProcess.WaitForExitAsync(cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            if (_currentProcess.ExitCode == 0)
            {
                if (Directory.Exists(tempPath))
                {
                    var files = Directory.GetFiles(tempPath).Where(f => !f.EndsWith(".part") && !f.EndsWith(".ytdl")).ToArray();
                    if (files.Length > 0)
                    {
                        var downloadedFile = files[0];
                        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(downloadedFile);
                        var ext = Path.GetExtension(downloadedFile);
                        var finalPath = Path.Combine(outputPath, Path.GetFileName(downloadedFile));
                        
                        if (settings.FileExistsAction != "Overwrite")
                        {
                            int counter = 1;
                            while (File.Exists(finalPath))
                            {
                                finalPath = Path.Combine(outputPath, $"{fileNameWithoutExt} ({counter}){ext}");
                                counter++;
                            }
                        }
                        
                        File.Move(downloadedFile, finalPath, true);
                    }
                }

                progress?.Report(new DownloadProgress
                {
                    Percentage = 100,
                    SpeedBytesPerSec = 0,
                    Eta = "00:00",
                    Status = "finished"
                });
            }
            else
            {
                var errMessage = string.IsNullOrWhiteSpace(stderrOutput) ? $"Process exited with code {_currentProcess.ExitCode}" : ParseErrorMessage(stderrOutput);
                throw new Exception(errMessage);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            throw;
        }
        finally
        {
            try
            {
                if (_currentProcess != null && !_currentProcess.HasExited)
                {
                    _currentProcess.Kill(true);
                }
            }
            catch { }
            
            _currentProcess?.Dispose();
            _currentProcess = null;

            Task.Run(async () =>
            {
                for (int i = 0; i < 5; i++)
                {
                    await Task.Delay(1000);
                    try 
                    { 
                        if (Directory.Exists(tempPath)) 
                            Directory.Delete(tempPath, true); 
                        break; 
                    } 
                    catch { }
                }
            });
        }
    }

    private string ParseErrorMessage(string rawError)
    {
        if (string.IsNullOrWhiteSpace(rawError)) return "An unknown error occurred.";
        
        var lowerError = rawError.ToLower();
        
        if (lowerError.Contains("truncated") || lowerError.Contains("incomplete youtube id") || lowerError.Contains("not a valid url"))
            return "Invalid video URL. Please check the link and try again.";
            
        if (lowerError.Contains("region") || lowerError.Contains("country") || lowerError.Contains("private video") || lowerError.Contains("video unavailable"))
            return "This video is unavailable. It might be private, deleted, or region-restricted.";
            
        if (lowerError.Contains("sign in to confirm") || lowerError.Contains("bot") || lowerError.Contains("429"))
            return "YouTube blocked the request (likely anti-bot protection). Please try again later.";

        if (lowerError.Contains("no space left on device") || lowerError.Contains("not enough space"))
            return "Not enough disk space to download this video.";

        if (lowerError.Contains("404") || lowerError.Contains("not found"))
            return "Invalid video URL. Please check the link and try again.";
            
        if (lowerError.Contains("failed to resolve") || lowerError.Contains("name or service not known") || lowerError.Contains("network is unreachable") || lowerError.Contains("httpsconnection") || lowerError.Contains("connection reset by peer") || lowerError.Contains("timeout"))
            return "Connection Lost. Please check your internet connection.";
            
        return "An unknown error occurred during download. Please try again.";
    }

    public async Task DownloadAudioAsync(string url, string formatId, string outputPath, IProgress<DownloadProgress> progress, CancellationToken cancellationToken)
    {
        var settings = new SettingsService().Load();
        
        var ytDlpPath = GetExecutablePath("yt-dlp.exe");
        var ffmpegPath = GetExecutablePath("ffmpeg.exe");
        string ffmpegArg = ffmpegPath != "ffmpeg.exe" ? $" --ffmpeg-location \"{Path.GetDirectoryName(ffmpegPath)}\"" : "";

        outputPath = outputPath.TrimEnd('\\', '/');
        var tempPath = Path.Combine(Path.GetTempPath(), "Candy", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempPath);

        string ytHomePath = tempPath; // Always download to temp to protect original file on cancel
        string extraArgs = "";

        var args = $"--newline --progress -f {formatId} -x -P \"home:{ytHomePath}\" -P \"temp:{tempPath}\" -o \"%(title)s.%(ext)s\"{ffmpegArg}{extraArgs}";

        if (settings.EmbedThumbnail) args += " --embed-thumbnail";
        if (settings.EmbedMetadata) args += " --add-metadata";
        
        if (settings.AudioConversion == "MP3") args += " --audio-format mp3 --audio-quality 0";
        else if (settings.AudioConversion == "FLAC") args += " --audio-format flac";
        
        if (settings.SpeedLimit == "2MB/s") args += " --limit-rate 2M";
        else if (settings.SpeedLimit == "5MB/s") args += " --limit-rate 5M";
        else if (settings.SpeedLimit == "10MB/s") args += " --limit-rate 10M";

        args += $" \"{url}\"";

        var startInfo = new ProcessStartInfo
        {
            FileName = ytDlpPath,
            Arguments = args,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        _currentProcess = new Process { StartInfo = startInfo };

        string? destinationPath = null;

        using var ctr = cancellationToken.Register(() => Cancel());

        try
        {
            _currentProcess.Start();

            // Drain stderr on a background thread to prevent deadlocks
            string stderrOutput = "";
            _ = Task.Run(async () =>
            {
                try { stderrOutput = await _currentProcess.StandardError.ReadToEndAsync(); } catch { }
            });

            var regex = new Regex(@"\[download\]\s+([\d.]+)%\s+of\s+\S+\s+at\s+([\d.]+)(\w+)/s\s+ETA\s+(\S+)");
            var extractingRegex = new Regex(@"\[ExtractAudio\]");
            var destRegex = new Regex(@"\[(?:download|ExtractAudio|Merger)\] (?:Destination: |Merging formats into "")([^""]+)");

            string fullStdout = "";

            // Read stdout on a background thread to avoid blocking the UI
            await Task.Run(async () =>
            {
                var reader = _currentProcess.StandardOutput;
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    fullStdout += line + "\n";
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    var destMatch = destRegex.Match(line);
                    if (destMatch.Success)
                    {
                        destinationPath = destMatch.Groups[1].Value.Trim();
                    }

                    var match = regex.Match(line);
                    if (match.Success)
                    {
                        if (double.TryParse(match.Groups[1].Value, out double percent) &&
                            double.TryParse(match.Groups[2].Value, out double speedValue))
                        {
                            string unit = match.Groups[3].Value;
                            double speedBytes = speedValue;
                            if (unit.Equals("KiB", StringComparison.OrdinalIgnoreCase)) speedBytes *= 1024;
                            else if (unit.Equals("MiB", StringComparison.OrdinalIgnoreCase)) speedBytes *= 1024 * 1024;
                            else if (unit.Equals("GiB", StringComparison.OrdinalIgnoreCase)) speedBytes *= 1024 * 1024 * 1024;

                            progress?.Report(new DownloadProgress
                            {
                                Percentage = percent,
                                SpeedBytesPerSec = speedBytes,
                                Eta = match.Groups[4].Value,
                                Status = "downloading"
                            });
                        }
                    }
                    else if (line.Contains("[download] 100%"))
                    {
                        progress?.Report(new DownloadProgress
                        {
                            Percentage = 100,
                            SpeedBytesPerSec = 0,
                            Eta = "00:00",
                            Status = "downloading"
                        });
                    }
                    else if (extractingRegex.IsMatch(line) || line.Contains("Post-process") || line.Contains("Destination:"))
                    {
                        progress?.Report(new DownloadProgress
                        {
                            Percentage = 100,
                            SpeedBytesPerSec = 0,
                            Eta = "00:00",
                            Status = "postprocessing"
                        });
                    }
                }
            });

            await _currentProcess.WaitForExitAsync(cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            if (_currentProcess.ExitCode == 0)
            {
                // For Auto-Rename, the final file is sitting in the tempPath
                if (Directory.Exists(tempPath))
                {
                    var files = Directory.GetFiles(tempPath).Where(f => !f.EndsWith(".part") && !f.EndsWith(".ytdl")).ToArray();
                    if (files.Length > 0)
                    {
                        var downloadedFile = files[0];
                        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(downloadedFile);
                        var ext = Path.GetExtension(downloadedFile);
                        var finalPath = Path.Combine(outputPath, Path.GetFileName(downloadedFile));
                        
                        if (settings.FileExistsAction != "Overwrite")
                        {
                            int counter = 1;
                            while (File.Exists(finalPath))
                            {
                                finalPath = Path.Combine(outputPath, $"{fileNameWithoutExt} ({counter}){ext}");
                                counter++;
                            }
                        }
                        
                        File.Move(downloadedFile, finalPath, true);
                    }
                }

                progress?.Report(new DownloadProgress
                {
                    Percentage = 100,
                    SpeedBytesPerSec = 0,
                    Eta = "00:00",
                    Status = "finished"
                });
            }
            else
            {
                var errMessage = string.IsNullOrWhiteSpace(stderrOutput) ? $"Process exited with code {_currentProcess.ExitCode}" : ParseErrorMessage(stderrOutput);
                throw new Exception(errMessage);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            throw;
        }
        finally
        {
            try
            {
                if (_currentProcess != null && !_currentProcess.HasExited)
                {
                    _currentProcess.Kill(true);
                }
            }
            catch { }
            
            _currentProcess?.Dispose();
            _currentProcess = null;

            // Cleanup the isolated temporary directory
            Task.Run(async () =>
            {
                for (int i = 0; i < 5; i++)
                {
                    await Task.Delay(1000);
                    try 
                    { 
                        if (Directory.Exists(tempPath)) 
                            Directory.Delete(tempPath, true); 
                        break; 
                    } 
                    catch { }
                }
            });
        }
    }

    public async Task DownloadManualAsync(string url, string formatSelection, string outputPath, string? proxyUrl, string? cookiesFilePath, bool useCookies, bool embedSubtitles, string? extraArguments, IProgress<DownloadProgress> progress, CancellationToken cancellationToken)
    {
        var settings = new SettingsService().Load();

        var ytDlpPath = GetExecutablePath("yt-dlp.exe");
        var ffmpegPath = GetExecutablePath("ffmpeg.exe");
        string ffmpegArg = ffmpegPath != "ffmpeg.exe" ? $" --ffmpeg-location \"{Path.GetDirectoryName(ffmpegPath)}\"" : "";

        outputPath = outputPath.TrimEnd('\\', '/');
        var tempPath = Path.Combine(Path.GetTempPath(), "Candy", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempPath);

        string ytHomePath = tempPath; // Always download to temp to protect original file on cancel
        string extraArgs = "";

        // Build format selection
        var args = $"--newline --progress -f {formatSelection} -P \"home:{ytHomePath}\" -P \"temp:{tempPath}\" -o \"%(title)s.%(ext)s\"{ffmpegArg}{extraArgs}";

        // Proxy
        if (!string.IsNullOrWhiteSpace(proxyUrl))
            args += $" --proxy \"{proxyUrl}\"";

        // Cookies
        if (useCookies && !string.IsNullOrWhiteSpace(cookiesFilePath) && File.Exists(cookiesFilePath))
            args += $" --cookies \"{cookiesFilePath}\"";

        // Subtitles (creator-uploaded only, not auto-generated)
        if (embedSubtitles)
            args += " --write-subs --embed-subs --no-write-auto-subs";

        // Speed limit
        if (settings.SpeedLimit == "2MB/s") args += " --limit-rate 2M";
        else if (settings.SpeedLimit == "5MB/s") args += " --limit-rate 5M";
        else if (settings.SpeedLimit == "10MB/s") args += " --limit-rate 10M";

        // User-injected arguments
        if (!string.IsNullOrWhiteSpace(extraArguments))
            args += $" {extraArguments.Trim()}";

        args += $" \"{url}\"";

        var startInfo = new ProcessStartInfo
        {
            FileName = ytDlpPath,
            Arguments = args,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        _currentProcess = new Process { StartInfo = startInfo };

        using var ctr = cancellationToken.Register(() => Cancel());

        try
        {
            _currentProcess.Start();

            string stderrOutput = "";
            _ = Task.Run(async () =>
            {
                try { stderrOutput = await _currentProcess.StandardError.ReadToEndAsync(); } catch { }
            });

            var regex = new Regex(@"\[download\]\s+([\d.]+)%\s+of\s+\S+\s+at\s+([\d.]+)(\w+)/s\s+ETA\s+(\S+)");

            await Task.Run(async () =>
            {
                var reader = _currentProcess.StandardOutput;
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    var match = regex.Match(line);
                    if (match.Success)
                    {
                        if (double.TryParse(match.Groups[1].Value, out double percent) &&
                            double.TryParse(match.Groups[2].Value, out double speedValue))
                        {
                            string unit = match.Groups[3].Value;
                            double speedBytes = speedValue;
                            if (unit.Equals("KiB", StringComparison.OrdinalIgnoreCase)) speedBytes *= 1024;
                            else if (unit.Equals("MiB", StringComparison.OrdinalIgnoreCase)) speedBytes *= 1024 * 1024;
                            else if (unit.Equals("GiB", StringComparison.OrdinalIgnoreCase)) speedBytes *= 1024 * 1024 * 1024;

                            progress?.Report(new DownloadProgress
                            {
                                Percentage = percent,
                                SpeedBytesPerSec = speedBytes,
                                Eta = match.Groups[4].Value,
                                Status = "downloading"
                            });
                        }
                    }
                    else if (line.Contains("[download] 100%"))
                    {
                        progress?.Report(new DownloadProgress
                        {
                            Percentage = 100,
                            SpeedBytesPerSec = 0,
                            Eta = "00:00",
                            Status = "downloading"
                        });
                    }
                    else if (line.Contains("Post-process") || line.Contains("[Merger]") || line.Contains("[ExtractAudio]"))
                    {
                        progress?.Report(new DownloadProgress
                        {
                            Percentage = 100,
                            SpeedBytesPerSec = 0,
                            Eta = "00:00",
                            Status = "postprocessing"
                        });
                    }
                }
            });

            await _currentProcess.WaitForExitAsync(cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            if (_currentProcess.ExitCode == 0)
            {
                if (Directory.Exists(tempPath))
                {
                    var files = Directory.GetFiles(tempPath).Where(f => !f.EndsWith(".part") && !f.EndsWith(".ytdl")).ToArray();
                    foreach (var downloadedFile in files)
                    {
                        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(downloadedFile);
                        var ext = Path.GetExtension(downloadedFile);
                        var finalPath = Path.Combine(outputPath, Path.GetFileName(downloadedFile));

                        if (settings.FileExistsAction != "Overwrite")
                        {
                            int counter = 1;
                            while (File.Exists(finalPath))
                            {
                                finalPath = Path.Combine(outputPath, $"{fileNameWithoutExt} ({counter}){ext}");
                                counter++;
                            }
                        }

                        File.Move(downloadedFile, finalPath, true);
                    }
                }

                progress?.Report(new DownloadProgress
                {
                    Percentage = 100,
                    SpeedBytesPerSec = 0,
                    Eta = "00:00",
                    Status = "finished"
                });
            }
            else
            {
                var errMessage = string.IsNullOrWhiteSpace(stderrOutput) ? $"Process exited with code {_currentProcess.ExitCode}" : ParseErrorMessage(stderrOutput);
                throw new Exception(errMessage);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            throw;
        }
        finally
        {
            try
            {
                if (_currentProcess != null && !_currentProcess.HasExited)
                {
                    _currentProcess.Kill(true);
                }
            }
            catch { }

            _currentProcess?.Dispose();
            _currentProcess = null;

            Task.Run(async () =>
            {
                for (int i = 0; i < 5; i++)
                {
                    await Task.Delay(1000);
                    try
                    {
                        if (Directory.Exists(tempPath))
                            Directory.Delete(tempPath, true);
                        break;
                    }
                    catch { }
                }
            });
        }
    }

    public void Cancel()
    {
        if (_currentProcess != null && !_currentProcess.HasExited)
        {
            try
            {
                _currentProcess.Kill(true);
            }
            catch
            {
                // Ignore kill errors
            }
        }
        
        CleanupOrphanedMeiFolders();
    }

    public void CleanupOrphanedMeiFolders()
    {
        Task.Run(async () =>
        {
            try
            {
                // Give OS time to fully terminate process tree and release file locks
                await Task.Delay(1500);

                // Check if any yt-dlp process is currently running
                var runningProcesses = System.Diagnostics.Process.GetProcessesByName("yt-dlp");
                if (runningProcesses.Length > 0)
                {
                    return; // Do not delete MEI folders if yt-dlp is running
                }

                var tempPath = Path.GetTempPath();
                var meiFolders = Directory.GetDirectories(tempPath, "_MEI*");
                foreach (var folder in meiFolders)
                {
                    try
                    {
                        Directory.Delete(folder, true);
                    }
                    catch { }
                }
            }
            catch { }
        });
    }
}
