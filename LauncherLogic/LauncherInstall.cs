using PalladiumUpdater.Protocol;

namespace LauncherLogic;

public class LauncherInstall
{
	public const string ConfigFile =  "SilverShooterLauncherConfig.txt";
	public const string AppName = "SilverShooterLauncher";
	public const string Version = "v0.1";
	
	public static async Task<UpdateSourceConfig> GetLauncherUpdateSourceConfig()
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
			ArchiveUrl = "https://raphaelbuquet.com/silvershooter/launcher/archive.zip",
			LatestVersionUrl = "https://raphaelbuquet.com/silvershooter/launcher/LatestVersion.txt"
		};
	}
}