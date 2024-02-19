using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using PalladiumUpdater.Protocol;

namespace LauncherLogic;

public class LauncherState : IDisposable
{
	private readonly string? appExe;

	/// <summary>
	///     Emits null when it isn't installed, emits a value when it is installed or found.
	/// </summary>
	public IObservable<string?> InstalledGameVersion => installedGameVersion;

	/// <summary>
	///     Emits null when it isn't found, or the value when it is fetched.
	/// </summary>
	public IObservable<string?> AvailableGameVersion => availableGameVersion;

	public readonly IObservable<bool> GameUpdateAvailable;

	/// <summary>
	///     Emits null when it isn't found, or the value when it is fetched.
	/// </summary>
	public IObservable<string?> AvailableLauncherVersion => availableLauncherVersion;

	public readonly IObservable<bool> LauncherUpdateAvailable;

	public IObservable<bool> IsGameInstalled => isGameInstalled;

	public IObservable<bool> IsLoading => isLoading;

	/// <summary>
	///     Emits user facing message updates.
	/// </summary>
	public IObservable<string> UserMessage => userMessage;

	private readonly LoadingStack loadingStack;
	private readonly ReplaySubject<bool> isLoading;
	private readonly ReplaySubject<bool> isGameInstalled;
	private readonly ReplaySubject<string?> installedGameVersion;
	private readonly HttpClient client;
	private readonly ReplaySubject<string> userMessage;
	private readonly ReplaySubject<string?> availableLauncherVersion;
	private readonly ReplaySubject<string?> availableGameVersion;

	public LauncherState(string? appExe)
	{
		this.appExe = appExe;

		isLoading = new ReplaySubject<bool>();
		loadingStack = new LoadingStack(isLoading);

		isGameInstalled = new ReplaySubject<bool>(1);
		installedGameVersion = new ReplaySubject<string?>(1);
		availableGameVersion = new ReplaySubject<string?>(1);
		userMessage = new ReplaySubject<string>(1);
		availableLauncherVersion = new ReplaySubject<string?>(1);

		RefreshGameInstallState();

		client = new HttpClient();

		// network calls
		{
			GetAvailableGameVersion(client)
				.BindToLoadingStack(loadingStack)
				.ToObservable()
				.Subscribe(availableGameVersion.OnNext, availableGameVersion.OnError);
			GetAvailableLauncherVersion(client)
				.BindToLoadingStack(loadingStack)
				.ToObservable()
				.Subscribe(availableLauncherVersion.OnNext, availableLauncherVersion.OnError);
		}

		// state logic
		{
			GameUpdateAvailable = installedGameVersion.CombineLatest(availableGameVersion).Select(pair =>
			{
				(string? installedVersionValue, string? availableVersionValue) = pair;
				return installedVersionValue != null && availableVersionValue != null && installedVersionValue != availableVersionValue;
			});
			LauncherUpdateAvailable = availableLauncherVersion.Select(x =>
			{
				if (x == null)
				{
					return false;
				}
				return x != LauncherInstall.Version;
			});
		}

		// user facing messages
		{
			availableGameVersion.Where(x => x is null)
				.Subscribe(_ => { userMessage.OnNext("The server is unavailable."); });
			LauncherUpdateAvailable
				.Take(1) // only show once to user
				.Where(x => x)
				.Subscribe(_ => { userMessage.OnNext("A launcher update is available."); });

			GameUpdateAvailable
				.Take(1) // only show once to user
				.Where(x => x)
				.Subscribe(_ => { userMessage.OnNext("A game update is available!"); });
		}

		// error handling
		{
			isLoading.Subscribe(_ => { }, HandleException);
			installedGameVersion.Subscribe(_ => { }, HandleException);
			availableGameVersion.Subscribe(_ => { }, HandleException);
			availableLauncherVersion.Subscribe(_ => { }, HandleException);
			isGameInstalled.Subscribe(_ => { }, HandleException);
			LauncherUpdateAvailable.Subscribe(_ => { }, HandleException);
			GameUpdateAvailable.Subscribe(_ => { }, HandleException);
		}
	}

	private void RefreshGameInstallState()
	{
		installedGameVersion.OnNext(DateTime.Now.ToString());
		isGameInstalled.OnNext(GameInstall.IsGameInstalled());
		GameInstall.GetGameVersion()
			.BindToLoadingStack(loadingStack)
			.ToObservable()
			.Subscribe(installedGameVersion.OnNext, installedGameVersion.OnError);
	}

	private void HandleException(Exception e)
	{
		userMessage.OnNext("An internal error occured.");
	}

	private static async Task<string?> GetAvailableLauncherVersion(HttpClient client)
	{
		UpdateSourceConfig config = await LauncherInstall.GetLauncherUpdateSourceConfig();
		try
		{
			return await HttpDownloader.GetLatestVersion(client, config.LatestVersionUrl);
		}
		catch (Exception e)
		{
			_ = Console.Error.WriteLineAsync($"Failed to get the available launcher version: {e}");
			return null;
		}
	}

