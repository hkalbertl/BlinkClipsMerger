﻿using CliWrap;
using CliWrap.Buffered;
using SkiaSharp;
using System.Globalization;
using System.Text.RegularExpressions;

namespace BlinkClipsMerger
{
    internal static class Processor
    {
        /// <summary>
        /// The Blink clips file extension filter. The clips should have .mp4 extension.
        /// </summary>
        private const string BlinkClipFileExtensionFilter = "*.mp4";

        /// <summary>
        /// The file extension name of intermediate video files. Such as the title clip.
        /// </summary>
        private const string IntermediateVideoExtension = ".mkv";

        /// <summary>
        /// The date format used for Blink month directory name. It should match something like: 23-12
        /// </summary>
        private const string BlinkMonthDirectoryFormat = "yy-MM";

        /// <summary>
        /// The date format used for Blink date directory name. It should match something like: 23-12-31
        /// </summary>
        private const string BlinkDateDirectoryFormat = "yy-MM-dd";

        /// <summary>
        /// The time format used for prefix of Blink clip file name. It should match something like: 23-59-58
        /// </summary>
        private const string BlinkClipTimeFormat = "HH-mm-ss";

        /// <summary>
        /// A static class defined parameters for FFmpeg executables.
        /// </summary>
        static class FFmpegParameters
        {
            /// <summary>
            /// The ffmpeg parameters for creating slient audio stream.
            /// These parameters should be simliar to Blink clips standard audio encoding.
            /// </summary>
            internal const string AddSlientAudio = "-f lavfi -i anullsrc=r=16000:cl=mono:n=32";

            /// <summary>
            /// The output audio parameters that using AAC single channel (mono) with 16k bit rate.
            /// </summary>
            internal const string EncodeOutputAudio = "-c:a aac -ac 1 -b:a 16k";

            /// <summary>
            /// The parameters for getting video duration by using ffprobe.
            /// </summary>
            internal const string GetDuration = "-v error -select_streams v:0 -show_entries format=duration -of csv=p=0";

            /// <summary>
            /// The parameters for getting video dimension by using ffprobe.
            /// </summary>
            internal const string GetDimension = "-v error -select_streams v:0 -show_entries stream=width,height -of csv=s=x:p=0";

            /// <summary>
            /// The parameters for getting audio stream by using ffprobe.
            /// </summary>
            internal const string GetAudioStream = "-v error -show_streams -select_streams a";
        }

        /// <summary>
        /// The Regex for Blink clip file name. It should match something like: 23-59-58_Garage_123.mp4
        /// </summary>
        private static readonly Regex BlinkClipFileNameRegex = new("^[0-9]{2}\\-[0-9]{2}\\-[0-9]{2}_.+_[0-9]+\\.mp4$");

