using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace MyDrawVector.Services;

public sealed class FileAssociationService
{
    public const string Extension = ".mydraw";
    public const string ProgId = "MyDrawVector.Document";
    public const string ContentType = "application/x-mydrawvector";

    private const int ShcneAssocChanged = 0x08000000;
    private const int ShcnfIdList = 0x0000;

    public string? CurrentExecutablePath => Environment.ProcessPath;

    public bool IsRegisteredForCurrentExe()
    {
        var exe = CurrentExecutablePath;
        if (string.IsNullOrWhiteSpace(exe))
        {
            return false;
        }

        try
        {
            using var commandKey = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{ProgId}\shell\open\command");
            var command = commandKey?.GetValue(null) as string;
            return command is not null &&
                   command.Contains(exe, StringComparison.OrdinalIgnoreCase) &&
                   command.Contains("%1", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    public void Register()
    {
        var exe = CurrentExecutablePath;
        if (string.IsNullOrWhiteSpace(exe))
        {
            return;
        }

        using (var extensionKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{Extension}"))
        {
            extensionKey.SetValue(null, ProgId);
            extensionKey.SetValue("Content Type", ContentType);
            using var openWith = extensionKey.CreateSubKey("OpenWithProgids");
            openWith.SetValue(ProgId, string.Empty, RegistryValueKind.String);
        }

        using (var progIdKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}"))
        {
            progIdKey.SetValue(null, "MyDrawVector 向量圖");
            using var iconKey = progIdKey.CreateSubKey("DefaultIcon");
            iconKey.SetValue(null, $"\"{exe}\",0");
            using var commandKey = progIdKey.CreateSubKey(@"shell\open\command");
            commandKey.SetValue(null, $"\"{exe}\" \"%1\"");
        }

        using (var applicationKey = Registry.CurrentUser.CreateSubKey(
                   @"Software\Classes\Applications\MyDrawVector.exe"))
        {
            applicationKey.SetValue("FriendlyAppName", "MyDrawVector");
            using var commandKey = applicationKey.CreateSubKey(@"shell\open\command");
            commandKey.SetValue(null, $"\"{exe}\" \"%1\"");
            using var supported = applicationKey.CreateSubKey("SupportedTypes");
            supported.SetValue(Extension, string.Empty, RegistryValueKind.String);
        }

        NotifyShell();
    }

    public void Unregister()
    {
        try
        {
            using (var extensionKey = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{Extension}", writable: true))
            {
                if (extensionKey is not null)
                {
                    if (string.Equals(extensionKey.GetValue(null) as string, ProgId, StringComparison.OrdinalIgnoreCase))
                    {
                        extensionKey.DeleteValue(string.Empty, throwOnMissingValue: false);
                    }

                    if (string.Equals(extensionKey.GetValue("Content Type") as string, ContentType, StringComparison.OrdinalIgnoreCase))
                    {
                        extensionKey.DeleteValue("Content Type", throwOnMissingValue: false);
                    }

                    using (var openWith = extensionKey.OpenSubKey("OpenWithProgids", writable: true))
                    {
                        openWith?.DeleteValue(ProgId, throwOnMissingValue: false);
                        if (openWith is not null && openWith.ValueCount == 0 && openWith.SubKeyCount == 0)
                        {
                            openWith.Close();
                            extensionKey.DeleteSubKeyTree("OpenWithProgids", throwOnMissingSubKey: false);
                        }
                    }
                }
            }

            Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{ProgId}", throwOnMissingSubKey: false);
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\Applications\MyDrawVector.exe", throwOnMissingSubKey: false);

            using (var extensionKey = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{Extension}"))
            {
                if (extensionKey is not null && extensionKey.ValueCount == 0 && extensionKey.SubKeyCount == 0)
                {
                    extensionKey.Close();
                    Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{Extension}", throwOnMissingSubKey: false);
                }
            }

            NotifyShell();
        }
        catch
        {
            // The menu action reports failure through its own status text.
            throw;
        }
    }

    public static bool IsSupportedFile(string path)
    {
        return string.Equals(Path.GetExtension(path), Extension, StringComparison.OrdinalIgnoreCase);
    }

    private static void NotifyShell()
    {
        SHChangeNotify(ShcneAssocChanged, ShcnfIdList, IntPtr.Zero, IntPtr.Zero);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern void SHChangeNotify(int eventId, int flags, IntPtr item1, IntPtr item2);
}
