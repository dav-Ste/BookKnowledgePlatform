using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;

namespace BookKnowledge.Gateway.IntegrationTests;

public class PlaywrightE2ETests : IDisposable
{
    private Process? _identityProc;
    private Process? _frontendProc;
    private static readonly char[] NewLineChars = new[] { '\n', '\r' };
    private static readonly string[] PlaywrightInstallArgs = new[] { "install" };

    [Fact]
    public async Task PlaywrightSetCookiesEndToEnd()
    {
        var identityPort = GetFreePort();
        var frontendPort = GetFreePort();

        var solutionRoot = GetSolutionRoot();
        var identityProject = System.IO.Path.Combine(solutionRoot, "src", "Identity", "BookKnowledge.Identity.Api");
        var frontendProject = System.IO.Path.Combine(solutionRoot, "src", "Frontend", "BookKnowledge.BookSearch.Web");

        _identityProc = StartDotnetRun(identityProject, identityPort);
        _frontendProc = StartDotnetRun(frontendProject, frontendPort, env: new[] { ($"ASPNETCORE_URLS", $"http://localhost:{frontendPort}") });

        var identityUrl = $"http://localhost:{identityPort}";
        var frontendUrl = $"http://localhost:{frontendPort}";

        await WaitUntilUp(frontendUrl + "/health", TimeSpan.FromSeconds(30));

        using var playwright = await Playwright.CreateAsync();
        // Ensure browsers are installed for Playwright. This runs the playwright CLI install step.
        // Ensure playwright browsers installed (synchronous CLI invocation)
        Microsoft.Playwright.Program.Main(PlaywrightInstallArgs);

        var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();

        var response = await page.GotoAsync(frontendUrl + "/test/setcookies");
        Assert.NotNull(response);
        var headers = response.Headers;
        if (!headers.TryGetValue("set-cookie", out var sc))
        {
            // sometimes headers are lower/upper; check all
            sc = headers.FirstOrDefault(h => h.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase)).Value;
        }

        Assert.False(string.IsNullOrEmpty(sc));
        var setCookies = sc.Split(PlaywrightE2ETests.NewLineChars, StringSplitOptions.RemoveEmptyEntries);

        Assert.Contains(setCookies, s => s.Contains("correlation_test=", StringComparison.OrdinalIgnoreCase) && s.Contains("Secure") && s.Contains("HttpOnly") && s.Contains("SameSite=None", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(setCookies, s => s.Contains("PAX=", StringComparison.OrdinalIgnoreCase) && s.Contains("Secure") && s.Contains("HttpOnly") && s.Contains("SameSite=Strict", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(setCookies, s => s.Contains("JAX=", StringComparison.OrdinalIgnoreCase) && s.Contains("Secure") && s.Contains("HttpOnly") && s.Contains("SameSite=Strict", StringComparison.OrdinalIgnoreCase));

        await browser.CloseAsync();
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string GetSolutionRoot()
    {
        var dir = AppContext.BaseDirectory;
        var d = new System.IO.DirectoryInfo(dir);
        while (d != null && !System.IO.File.Exists(System.IO.Path.Combine(d.FullName, "BookKnowledgePlatform.slnx")))
            d = d.Parent!;
        return d?.FullName ?? AppContext.BaseDirectory;
    }

    private static Process StartDotnetRun(string projectPath, int port, (string, string)[]? env = null)
    {
        var psi = new ProcessStartInfo("dotnet", "run --no-build")
        {
            WorkingDirectory = projectPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.Environment["ASPNETCORE_URLS"] = $"http://localhost:{port}";
        psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        if (env != null)
        {
            foreach (var (k, v) in env) psi.Environment[k] = v;
        }

        var p = Process.Start(psi)!;
        p.OutputDataReceived += (s, e) => { if (e.Data != null) Debug.WriteLine(e.Data); };
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        return p!;
    }

    private static async Task WaitUntilUp(string url, TimeSpan timeout)
    {
        using var http = new HttpClient();
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            try
            {
                var r = await http.GetAsync(url);
                if (r.IsSuccessStatusCode) return;
            }
            catch { }
            await Task.Delay(500);
        }
        throw new TimeoutException($"Service did not become ready at {url}");
    }

    public void Dispose()
    {
        try { if (_frontendProc != null && !_frontendProc.HasExited) _frontendProc.Kill(true); } catch { }
        try { if (_identityProc != null && !_identityProc.HasExited) _identityProc.Kill(true); } catch { }
        GC.SuppressFinalize(this);
    }
}
