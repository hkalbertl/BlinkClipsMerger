using BlinkClipsMerger;
using CommandLine;
using CommandLine.Text;

// Parse the command line arguments
var parser = new Parser(with => with.HelpWriter = null);
var parserResult = parser.ParseArguments<Options>(args);
parserResult.WithNotParsed(errors =>
{
    // Show help text with error occurred
    var helpText = HelpText.AutoBuild(parserResult, h =>
    {
        // Add the example usage
        h.AddPreOptionsLines([
            string.Empty,
            string.Empty,
            "Example:",
            string.Empty,
            "  BlinkClipsMerger [options] <input directory> <output directory>",
            ]);
        h.AdditionalNewLineAfterOption = false;
        return HelpText.DefaultParsingErrorsHandler(parserResult, h);
    }, e => e);
    Console.WriteLine(helpText);
});
await parserResult.WithParsedAsync(async o =>
{
    // Process the clip merge logic
    var success = await Processor.ProcessAsync(o);
    if (!success)
    {
        // Error occurred
        Environment.Exit(-2);
    }
});