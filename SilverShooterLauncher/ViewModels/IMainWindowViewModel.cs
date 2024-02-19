using System.Reactive;
using ReactiveUI;

namespace SilverShooterLauncher.ViewModels;

public interface IMainWindowViewModel
{
	string InstalledVersion { get ; }
	string LatestVersion { get ; }
	ReactiveCommand<Unit, Unit> PlayCommand { get ; }
	ReactiveCommand<Unit, Unit> UpdateCommand { get ; }
	string StatusText { get ; }
	ReactiveCommand<Unit, Unit> DownloadCommand { get ; }
	bool IsDownloadVisible { get ; }
	bool IsPlayVisible { get ; }
	bool IsUpdateVisible { get ; }
	string LauncherVersion { get; }
	bool IsLoading { get; }
}