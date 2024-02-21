using Palladium.Logging;
using PalladiumUpdater.Protocol;

namespace LauncherLogic;

public static class Utils
{
	public static async Task<UpdateSourceConfig?> TryGetGameInstallSourceConfig(Log log, string configFile)
	{
		UpdateSourceConfig? config = null;
		if (File.Exists(configFile))
		{
			try
			{
				config = await ConfigParser.Parse(configFile);
			}
			catch (Exception e)
			{
				// if the config is wrong that's fine
				log.Error($"Failed to parse config file \"{configFile}\": {e}");
			}
		}
		return config;
	}

	public static Task<T> BindToLoadingStack<T>(this Task<T> task, LoadingStack loadingStack)
	{
		if (task.IsCompleted)
		{
			return task;
		}

		LoadingStack.Handle handle = loadingStack.Acquire();
		task.ContinueWith(_ => handle.Dispose());
		return task;
	}
}