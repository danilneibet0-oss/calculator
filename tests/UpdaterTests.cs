using System;
using Calculator;

static class UpdaterTests
{
    static int failed;

    static void Check(string name, bool ok)
    {
        if (ok) return;
        failed++;
        Console.WriteLine("FAIL {0}", name);
    }

    public static int Run()
    {
        var current = new Version(1, 3, 0, 0);
        Check("same version is not newer", !Updater.IsNewer("v1.3.0", current));
        Check("older version is not newer", !Updater.IsNewer("v1.2.0", current));
        Check("newer minor is newer", Updater.IsNewer("v1.4.0", current));
        Check("newer patch without v", Updater.IsNewer("1.3.1", current));
        Check("numeric, not text, comparison", Updater.IsNewer("v1.10.0", new Version(1, 9, 0, 0)));
        Check("garbage tag is ignored", !Updater.IsNewer("latest", current));
        Check("empty tag is ignored", !Updater.IsNewer("", current));

        const string hex = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        string json = "{\"tag_name\":\"v1.4.0\",\"assets\":[" +
            "{\"name\":\"Calculator-Setup-1.4.0.exe\",\"browser_download_url\":\"https://example.com/a.exe\"}," +
            "{\"name\":\"Calculator-Setup-1.4.0.exe.sha256\",\"browser_download_url\":\"https://example.com/a.sha256\"}]}";
        ReleaseInfo release = Updater.ParseRelease(json);
        Check("tag parsed", release.TagName == "v1.4.0");

        ReleaseAsset installer = Updater.FindInstaller(release);
        Check("installer found", installer != null && installer.Name == "Calculator-Setup-1.4.0.exe");
        Check("checksum found", installer != null && Updater.FindAsset(release, installer.Name + ".sha256") != null);
        Check("no installer in empty release",
            Updater.FindInstaller(Updater.ParseRelease("{\"tag_name\":\"v1.4.0\",\"assets\":[]}")) == null);

        string hash;
        Check("checksum line with file name", Updater.TryParseChecksum(hex + "  Calculator-Setup-1.4.0.exe\n", "Calculator-Setup-1.4.0.exe", out hash) && hash == hex);
        Check("checksum for another file rejected", !Updater.TryParseChecksum(hex + "  other.exe", "Calculator-Setup-1.4.0.exe", out hash));
        Check("short hash rejected", !Updater.TryParseChecksum("abc  Calculator-Setup-1.4.0.exe", "Calculator-Setup-1.4.0.exe", out hash));
        Check("bare hash accepted", Updater.TryParseChecksum(hex, "any.exe", out hash) && hash == hex);
        Check("empty checksum rejected", !Updater.TryParseChecksum("", "any.exe", out hash));

        return failed;
    }
}
