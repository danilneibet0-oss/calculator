using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.Win32;

namespace Calculator
{
    [DataContract]
    public sealed class ReleaseInfo
    {
        [DataMember(Name = "tag_name")] public string TagName;
        [DataMember(Name = "assets")] public List<ReleaseAsset> Assets;
    }

    [DataContract]
    public sealed class ReleaseAsset
    {
        [DataMember(Name = "name")] public string Name;
        [DataMember(Name = "browser_download_url")] public string Url;
    }

    /// <summary>Checks GitHub for a newer release, downloads its installer in the background and runs it
    /// silently when the window closes. Only installers whose SHA-256 matches the published checksum are used.</summary>
    public static class Updater
    {
        const string ReleasesUrl = "https://api.github.com/repos/danilneibet0-oss/calculator/releases/latest";
        const string InstallerPrefix = "Calculator-Setup-";
        const string InstallerSuffix = ".exe";
        const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{6B1F6E52-3C0D-4C8E-9A57-1F2A7B9D4E10}_is1";

        static volatile string readyInstaller;

        /// <summary>True if the tag ("v1.4.0" or "1.4.0") is a higher version than current. Revision is ignored.</summary>
        public static bool IsNewer(string tag, Version current)
        {
            Version latest;
            if (string.IsNullOrEmpty(tag) || !Version.TryParse(tag.TrimStart('v', 'V'), out latest)) return false;
            var now = new Version(current.Major, current.Minor, Math.Max(0, current.Build));
            return latest > now;
        }

        public static ReleaseInfo ParseRelease(string json)
        {
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                return (ReleaseInfo)new DataContractJsonSerializer(typeof(ReleaseInfo)).ReadObject(ms);
        }

        public static ReleaseAsset FindInstaller(ReleaseInfo release)
        {
            if (release == null || release.Assets == null) return null;
            foreach (ReleaseAsset a in release.Assets)
                if (a.Name != null && a.Name.StartsWith(InstallerPrefix, StringComparison.Ordinal)
                    && a.Name.EndsWith(InstallerSuffix, StringComparison.Ordinal)) return a;
            return null;
        }

        public static ReleaseAsset FindAsset(ReleaseInfo release, string name)
        {
            if (release == null || release.Assets == null) return null;
            foreach (ReleaseAsset a in release.Assets)
                if (a.Name == name) return a;
            return null;
        }

        /// <summary>Reads a checksum file in the "sha256sum" format: "&lt;hash&gt;  &lt;file&gt;", or just the hash.</summary>
        public static bool TryParseChecksum(string text, string fileName, out string hash)
        {
            hash = null;
            if (text == null) return false;
            foreach (string raw in text.Split('\n'))
            {
                string[] parts = raw.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0 || !IsSha256(parts[0])) continue;
                if (parts.Length > 1 && parts[1].TrimStart('*') != fileName) continue;
                hash = parts[0].ToLowerInvariant();
                return true;
            }
            return false;
        }

        static bool IsSha256(string s)
        {
            if (s.Length != 64) return false;
            foreach (char c in s)
                if (!Uri.IsHexDigit(c)) return false;
            return true;
        }

        public static bool HashMatches(string path, string expectedHash)
        {
            using (var sha = SHA256.Create())
            using (var fs = File.OpenRead(path))
            {
                string actual = BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
                return actual == expectedHash;
            }
        }

        /// <summary>True when the program runs from the folder that setup.iss installed it to.
        /// A development build (build\Calculator.exe) therefore never replaces itself.</summary>
        public static bool IsInstalled(string exePath)
        {
            string dir = Path.GetFullPath(Path.GetDirectoryName(exePath)).TrimEnd('\\');
            foreach (string hive in new[] { "HKEY_CURRENT_USER", "HKEY_LOCAL_MACHINE" })
            {
                string loc = Registry.GetValue(hive + "\\" + UninstallKey, "InstallLocation", null) as string;
                if (!string.IsNullOrEmpty(loc) &&
                    string.Equals(Path.GetFullPath(loc).TrimEnd('\\'), dir, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>Starts the check on a background thread, so the window never waits for the network.</summary>
        public static void CheckInBackground()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                try { Check(); }
                catch { }   // no network or GitHub is down: try again next start
            });
        }

        static void Check()
        {
            string exe = typeof(Updater).Assembly.Location;
            if (!IsInstalled(exe)) return;

            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            ReleaseInfo release = ParseRelease(Get(ReleasesUrl));
            if (!IsNewer(release.TagName, typeof(Updater).Assembly.GetName().Version)) return;

            ReleaseAsset installer = FindInstaller(release);
            if (installer == null) return;
            ReleaseAsset checksum = FindAsset(release, installer.Name + ".sha256");
            string hash;
            if (checksum == null || !TryParseChecksum(Get(checksum.Url), installer.Name, out hash)) return;

            string dir = UpdateFolder;
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, installer.Name);
            Download(installer.Url, path);
            if (!HashMatches(path, hash))
            {
                File.Delete(path);
                return;
            }
            readyInstaller = path;
        }

        static string UpdateFolder
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Calculator", "update"); }
        }

        static HttpWebRequest Request(string url)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = "Calculator-Updater";
            req.Timeout = 15000;
            req.ReadWriteTimeout = 15000;
            return req;
        }

        static string Get(string url)
        {
            using (var resp = (HttpWebResponse)Request(url).GetResponse())
            using (var reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                return reader.ReadToEnd();
        }

        static void Download(string url, string path)
        {
            using (var resp = (HttpWebResponse)Request(url).GetResponse())
            using (var input = resp.GetResponseStream())
            using (var output = File.Create(path))
                input.CopyTo(output);
        }

        /// <summary>Runs the verified installer silently. Called as the window closes, so the update never interrupts the user.</summary>
        public static void InstallOnExit()
        {
            string path = readyInstaller;
            if (path == null) return;
            try { Process.Start(path, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS"); }
            catch { }
        }
    }
}
