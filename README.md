<div align="center">
  <img src="icons/universal.png" alt="Candy Plus Logo" width="128"/>
  <h1>Candy Plus</h1>
  <p>An advanced, modern graphical user interface for yt-dlp built with WPF and .NET 8.0.</p>
</div>

---

## Overview

Candy Plus is a standalone Windows frontend for `yt-dlp` and `FFmpeg` with more advanced controls and features compared to the standard Candy version. It abstracts command-line operations into a seamless graphical interface, allowing users to parse, select, and download high-quality video and audio streams into standard media containers without requiring CLI knowledge.

> **Note**: This build is based on the classic version of [Candy](https://github.com/chetanbansode/Candy)

## Features

- **Advanced Format Parsing**: Automatically fetches and categorizes all available video and audio streams for a given URL with fine-grained control.
- **Native Fluent UI**: Built using Windows Presentation Foundation (WPF) with `WPF UI`, supporting native system themes (Light/Dark) and Mica backdrops on Windows 11.
- **Metadata Support**: Optional embedding of creator subtitles and stream metadata directly into the output file.
- **Plus Exclusive Capabilities**: Advanced configuration and options for power users looking to get the most out of `yt-dlp`.

## Screenshots

<img width="1920" height="1021" alt="screenshot1" src="https://github.com/user-attachments/assets/9f191a48-62f6-4b6f-aec6-2f676657cc77" />
<br><br>
<img width="1920" height="1028" alt="screenshot2" src="https://github.com/user-attachments/assets/bc8d031d-1753-480a-93e0-0e55427f15d0" />
<br><br>
<img alt="screenshot3" src="https://github.com/user-attachments/assets/94ca2781-7cf6-415b-ad5e-46d64933f4a3" width='490' /> &nbsp;

<img alt="screenshot4" src="https://github.com/user-attachments/assets/7d119dc0-8ab3-40a6-887f-13d0b7b48788" width='490' />





## Installation

End-users can download the pre-compiled installer:

1. Navigate to the [Releases](../../releases) section of this repository.
2. Download `CandyPlusInstaller.exe`.
3. Run the installer. All dependencies (including `yt-dlp` and `FFmpeg`) are bundled internally.

## Building from Source

To compile the application from source, you will need the [.NET 8.0 SDK](https://dotnet.microsoft.com/en-us/download)

### 1. Build the Executable

Clone the repository and run the following command in the project root to produce a standalone executable:

```cmd
dotnet publish -c ReleasePlus -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true
```

*(Note: We use the `ReleasePlus` configuration to target the Candy Plus specific build settings)*

### 2. Supply External Binaries

Candy Plus relies on external binaries that are **not** bundled in this source repository due to their size. Before compiling the Windows Installer, you must acquire these binaries manually:

1. Download the latest `yt-dlp.exe` from [yt-dlp releases](https://github.com/yt-dlp/yt-dlp/releases)
2. Download the latest `ffmpeg.exe` and `ffprobe.exe` from [FFmpeg Windows builds](https://ffmpeg.org/download.html)
3. Place all three `.exe` files into the following directory:
   `bin\ReleasePlus\net8.0-windows\win-x64\publish\`

### 3. Compile the Installer

Open `setup_plus.iss` with Inno Setup 6 and compile the script. The final installer will be generated as `CandyPlusInstaller.exe` in the `Output\` directory.

## Acknowledgements

Candy Plus is a graphical wrapper. All core downloading and media processing capabilities are strictly powered by the following incredible open-source projects. 

- **[yt-dlp](https://github.com/yt-dlp/yt-dlp)**: A youtube-dl fork with additional features and fixes.
- **[FFmpeg](https://ffmpeg.org/)**: A complete, cross-platform solution to record, convert and stream audio and video.
- **[WPF UI](https://github.com/lepoco/wpfui)**: Fluent design system elements for WPF.

## License

This project is licensed under the [MIT License](LICENSE)
