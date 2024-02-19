using System.Reactive;
using System.Reactive.Linq;
using LauncherLogic;
using ReactiveUI;

namespace SilverShooterLauncher.ViewModels;

public class DesignMainWindowViewModel : IMainWindowViewModel
{
	/// <inheritdoc />
	public string InstalledVersion { get; } = "Installed version: v0.1";

	/// <inheritdoc />
	public string LatestVersion { get; } = "Latest version: v0.1";

	/// <inheritdoc />
	public ReactiveCommand<Unit, Unit> PlayCommand { get; } = ReactiveCommand.Create(() => { }, Observable.Return(false));

	/// <inheritdoc />
	public ReactiveCommand<Unit, Unit> UpdateCommand { get; } = ReactiveCommand.Create(() => { }, Observable.Return(false));

	/// <inheritdoc />
	public string StatusText { get; } = "A launcher update is available.";

	/// <inheritdoc />
	public ReactiveCommand<Unit, Unit> DownloadCommand { get; } = ReactiveCommand.Create(() => { }, Observable.Return(true));

	/// <inheritdoc />
	public bool IsDownloadVisible { get; } = true;

	/// <inheritdoc />
	public bool IsPlayVisible { get; } = false;

	/// <inheritdoc />
	public bool IsUpdateVisible { get; } = false;

	/// <inheritdoc />
	public string LauncherVersion { get; } = $"Launcher version {LauncherInstall.Version}";

	/// <inheritdoc />
	public bool IsLoading { get; } = true;
}