using CliWrap;
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
        /// The time format used for Blink clip file name. It should match something like: 23-59-58
        /// </summary>
        private const string BlinkClipTimeFormat = "HH-mm-ss";

        /// <summary>
        /// The ffmpeg parameters for creating slient audio stream.
        /// These parameters should be simliar to Blink clips standard audio encoding.
        /// </summary>
        private const string SlientAudioParameters = "-f lavfi -i anullsrc=r=16000:cl=mono:n=32";

        /// <summary>
        /// The output audio parameters that using AAC single channel (mono) with 16k bit rate.
        /// </summary>
        private const string OutputAudioParameters = "-c:a aac -ac 1 -b:a 16k";

        /// <summary>
        /// The Regex for Blink clip file name. It should match somthing like: 23-59-58_Garage_123.mp4
        /// </summary>
        private static readonly Regex BlinkClipFileRegex = new("^[0-9]{2}\\-[0-9]{2}\\-[0-9]{2}_.+_[0-9]+\\.mp4$");

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

            // Change thread culture
            if (!string.IsNullOrEmpty(options.ThreadCulture))
            {
                var culture = new CultureInfo(options.ThreadCulture);
                Thread.CurrentThread.CurrentCulture = culture;
                Thread.CurrentThread.CurrentUICulture = culture;
                if (!options.Quiet)
                {
                    Console.WriteLine("Using thread culture: {0}", options.ThreadCulture);
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
                if (!options.Quiet)
                {
                    Console.WriteLine("Termination key detected. Cleaning up...");
                }
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
                if (!options.Quiet)
                {
                    Console.WriteLine("Processing month directory: {0}", monthFolder.Name);
                }

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
                    if (!options.Quiet)
                    {
                        Console.WriteLine("  Processing date directory: {0}", dateFolder.Name);
                    }

                    // Get the list of clip files
                    foreach (var clipFile in dateFolder.GetFiles(BlinkClipFileExtensionFilter))
                    {
                        if (!BlinkClipFileRegex.IsMatch(clipFile.Name))
                        {
                            // Skip file name that are not HH-mm-ss_[Camera Name]_[Sequence].mp4
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
                        var ffprobeResult = await Cli.Wrap(ffprobeCommand).WithArguments(options =>
                        {
                            options.Add("-v error -select_streams v:0 -show_entries stream=duration -of default=noprint_wrappers=1:nokey=1", false);
                            options.Add(clipInfo.FullPath);
                        }).WithValidation(CommandResultValidation.None).ExecuteBufferedAsync();
                        if (!ffprobeResult.IsSuccess || !double.TryParse(ffprobeResult.StandardOutput, out var duration))
                        {
                            // Failed to read duration? May be the file is corrupted?
                            if (!options.Quiet)
                            {
                                Console.WriteLine("    Ignored corrupted / unsupported file: {0}", clipFile.Name);
                            }
                            continue;
                        }

                        // Check duration
                        if (options.IgnoreDuration > 0 && duration < options.IgnoreDuration)
                        {
                            // Ignore invalid / too short clips
#if DEBUG
                            System.Diagnostics.Debug.WriteLine($"Ignored too short clip: {clipFile.Name}");
#endif
                            continue;
                        }
                        clipInfo.Duration = duration;

                        // Get clip dimension
                        ffprobeResult = await Cli.Wrap(ffprobeCommand).WithArguments(options =>
                        {
                            options.Add("-v error -select_streams v:0 -show_entries stream=width,height -of csv=s=x:p=0", false);
                            options.Add(clipInfo.FullPath);
                        }).ExecuteBufferedAsync();
                        int separator = ffprobeResult.StandardOutput.IndexOf('x');
                        clipInfo.ClipWidth = int.Parse(ffprobeResult.StandardOutput[..separator]);
                        clipInfo.ClipHeight = int.Parse(ffprobeResult.StandardOutput[(separator + 1)..]);

                        // Check clip has audio stream
                        ffprobeResult = await Cli.Wrap(ffprobeCommand).WithArguments(options =>
                        {
                            options.Add("-v error -show_streams -select_streams a", false);
                            options.Add(clipInfo.FullPath);
                        }).ExecuteBufferedAsync();
                        clipInfo.HasAudio = !string.IsNullOrWhiteSpace(ffprobeResult.StandardOutput);

                        if (!options.Quiet)
                        {
                            Console.WriteLine("    {0}: {1}", clipFile.Name, clipInfo.ToString());
                        }

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
                    if (!options.Quiet)
                    {
                        Console.WriteLine("    Total {0} camera(s) found on {1}: {2}",
                            cameraClipList.Count, dateFolder.Name, string.Join(", ", cameraClipList.Keys));
                    }

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
            if (!options.Quiet)
            {
                Console.WriteLine("Finished! Total {0} file(s) generated.", totalMergedFiles);
            }
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

            // Process on each camera
            int mergedFiles = 0;
            DateTime fileNameDate = DateTime.MinValue;
            foreach (var cameraName in cameraClipList.Keys)
            {
                string workingDirectoryPath = null;
                var clipList = cameraClipList[cameraName];
                try
                {
                    // Declare variables
                    var combineVideoList = new List<string>();
                    var shouldUseAudio = clipList.Any(m => m.HasAudio);

                    // Create working directory
                    workingDirectoryPath = Path.Combine(options.OutputDirectory, $"_bcm-working-{DateTime.Now.Ticks}");
                    Directory.CreateDirectory(workingDirectoryPath);

                    if (!options.Quiet)
                    {
                        Console.WriteLine("  Preparing intermediate clips for camera {0}: {1} files with{2} audio",
                            cameraName, clipList.Count, shouldUseAudio ? string.Empty : "out");
                    }
                    foreach (var clipInfo in clipList)
                    {
                        // Use the capture time of first clip for output file name
                        var sourceClipName = Path.GetFileName(clipInfo.FullPath);
                        if (fileNameDate == DateTime.MinValue)
                        {
                            fileNameDate = clipInfo.CaptureTime;
                        }

                        // Create a blink image with solid colour
                        if (!options.Quiet)
                        {
                            Console.WriteLine("    Creating title clip for {0}", sourceClipName);
                        }
                        var clipImagePath = Path.Combine(workingDirectoryPath, $"_bcm-title-{clipInfo.CaptureTime:yyMMddHHmmss}.png");
                        using (var surface = SKSurface.Create(new SKImageInfo(clipInfo.ClipWidth, clipInfo.ClipHeight)))
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
                                args.Add(SlientAudioParameters, false);
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
                                args.Add(OutputAudioParameters, false);
                            }

                            // Add video mapping
                            args.Add($"-pix_fmt yuvj420p -map 0:v", false);

                            // Add audio mapping, if required
                            if (shouldUseAudio)
                            {
                                args.Add($"-map 1:a -shortest", false);
                            }

                            args.Add(titleClipPath);

                        }).ExecuteAsync(cancellationToken);

                        // Check audio stream on source video
                        var sourceClipPath = clipInfo.FullPath;
                        if (shouldUseAudio && !clipInfo.HasAudio)
                        {
                            // Add audio stream to source video
                            if (!options.Quiet)
                            {
                                Console.WriteLine("    Adding slient audio: {0}", sourceClipName);
                            }
                            var clipWithAudioPath = Path.Combine(workingDirectoryPath, $"_bcm-source-{clipInfo.CaptureTime:yyMMddHHmmss}{IntermediateVideoExtension}");

                            await Cli.Wrap(ffmpegCommand).WithArguments(args =>
                            {
                                // Add source video
                                args.Add("-i");
                                args.Add(sourceClipPath);

                                // Add slient audio
                                args.Add(SlientAudioParameters, false);

                                // Copy video stream
                                args.Add("-c:v copy", false);

                                // Encode audio
                                args.Add(OutputAudioParameters, false);

                                // Fix the stream length
                                args.Add("-shortest");

                                args.Add(clipWithAudioPath);
                            }).ExecuteAsync(cancellationToken);

                            // Use the new source with audio file for futher processing
                            sourceClipPath = clipWithAudioPath;
                        }

                        // Merge the title and source video
                        var combinedClipName = $"_bcm-combined-{clipInfo.CaptureTime:yyMMddHHmmss}{IntermediateVideoExtension}";
                        var combinedClipPath = Path.Combine(workingDirectoryPath, combinedClipName);
                        if (!options.Quiet)
                        {
                            Console.WriteLine("      Combining title with source clip: {0}", combinedClipName);
                        }
                        await Cli.Wrap(ffmpegCommand).WithWorkingDirectory(workingDirectoryPath).WithArguments(args =>
                        {
                            // Add title clip
                            args.Add("-i");
                            args.Add(titleClipPath);

                            // Add source clip
                            args.Add("-i");
                            args.Add(sourceClipPath);

                            // Add merge paraemters based on the existence of audio stream
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

                            // Add video encoding parameters
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
                                args.Add(OutputAudioParameters, false);
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
                        if (!options.Quiet)
                        {
                            Console.WriteLine("    Transcode the single clip as merged clip: {0}", outputName);
                        }

                        // Just copy that single clip as output
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

                        if (!options.Quiet)
                        {
                            Console.WriteLine("    Merging {0} file(s) to {1}", combineVideoList.Count, outputName);
                        }

                        // Start video merging
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
                    if (!options.Quiet)
                    {
                        Console.WriteLine("      Video merged: {0}", outputName);
                    }
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