        /// <summary>
        /// The entry point of main logic.
        /// </summary>
        /// <param name="options">The parsed command line options.</param>
        /// <returns>Return true if the merging completed successfully.</returns>
        internal static async Task<bool> ProcessAsync(Options options)
        {
            // Check the options
            if (!Directory.Exists(options.InputDirectory))
            {
                Console.Error.WriteLine("Invalid input directory: {0}", options.InputDirectory);
                return false;
            }
            if (!Directory.Exists(options.OutputDirectory))
            {
                Console.Error.WriteLine("Invalid output directory: {0}", options.OutputDirectory);
                return false;
            }
            if (options.TitleDuration <= 0)
            {
                Console.Error.WriteLine("Invalid title duration: {0}", options.TitleDuration);
                return false;
            }
            if (!SKColor.TryParse(options.TitleBackground, out var skBackgroundColor))
            {
                Console.Error.WriteLine("Invalid title background color: {0}", options.TitleBackground);
                return false;
            }
            if (!SKColor.TryParse(options.TitleForeground, out var skForegroundColor))
            {
                Console.Error.WriteLine("Invalid title foreground color: {0}", options.TitleForeground);
                return false;
            }
            if (string.IsNullOrEmpty(options.FileNameTemplate))
            {
                if (options.GroupByMonth)
                {
                    options.FileNameTemplate = "{0}_{1:yyyy-MM}.mp4";
                }
                else
                {
                    options.FileNameTemplate = "{0}_{1:yyyy-MM-dd}.mp4";
                }
            }

            // An inline method for writing log message to console based on options.Quiet value
            void WriteLog(string message)
            {
                if (!options.Quiet)
                {
                    Console.WriteLine(message);
                }
            };

            // Change thread culture
            if (!string.IsNullOrEmpty(options.ThreadCulture))
            {
                try
                {
                    var culture = CultureInfo.GetCultureInfo(options.ThreadCulture);
                    Thread.CurrentThread.CurrentCulture = culture;
                    Thread.CurrentThread.CurrentUICulture = culture;
                    WriteLog($"Using thread culture: {options.ThreadCulture}");
                }
                catch (CultureNotFoundException)
                {
                    Console.Error.WriteLine("Invalid thread culture: {0}", options.ThreadCulture);
                    return false;
                }
            }

            // Cache the Skia fonts and styles
            using var skFontManager = SKFontManager.CreateDefault();
            using var skTypeface = skFontManager.MatchFamily(options.FontFamily, SKFontStyle.Normal);
            using var skPaintStyle = new SKPaint()
            {
                IsAntialias = true,
                Color = skForegroundColor,
                TextSize = options.FontSize,
                Typeface = skTypeface,
                TextAlign = SKTextAlign.Center,
            };

            // Prepare the executable
            var ffprobeCommand = options.FFProbeExecutablePath ?? "ffprobe";

            // Handle termination by ctrl + c
            using var cancellationSource = new CancellationTokenSource();
            Console.CancelKeyPress += async (_, e) =>
            {
                e.Cancel = true;
                WriteLog("Termination key detected. Cleaning up...");
                if (!cancellationSource.IsCancellationRequested)
                {
                    cancellationSource.Cancel();
                }
                await Task.Delay(1000);
            };

            // Prepare the camera name filter Regex
            Regex cameraFilter = null;
            if (!string.IsNullOrEmpty(options.CameraFilter))
            {
                cameraFilter = new Regex(options.CameraFilter);
            }

            // Process on each month folder
            int totalMergedFiles = 0;
            var cameraClipList = new Dictionary<string, List<ClipInfo>>();
            foreach (var monthFolder in new DirectoryInfo(options.InputDirectory).GetDirectories())
            {
                if (!DateTime.TryParseExact(monthFolder.Name, BlinkMonthDirectoryFormat, null, DateTimeStyles.None, out var clipMonth))
                {
                    // Skip folder name that are not YY-MM
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"Skipping non-Blink standard naming month folder: {monthFolder.FullName}");
#endif
                    continue;
                }
                WriteLog($"Processing month directory: {monthFolder.Name}");

                // Process on each date folder
                foreach (var dateFolder in monthFolder.GetDirectories())
                {
                    if (!DateTime.TryParseExact(dateFolder.Name, BlinkDateDirectoryFormat, null, DateTimeStyles.None, out var clipDate))
                    {
                        // Skip folder name that are not YY-MM-DD
#if DEBUG
                        System.Diagnostics.Debug.WriteLine($"Skipping non-Blink standard naming date folder: {dateFolder.FullName}");
#endif
                        continue;
                    }

                    // Retrieve clip files by file extension filter
                    var clipFiles = dateFolder.GetFiles(BlinkClipFileExtensionFilter);
                    WriteLog($"  Processing date directory: {dateFolder.Name} with {clipFiles.Length} clip(s)");

                    // Process on each date clip files
                    foreach (var clipFile in clipFiles)
                    {
                        if (!BlinkClipFileNameRegex.IsMatch(clipFile.Name))
                        {
                            // Skip file name that are not HH-mm-ss_[Camera]_[Sequence].mp4
#if DEBUG
                            System.Diagnostics.Debug.WriteLine($"Skipping non-Blink standard naming camera clip: {clipFile.Name}");
#endif
                            continue;
                        }
                        var cameraName = clipFile.Name[9..clipFile.Name.LastIndexOf('_')];
                        if (null != cameraFilter && !cameraFilter.IsMatch(cameraName))
                        {
                            // Skip camera that does not match to the expression
#if DEBUG
                            System.Diagnostics.Debug.WriteLine($"Filtered camera clip: {cameraName} of {clipFile.Name}");
#endif
                            continue;
                        }

                        // Prepare the ClipInfo
                        var rawClipTime = clipFile.Name[..8];
                        var clipTime = clipDate.Add(DateTime.ParseExact(rawClipTime, BlinkClipTimeFormat, null).TimeOfDay);
                        var clipInfo = new ClipInfo(clipFile.FullName, clipTime);

                        // Get clip duration
                        BufferedCommandResult ffprobeResult = null;
                        try
                        {
                            ffprobeResult = await Cli.Wrap(ffprobeCommand).WithArguments(args =>
                            {
                                args.Add(FFmpegParameters.GetDuration, false);
                                args.Add(clipInfo.FullPath);
                            }).WithValidation(CommandResultValidation.None).ExecuteBufferedAsync();
                            if (!double.TryParse(ffprobeResult.StandardOutput, out var duration))
                            {
                                // Failed to read duration? May be the file is corrupted?
                                WriteLog($"    Ignored corrupted / unsupported file: {clipFile.Name}");
                                continue;
                            }
                            clipInfo.Duration = duration;
                        }
                        catch (Exception ex)
                        {
                            WriteLog($"    Failed to get duration: {clipFile.Name}, {ex.Message}");
                        }

                        // Check duration
                        if (options.IgnoreDuration > 0 && clipInfo.Duration < options.IgnoreDuration)
                        {
                            // Ignore invalid / too short clips
                            WriteLog($"    Ignored short clip: {clipFile.Name} < {options.IgnoreDuration}s");
                            continue;
                        }

                        // Get clip dimension
                        ffprobeResult = await Cli.Wrap(ffprobeCommand).WithArguments(args =>
                        {
                            args.Add(FFmpegParameters.GetDimension, false);
                            args.Add(clipInfo.FullPath);
                        }).ExecuteBufferedAsync();
                        int separator = ffprobeResult.StandardOutput.IndexOf('x');
                        clipInfo.ClipWidth = int.Parse(ffprobeResult.StandardOutput[..separator]);
                        clipInfo.ClipHeight = int.Parse(ffprobeResult.StandardOutput[(separator + 1)..]);

                        // Check clip has audio stream
                        ffprobeResult = await Cli.Wrap(ffprobeCommand).WithArguments(args =>
                        {
                            args.Add(FFmpegParameters.GetAudioStream, false);
                            args.Add(clipInfo.FullPath);
                        }).ExecuteBufferedAsync();
                        clipInfo.HasAudio = !string.IsNullOrWhiteSpace(ffprobeResult.StandardOutput);

                        // Print clip info
                        WriteLog($"    {clipFile.Name}: {clipInfo}");

                        // Add clip to camera list
                        if (cameraClipList.TryGetValue(cameraName, out List<ClipInfo> list))
                        {
                            list.Add(clipInfo);
                        }
                        else
                        {
                            cameraClipList.Add(cameraName, [clipInfo]);
                        }

                        // Check for termination
                        if (cancellationSource.Token.IsCancellationRequested)
                        {
                            return false;
                        }
                    }
                    WriteLog($"    Total {cameraClipList.Count} camera(s) found on {dateFolder.Name}: {string.Join(", ", cameraClipList.Keys)}");

                    // For merging clips by month, skip to next date
                    if (options.GroupByMonth)
                    {
                        continue;
                    }

                    // Merge file by date
                    totalMergedFiles += await MergeClips(options, cameraClipList, skBackgroundColor, skPaintStyle, cancellationSource.Token);
                    cameraClipList.Clear();

                    // Check for termination
                    if (cancellationSource.Token.IsCancellationRequested)
                    {
                        return false;
                    }
                }

                // For merging clips by month, skip to next date
                if (options.GroupByMonth)
                {
                    // Merge file by month
                    totalMergedFiles += await MergeClips(options, cameraClipList, skBackgroundColor, skPaintStyle, cancellationSource.Token);
                }

                // Check for termination
                if (cancellationSource.Token.IsCancellationRequested)
                {
                    return false;
                }
            }

