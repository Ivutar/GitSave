using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reactive;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using GitSave.Models;
using GitSave.Tools;
using ReactiveUI;
using MessageBox.Avalonia.DTO;
using MessageBoxAvaloniaEnums = MessageBox.Avalonia.Enums;
using System.Reactive.Linq;

namespace GitSave.ViewModels;

public class MainWindowViewModel : ReactiveObject
{
    public MainWindowViewModel()
    {
        IObservable<bool> canExecuteNewCommand = this.WhenAnyValue(vm => vm.NewComment, (comment) => !string.IsNullOrEmpty(comment));
        IObservable<bool> canExecuteUpdateCommand = this.WhenAnyValue(vm => vm.LastComment, (comment) => !string.IsNullOrEmpty(comment));

        NewCommand = ReactiveCommand.Create(NewImpl, canExecuteNewCommand);
        RefreshCommand = ReactiveCommand.Create(RefreshImpl);
        UpdateCommand = ReactiveCommand.Create(UpdateImpl, canExecuteUpdateCommand);
        ResetCommand = ReactiveCommand.Create(ResetImpl);
        SetWorkFolderCommand = ReactiveCommand.Create(SetWorkFolderImpl);
        ResetToCommit = ReactiveCommand.Create(ResetToCommitImpl);

        this
            .WhenAnyValue(
                vm => vm.ShowAllCommits,
                vm => vm.Limit,
                (all, limit) => (all, limit))
            .Throttle(TimeSpan.FromSeconds(0.8))
            .DistinctUntilChanged()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(async _ => await LoadCommits()); //todo: as command ?

        _HasUpdates = Observable
            .Interval(TimeSpan.FromSeconds(0.8))
            .Select(_ => Observable.FromAsync(async () => await CheckUpdatesImpl()))
            .Concat()
            .DistinctUntilChanged()
            .ObserveOn(RxApp.MainThreadScheduler)
            .ToProperty(this, x => x.HasUpdates);

        WorkFolder = Directory.GetCurrentDirectory();
    }

    public IObservable<R> SelectAsync<T,R>(IObservable<T> source, Func<T,Task<R>> asyncSelector)
    {
        return source
            .Select(value => Observable.FromAsync(() => asyncSelector(value)))
            .Concat();
    }

    #region [ Properties ]

    private int _Limit = 25;
    public int Limit
    {
        get => _Limit;
        set => this.RaiseAndSetIfChanged(ref _Limit, value);
    }

    private string? _NewComment;
    public string? NewComment
    {
        get => _NewComment;
        set => this.RaiseAndSetIfChanged(ref _NewComment, value);
    }

    private string? _LastComment;
    public string? LastComment
    {
        get => _LastComment;
        set => this.RaiseAndSetIfChanged(ref _LastComment, value);
    }

    private Commit? _SelectedCommit;
    public Commit? SelectedCommit
    {
        get => _SelectedCommit;
        set => this.RaiseAndSetIfChanged(ref _SelectedCommit, value);
    }

    private string? _WorkFolder;
    public string? WorkFolder
    {
        get => _WorkFolder;
        set => this.RaiseAndSetIfChanged(ref _WorkFolder, value);
    }

    private bool _ShowAllCommits;
    public bool ShowAllCommits
    {
        get => _ShowAllCommits;
        set => this.RaiseAndSetIfChanged(ref _ShowAllCommits, value);
    }

    private readonly ObservableAsPropertyHelper<bool> _HasUpdates;
    public bool HasUpdates => _HasUpdates.Value;

    public ObservableCollection<Commit> Commits { get; } = new ObservableCollection<Commit>();

    #endregion

    #region [ Commands ]

    public ICommand NewCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand UpdateCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand SetWorkFolderCommand { get; }
    public ICommand ResetToCommit { get; }

    #endregion

    #region [ Helpers ]

    async Task<bool> CheckUpdatesImpl()
    {        
        return await Git.HasUpates(WorkFolder);
    }

    async Task NewImpl()
    {
        await Git.New(NewComment, WorkFolder);
        await LoadCommits();
        NewComment = "";
    }
    
    async Task RefreshImpl()
    {
        await LoadCommits();
        NewComment = "";
    }

    async Task UpdateImpl()
    {
        NewComment = "";
        await Git.Update(LastComment, WorkFolder);
        //Commits[0].Comment = LastComment;
        await LoadCommits();
    }

    async Task ResetImpl()
    {
        await Git.Reset(WorkFolder);
        NewComment = "";
        LastComment = await Git.LastComment(WorkFolder);
    }

    async Task SetWorkFolderImpl()
    {
        OpenFolderDialog dialog = new OpenFolderDialog
        {
            Title = "Select work folder"
        };

        if (Avalonia.Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            WorkFolder = await dialog.ShowAsync(desktop.MainWindow);
            await LoadCommits();
        }
    }

    async Task ResetToCommitImpl()
    {        

        if (Avalonia.Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            //dialog
            var messageBoxStandardWindow = MessageBox.Avalonia.MessageBoxManager.GetMessageBoxStandardWindow(
                new MessageBoxStandardParams
                {
                    ButtonDefinitions = MessageBoxAvaloniaEnums.ButtonEnum.OkAbort,
                    ContentHeader = $"Reset to commit {SelectedCommit.UUID}",
                    ContentMessage = $"{SelectedCommit.Comment}"
                });

            //confirmation
            var res = await messageBoxStandardWindow.ShowDialog(desktop.MainWindow);

            //reset to commit
            if (res == MessageBoxAvaloniaEnums.ButtonResult.Ok)
            {
                await Git.ResetToCommit(SelectedCommit.UUID, WorkFolder);
                await LoadCommits();
                NewComment = "";
            }
        }
    }

    async Task LoadCommits ()
    {
        Commits.Clear();

        var list = await Git.GetCommits(Limit, ShowAllCommits, WorkFolder);

        foreach(var commit in list)
            Commits.Add(commit);

        LastComment = await Git.LastComment(WorkFolder);
    }

    #endregion

}