	private static async Task<string?> GetAvailableGameVersion(HttpClient client)
	{
		UpdateSourceConfig config = await GameInstall.GetGameUpdateSourceConfig();
		try
		{
			return await HttpDownloader.GetLatestVersion(client, config.LatestVersionUrl);
		}
		catch (Exception e)
		{
			_ = Console.Error.WriteLineAsync($"Failed to get the available game version: {e}");
			return null;
		}
	}

	public Task StartGame()
	{
		using LoadingStack.Handle handle = loadingStack.Acquire();
		try
		{
			GameInstall.StartGame();
			userMessage.OnNext("");
			return Task.CompletedTask;
		}
		catch (Exception e)
		{
			_ = Console.Error.WriteLineAsync($"Failed to start the game: {e}");
			userMessage.OnNext("Failed to start the game due to an internal error.");
			return Task.CompletedTask;
		}
	}

	public async Task StartUpdate()
	{
		if (await LauncherUpdateAvailable.FirstAsync())
		{
			using LoadingStack.Handle handle = loadingStack.Acquire();
			try
			{
				await UpdateLauncher();
			}
			catch (Exception e)
			{
				_ = Console.Error.WriteLineAsync($"Failed to update the launcher: {e}");
				userMessage.OnNext("Failed to update the launcher due to an internal error.");
			}
		}
		else if (await GameUpdateAvailable.FirstAsync())
		{
			using LoadingStack.Handle handle = loadingStack.Acquire();
			try
			{
				UpdateGame();
			}
			catch (Exception e)
			{
				_ = Console.Error.WriteLineAsync($"Failed to update the game: {e}");
				userMessage.OnNext("Failed to update the game due to an internal error.");
			}
		}
	}

	private void UpdateGame()
	{
		isGameInstalled.OnNext(false);
		installedGameVersion.OnNext(null);

		// TODO download
		// TODO update
		// TODO emit update
	}

	private async Task UpdateLauncher()
	{
		string? version = await AvailableLauncherVersion.FirstAsync();
		if (version == null)
		{
			throw new InvalidOperationException();
		}

		userMessage.OnNext($"Getting launcher update details...");
		UpdateSourceConfig config = await LauncherInstall.GetLauncherUpdateSourceConfig();

		string downloadedArchive;
		try
		{
			downloadedArchive = await HttpDownloader.GetArchive(client, config.ArchiveUrl, version, LauncherInstall.AppName, Observer.Create<float>(progress => { userMessage.OnNext($"Downloading launcher update: {Math.Round(progress * 100.0)}%"); }));
		}
		catch (Exception e)
		{
			_ = Console.Error.WriteLineAsync($"Failed to download the launcher update archive: {e}");
			userMessage.OnNext("Failed to download the launcher update.");
			return;
		}

		userMessage.OnNext($"Decompressing...");
		string downloadedUpdate = await ArchiveHandler.Decompress(downloadedArchive, LauncherInstall.AppName, version);

		userMessage.OnNext($"Copying files...");
		try
		{
			await Task.Run(async () => await SelfUpdate.InstallUpdateFromDirectory(downloadedUpdate, appExe, typeof(LauncherState).Assembly));
		}
		catch (Exception e)
		{
			_ = Console.Error.WriteLineAsync($"Failed to perform auto-update of launcher: {e}");
			userMessage.OnNext("Failed to perform auto-update of launcher.");
			return;
		}
	}

	public async Task StartDownload()
	{
		using LoadingStack.Handle handle = loadingStack.Acquire();
		try
		{
			string? version = await AvailableGameVersion.FirstAsync();
			if (version == null)
			{
				throw new InvalidOperationException();
			}

			userMessage.OnNext($"Getting game details...");
			UpdateSourceConfig config = await GameInstall.GetGameUpdateSourceConfig(); 

			string downloadedArchive;
			try
			{
				downloadedArchive = await HttpDownloader.GetArchive(client, config.ArchiveUrl, version, GameInstall.GameName, Observer.Create<float>(progress =>
				{
					userMessage.OnNext($"Downloading game: {Math.Round(progress * 100.0)}%");
				}));
			}
			catch (Exception e)
			{
				_ = Console.Error.WriteLineAsync($"Failed to download the game archive: {e}");
				userMessage.OnNext("Failed to download the game.");
				return;
			}

			userMessage.OnNext($"Decompressing...");
			string downloadedGame = await ArchiveHandler.Decompress(downloadedArchive, GameInstall.GameName, version);

			userMessage.OnNext($"Moving files...");
			try
			{
				await Task.Run(() => AppUpdate.InstallUpdateFromDirectory(downloadedGame, GameInstall.GameName));
			}
			catch (Exception e)
			{
				_ = Console.Error.WriteLineAsync($"Failed to install game files: {e}");
				userMessage.OnNext("Failed to install game files.");
				return;
			}
			userMessage.OnNext($"Game installed!");
			RefreshGameInstallState();
		}
		catch (Exception e)
		{
			_ = Console.Error.WriteLineAsync($"Failed to download the game: {e}");
			userMessage.OnNext("Failed to download the game due to an internal error.");
		}
	}

	/// <inheritdoc />
	public void Dispose()
	{
		isLoading.Dispose();
		isGameInstalled.Dispose();
		installedGameVersion.Dispose();
		client.Dispose();
		userMessage.Dispose();
		availableLauncherVersion.Dispose();
		availableGameVersion.Dispose();
	}
}