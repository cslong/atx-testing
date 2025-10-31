// See https://aka.ms/new-console-template for more information

using System.Diagnostics;

Console.WriteLine("Hello, World!");

var projectPath =
    "D:\\Users\\longachr\\source\\repos\\TestDotNet10ClassLibrary\\TestDotNet10ClassLibrary\\TestDotNet10ClassLibrary.csproj";

var process = new Process
{
    StartInfo = new ProcessStartInfo
    {
        FileName = "dotnet",
        Arguments = $"restore \"{projectPath}\"",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    }
};

process.Start();

string output = process.StandardOutput.ReadToEnd();
string error = process.StandardError.ReadToEnd();

process.WaitForExit();

Console.WriteLine(output);
if (!string.IsNullOrEmpty(error))
{
    Console.WriteLine($"Error: {error}");
}

Console.WriteLine(process.ExitCode); 