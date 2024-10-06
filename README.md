# BlinkClipsMerger

BlinkClipsMerger is a .NET console application written in C# for merging video clips captured by Blink security cameras with customizable title images. FFmpeg is being used for video encoding / merging.

## Features
* Merging video clips captured by the same camera in the same date to a single video.
* Adding title images between merged clips with camera name, captured date and time.
* Customizable options such as output file name / video encoder / quality / etc.
* Adding slient audio streams when required
* Resizing video resolution when required

https://github.com/user-attachments/assets/8e9ec428-527a-4859-9f66-8a46bf41a11d

## Supported Platforms
BlinkClipsMerger is written in pure C# so it should work on common modern platforms. Here are the tested platforms:
* Windows 10 / 11
* Ubuntu 24.04 LTS
* Raspberry Pi OS (ARM64)

## Prerequisites
* [.NET Runtime](https://dotnet.microsoft.com/en-us/download/dotnet)
* [FFmpeg](https://www.ffmpeg.org/download.html)

## Usage
```
# Windows

> BlinkClipsMerger.exe [options] <input directory> <output directory>
```
```
# Other platforms

> dotnet BlinkClipsMerger.dll [options] <input directory> <output directory>
```
## Options
```
  -q, --quite                    Hide unnecessary messages to console.

  -y, --overwrite                Overwrite existing output files.

  -g, --group-by-month           Merge clips based on the captured month instead of date.

  -m, --ffmpeg                   Specify the path to "ffmpeg" executable if FFmpeg is not configurated in system PATH
                                 variable.

  -p, --ffprobe                  Specify the path to "ffprobe" executable if FFmpeg is not configurated in system PATH
                                 variable.

  -t, --filename-template        The template string for generating output file name. Default value is
                                 "{0}_{1:yyyy-MM-dd}.mp4" where {0} is the camera name and {1} is the capture time
                                 (standard .NET date/time format can be applied). If -g option is used without -t, the
                                 default template "{0}_{1:yyyy-MM}.mp4" will be used.

  -f, --camera-filter            The camera name regex filter. Such as -f "^Gar" will match Garden and Garage clips.

  -r, --video-frame-rate         (Default: 25) The output video frame rate.

  -d, --title-duration           (Default: 2) The duration (in seconds) of title image.

  -c, --video-codec              (Default: libx265) The video codec for merged output files.

  -s, --video-codec-preset       The video codec preset to be used for output files. It will affect the output quality
                                 and not all video codec supported.

  -i, --ignore-duration          (Default: 1) Ignore clips that having duration (in seconds) lesser than specified
                                 value.

  --thread-culture               The custom thread culture to be used for date/time formatting. Such as zh-TW for
                                 Taiwan.

  --font-family                  (Default: sans-serif) The font family used in titles.

  --font-size                    (Default: 144) The font size of titles.

  --date-format                  (Default: yyyy-MM-dd) The date format of title that using standard .NET date/time
                                 formatting string.

  --time-format                  (Default: HH:mm:ss) The time format of title that using standard .NET date/time
                                 formatting string.

  --title-foreground             (Default: #ffffff) Foreground color of title.

  --title-background             (Default: #000000) Background color of title.

  --help                         Display this help screen.

  --version                      Display version information.

  <input directory> (pos. 0)     Required. Path to the Blink clips source directory

  <output directory> (pos. 1)    Required. Path to the video output directory
```

# Examples
```
# Basic example that using default options.
# Merging video clips under C:\blink\ and save output files to C:\merged\.

> BlinkClipsMerger.exe C:\blink\ C:\merged\
```

```
# For Windows, specifying ffmpeg and ffprobe path if it is not configurated in PATH variable

> BlinkClipsMerger.exe -m C:\FFmpeg\ffmpeg.exe -p C:\FFmpeg\ffmpeg.exe C:\blink\ C:\merged\
```

```
# Customize the title image by using "Arial" font with size 192.
# The date in title image will be formatted in "dd MMM YYY" by using Taiwan culture (zh-TW).
# Yellow color (#FFFF00) for title text and blue color (#0000FF) as background.

> BlinkClipsMerger.exe --font-family Arial --font-size 192 --thread-culture zh-TW --date-format "dd MMM yyyy" --title-foreground #FFFF00 --title-background #0000FF C:\blink\ C:\merged\
```

```
# Merge video clips of the same camera within the same month and using a custom output file name template.
# Output video files will be encoded by using H.264 with "slow" preset (slightly better quality).
# Camera name filter is applied so that only clips captured by camera with name started with "Gar" will be processed. Such as "Garden" and "Garage".

> BlinkClipsMerger.exe -g -t "MyClip_{0}_{1:yyyyMM}.mkv" -c libx264 -s slow -f "^Gar" C:\blink\ C:\merged\
```

## Assumptions
* The video clips should be copied out from Blink Sync Module 2.
* The name of month directory name is using `yy-MM` format. Such as `24-10`.
* The name of date directories are using `yy-MM-dd` format. Such as `24-10-04`.
* The name of video clips are using `HH-mm-ss_[Camera]_[Sequence]` format. Such as `13-24-56_Garden_012`.

## Dependency Libraries
* [CliWrap](https://github.com/Tyrrrz/CliWrap) for running FFmpeg on different platforms. This library has a __special__ [terms of use](https://github.com/Tyrrrz/CliWrap?tab=readme-ov-file#terms-of-use) so please take a look before using BlinkClipsMerger.
* [CommandLineParser](https://github.com/commandlineparser/commandline) for parsing command line arguments.
* [SkiaSharp](https://github.com/mono/SkiaSharp) for generating customizable title images.

## License
Licensed under the [MIT](http://www.opensource.org/licenses/mit-license.php) license.