            // Merge completed
            WriteLog($"Finished! Total {totalMergedFiles} file(s) generated.");
            return true;
        }

        /// <summary>
        /// Merge clips by specified camera / clip list.
        /// </summary>
        /// <param name="options">The parsed command line options.</param>
        /// <param name="cameraClipList">The dictionary of camera / clip list.</param>
        /// <param name="skBackgroundColor">The Skia background color object for title background.</param>
        /// <param name="skPaintStyle">The Skia paint style object for title text / styles.</param>
        /// <param name="cancellationToken">The cancellation token to terminate sub processes gratefully.</param>
        /// <returns>The number of merged clips.</returns>
        /// <exception cref="Exception">Unhandled exceptions, such as invalid template name / unknown ffmpeg errors.</exception>
        private async static Task<int> MergeClips(Options options, Dictionary<string, List<ClipInfo>> cameraClipList, SKColor skBackgroundColor, SKPaint skPaintStyle, CancellationToken cancellationToken)
        {
            // Prepare executable command
            var ffmpegCommand = options.FFMpegExecutablePath ?? "ffmpeg";

            // An inline method for writing log message to console based on options.Quiet value
            void WriteLog(string message)
            {
                if (!options.Quiet)
                {
                    Console.WriteLine(message);
                }
            };

            // Process on each camera
            int mergedFiles = 0;
            foreach (var cameraName in cameraClipList.Keys)
            {
                string workingDirectoryPath = null;
                var clipList = cameraClipList[cameraName];
                try
                {
                    // Declare variables
                    DateTime fileNameDate = DateTime.MinValue;
                    var combineVideoList = new List<string>();
                    var shouldUseAudio = false;
                    int maxClipWidth = 0, maxClipHeight = 0;
                    foreach (var clipInfo in clipList)
                    {
                        if (!shouldUseAudio && clipInfo.HasAudio)
                        {
                            // At least one clip has audio
                            shouldUseAudio = true;
                        }
                        if (maxClipWidth < clipInfo.ClipWidth)
                        {
                            // Keep the max dimension
                            maxClipWidth = clipInfo.ClipWidth;
                            maxClipHeight = clipInfo.ClipHeight;
                        }
                        if (fileNameDate == DateTime.MinValue)
                        {
                            // Use the capture time of first clip for output file name
                            fileNameDate = clipInfo.CaptureTime;
                        }
                    }

                    // Create working directory
                    workingDirectoryPath = Path.Combine(options.OutputDirectory, $"_bcm-working-{DateTime.Now.Ticks}");
                    Directory.CreateDirectory(workingDirectoryPath);

                    WriteLog($"  Preparing intermediate clips for camera {cameraName}: {clipList.Count} file(s) with{(shouldUseAudio ? string.Empty : "out")} audio");
                    foreach (var clipInfo in clipList.OrderBy(m => m.CaptureTime))
                    {
                        // Create a blank image with solid colour
                        var sourceClipName = Path.GetFileName(clipInfo.FullPath);
                        WriteLog($"    Creating title clip for {sourceClipName}");
                        var clipImagePath = Path.Combine(workingDirectoryPath, $"_bcm-title-{clipInfo.CaptureTime:yyMMddHHmmss}.png");
                        using (var surface = SKSurface.Create(new SKImageInfo(maxClipWidth, maxClipHeight)))
                        {
                            // Fill background color
                            var canvas = surface.Canvas;
                            canvas.Clear(skBackgroundColor);

                            // Draw the titles, which are Camera Name, Date and Time
                            SKRect textRectangle = new();
                            var textRows = new string[] {
                                cameraName,
                                clipInfo.CaptureTime.ToString(options.DateFormat),
                                clipInfo.CaptureTime.ToString(options.TimeFormat)
                            };
                            float textOriginX = clipInfo.ClipWidth / 2;
                            for (int r = 0; r < textRows.Length; r++)
                            {
                                // Measure the text size and adjust the origin position
                                skPaintStyle.MeasureText(textRows[r], ref textRectangle);
                                float textOriginY = (clipInfo.ClipHeight + textRectangle.Height) / 2;
                                if (r == 0)
                                {
                                    textOriginY -= textRectangle.Height * 1.5f;
                                }
                                else if (r == 2)
                                {
                                    textOriginY += textRectangle.Height * 1.5f;
                                }
                                canvas.DrawText(textRows[r], textOriginX, textOriginY, skPaintStyle);
                            }

                            // Save to image file
                            using var image = surface.Snapshot();
                            using var pngData = image.Encode(SKEncodedImageFormat.Png, 90);
                            using var pngStream = File.OpenWrite(clipImagePath);
                            pngData.SaveTo(pngStream);
                        }

                        // Create dummy title video
                        var titleClipPath = Path.ChangeExtension(clipImagePath, IntermediateVideoExtension);
                        await Cli.Wrap(ffmpegCommand).WithArguments(args =>
                        {
                            args.Add("-y -loop 1 -f image2 -i", false);
                            args.Add(clipImagePath);

                            // Add audio stream when more than one source clip has audio
                            if (shouldUseAudio)
                            {
                                args.Add(FFmpegParameters.AddSlientAudio, false);
                            }

                            // Add video parameters
                            args.Add("-t");
                            args.Add(options.TitleDuration);
                            args.Add("-c:v");
                            args.Add(options.VideoCodec);
                            if (!string.IsNullOrWhiteSpace(options.VideoCodecPreset))
                            {
                                args.Add("-preset");
                                args.Add(options.VideoCodecPreset);
                            }

                            // Add audio encoder, if required
                            if (shouldUseAudio)
                            {
                                args.Add(FFmpegParameters.EncodeOutputAudio, false);
                            }

                            // Add video mapping
                            args.Add("-pix_fmt yuvj420p -map 0:v", false);

                            // Add audio mapping, if required
                            if (shouldUseAudio)
                            {
                                args.Add("-map 1:a -shortest", false);
                            }

                            args.Add(titleClipPath);

                        }).ExecuteAsync(cancellationToken);

                        // Check to add audio stream / resize on source video
                        var sourceClipPath = clipInfo.FullPath;
                        var requiredSlient = shouldUseAudio && !clipInfo.HasAudio;
                        var requiredResize = clipInfo.ClipWidth != maxClipWidth;
                        if (requiredSlient || requiredResize)
                        {
                            // Add audio stream to source video
                            WriteLog($"      Extra processing on source clip: slient={(requiredSlient ? 'y' : 'n')},resize={(requiredResize ? 'y' : 'n')}");
                            var processedClipPath = Path.Combine(workingDirectoryPath,
                                $"_bcm-source-{clipInfo.CaptureTime:yyMMddHHmmss}{IntermediateVideoExtension}");

                            await Cli.Wrap(ffmpegCommand).WithArguments(args =>
                            {
                                // Add source video
                                args.Add("-i");
                                args.Add(sourceClipPath);

                                // Add slient audio
                                if (requiredSlient)
                                {
                                    args.Add(FFmpegParameters.AddSlientAudio, false);
                                }

                                // Check resize video is required
                                if (requiredResize)
                                {
                                    // Add video resize parameters
                                    args.Add("-c:v");
                                    args.Add(options.VideoCodec);
                                    if (!string.IsNullOrWhiteSpace(options.VideoCodecPreset))
                                    {
                                        args.Add("-preset");
                                        args.Add(options.VideoCodecPreset);
                                    }

                                    args.Add($"-vf scale={maxClipWidth}:{maxClipHeight}", false);
                                }
                                else
                                {
                                    // Copy video stream
                                    args.Add("-c:v copy", false);
                                }

                                if (requiredSlient)
                                {
                                    // Encode audio
                                    args.Add(FFmpegParameters.EncodeOutputAudio, false);

                                    // Fix the stream length
                                    args.Add("-shortest");
                                }
                                else if (clipInfo.HasAudio)
                                {
                                    // Copy audio if the source already as one
                                    args.Add("-c:a copy", false);
                                }

                                // Add output path
                                args.Add(processedClipPath);

                            }).ExecuteAsync(cancellationToken);

                            // Use the new source with audio file for futher processing
                            sourceClipPath = processedClipPath;
                        }

                        // Merge the title and source video
                        var combinedClipName = $"_bcm-combined-{clipInfo.CaptureTime:yyMMddHHmmss}{IntermediateVideoExtension}";
                        var combinedClipPath = Path.Combine(workingDirectoryPath, combinedClipName);
                        WriteLog($"      Combining title with source clip: {combinedClipName}");
                        await Cli.Wrap(ffmpegCommand).WithWorkingDirectory(workingDirectoryPath).WithArguments(args =>
                        {
                            // Add title clip
                            args.Add("-i");
                            args.Add(titleClipPath);

                            // Add source clip
                            args.Add("-i");
                            args.Add(sourceClipPath);

                            // Add merge parameters based on the existence of audio stream
                            if (shouldUseAudio)
                            {
                                args.Add($"-filter_complex \"[0:v][0:a][1:v][1:a]concat=n=2:v=1:a=1[outv][outa]\" -map \"[outv]\" -map \"[outa]\"", false);
                            }
                            else
                            {
                                args.Add($"-filter_complex \"[0:v][1:v]concat=n=2:v=1:a=0[outv]\" -map \"[outv]\"", false);
                            }

                            // Add frame rate
                            args.Add("-r");
                            args.Add(options.FrameRate);

                            // Encode the video
                            args.Add("-c:v");
                            args.Add(options.VideoCodec);
                            if (!string.IsNullOrWhiteSpace(options.VideoCodecPreset))
                            {
                                args.Add("-preset");
                                args.Add(options.VideoCodecPreset);
                            }

                            // Add audio encoding parameters
                            if (shouldUseAudio)
                            {
                                args.Add(FFmpegParameters.EncodeOutputAudio, false);
                            }

                            // Add output path
                            args.Add(combinedClipPath);

                        }).ExecuteAsync(cancellationToken);

                        // Add the combined path to list
                        combineVideoList.Add(combinedClipPath);
                    }

                    // Prepare output file
                    var outputName = string.Format(options.FileNameTemplate, cameraName, fileNameDate);
                    var outputPath = Path.Combine(options.OutputDirectory, outputName);

                    // Check the number of clips to merge
                    if (combineVideoList.Count == 1)
                    {
                        // Just copy that single clip as output
                        WriteLog($"    Transcode the single clip as merged clip: {outputName}");
                        await Cli.Wrap(ffmpegCommand).WithWorkingDirectory(workingDirectoryPath).WithArguments(args =>
                        {
                            if (options.Overwrite)
                            {
                                args.Add("-y");
                            }

                            // Add source video
                            args.Add("-i");
                            args.Add(combineVideoList[0]);

                            // Copy both video and audio
                            args.Add("-c copy", false);
                            args.Add(outputPath);

                        }).ExecuteAsync(cancellationToken);
                    }
                    else
                    {
                        // Prepare the combine file list
                        var combineListPath = Path.Combine(workingDirectoryPath, "_bcm-combine.txt");
                        File.WriteAllLines(combineListPath, combineVideoList.Select(p => $"file '{Path.GetFileName(p)}'"));

                        // Start video merging
                        WriteLog($"    Merging {combineVideoList.Count} file(s) to {outputName}");
                        await Cli.Wrap(ffmpegCommand).WithWorkingDirectory(workingDirectoryPath).WithArguments(args =>
                        {
                            if (options.Overwrite)
                            {
                                args.Add("-y");
                            }

                            // Add the file list for merging
                            args.Add("-f concat -safe 0 -i", false);
                            args.Add(combineListPath);

                            // Copy both video and audio
                            args.Add("-c copy", false);
                            args.Add(outputPath);

                        }).ExecuteAsync(cancellationToken);
                    }
                    WriteLog($"      Video merged: {outputName}");
                    mergedFiles++;
                }
                catch (OperationCanceledException)
                {
                    // Termination detected
                    return -1;
                }
                catch (Exception ex)
                {
                    throw new Exception("Failed to merge clips for camera: " + cameraName, ex);
                }
                finally
                {
                    // Clean up
                    if (!string.IsNullOrEmpty(workingDirectoryPath) && Directory.Exists(workingDirectoryPath))
                    {
                        try
                        {
                            Directory.Delete(workingDirectoryPath, true);
                        }
                        catch { }
                    }
                }
            }

            return mergedFiles;
        }
    }
}
