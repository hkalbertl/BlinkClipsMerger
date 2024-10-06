using BlinkClipsMerger;
using CommandLine;

await Parser.Default.ParseArguments<Options>(args).WithParsedAsync(async options =>
{
    // Process the clip merge logic
    if (!await Processor.ProcessAsync(options))
    {
        // Error occurred, exit with -1
        Environment.Exit(-1);
    }
});