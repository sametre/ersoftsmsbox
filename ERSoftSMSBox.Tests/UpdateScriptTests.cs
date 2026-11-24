using System.Diagnostics;
using NUnit.Framework;

namespace ERSoftSMSBox.Tests;

[TestFixture]
public class UpdateScriptTests
{
    [Test]
    public void WhenUpdatePathsContainApostrophesThenGeneratedPowerShellParses()
    {
        string script = Form1.CreateUpdateScript(@"C:\Samet's files\update.zip", @"C:\Program Files\ERSoft",
            @"C:\Program Files\ERSoft\ERSoftSMSBox.exe", @"C:\Temp\Samet's extract", 1234);
        string path = Path.Combine(Path.GetTempPath(), $"ERSoftParserTest_{Guid.NewGuid():N}.ps1");
        try
        {
            File.WriteAllText(path, script);
            ProcessStartInfo start = new("powershell.exe")
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            start.ArgumentList.Add("-NoProfile");
            start.ArgumentList.Add("-NonInteractive");
            start.ArgumentList.Add("-Command");
            start.ArgumentList.Add("$tokens = $null; $errors = $null; " +
                $"$null = [System.Management.Automation.Language.Parser]::ParseFile('{path.Replace("'", "''")}', [ref]$tokens, [ref]$errors); " +
                "$errors | ForEach-Object { Write-Output $_.Message }; if ($errors.Count -gt 0) { exit 1 }");
            using Process process = Process.Start(start)!;
            string errors = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            Assert.That(process.WaitForExit(10000), Is.True);
            Assert.That(process.ExitCode, Is.Zero, errors);
        }
        finally { File.Delete(path); }
    }
}
