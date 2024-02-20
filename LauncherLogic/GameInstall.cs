using System.Diagnostics;
using PalladiumUpdater.Protocol;

namespace LauncherLogic;

public class GameInstall
{
	public const string GameName = "SilverShooter";
	public const string GameExe = "SilverShooter.exe";
	public const string ConfigFile = "SilverShooterConfig.txt";

	public static bool IsGameInstalled()
	{
		var exe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), GameName, GameExe);
		return File.Exists(exe);
	}

	public static void MockGameInstall(string mockVersion)
	{
		var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), GameName);
		var exe = Path.Combine(dir, GameExe);
		if (!Directory.Exists(dir))
		{
			Directory.CreateDirectory(dir);
		}

		// always overwrite.
		File.WriteAllBytes(exe, Array.Empty<byte>());
		HttpDownloader.MockLocalVersion(GameName, mockVersion);
	}

	public static void DeleteGameInstall()
	{
		var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), GameName);
		if (Directory.Exists(dir))
		{
			Directory.Delete(dir, true);
		}
	}

	public static Process? StartGame()
	{
		var exe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), GameName, GameExe);
		var psi = new ProcessStartInfo()
		{
			FileName = exe,
			UseShellExecute = true
		};
		return Process.Start(psi);
	}

	public static Task<string?> GetGameVersion()
	{
		return HttpDownloader.GetLocalVersion(GameName);
	}

	public static async Task<UpdateSourceConfig> GetGameUpdateSourceConfig()
	{
		var config = await Utils.TryGetGameInstallSourceConfig(ConfigFile);
		if (config == null)
		{
			config = DefaultUpdateSourceConfig();
		}
		return config.Value;
	}

	private static UpdateSourceConfig DefaultUpdateSourceConfig()
	{
		return new UpdateSourceConfig()
		{
			ArchiveUrl = "https://raphaelbuquet.com/silvershooter/game/archive.zip",
			LatestVersionUrl = "https://raphaelbuquet.com/silvershooter/game/LatestVersion.txt"
		};
	}
}