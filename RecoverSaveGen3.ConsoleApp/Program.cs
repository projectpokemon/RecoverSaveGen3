// Read the file from command line. If none exists, set a message and return.

using RecoverSaveGen3.Lib;

try { Recover(args); }
catch (Exception ex)
{
    Console.WriteLine(ex.Message);
    Console.WriteLine(ex.StackTrace);
}

Console.WriteLine("Press any key to exit.");
Console.ReadKey();

return;

static void Recover(ReadOnlySpan<string> args)
{
    if (args.Length < 1)
    {
        Console.WriteLine("Please provide a file to recover.");
        return;
    }

    // Read the file and attempt to fix it.
    var path = args[0];
    var fi = new FileInfo(path);
    if (!fi.Exists)
    {
        Console.WriteLine("File not found: {0}", path);
        return;
    }
    if (!Fixer3.IsSizeWorthLookingAt(fi.Length))
    {
        Console.WriteLine("File too large to be a save file: {0}", path);
        return;
    }

    var data = File.ReadAllBytes(path);
    if (!Fixer3.TryFixSaveFile(data, out var fixedData, out var message))
    {
        Console.WriteLine("Failed to fix save file: {0}", path);
        Console.WriteLine("Reason: {0}", message);
        return;
    }

    // Write the fixed file.
    var fixedPath = Path.Combine(fi.DirectoryName!, fi.Name + ".fixed");
    File.WriteAllBytes(fixedPath, fixedData);

    Console.WriteLine("Original file: {0}", path);
    Console.WriteLine("Fixed save file written to: {0}", fixedPath);
    Console.WriteLine("Fix result: {0}", message);
}