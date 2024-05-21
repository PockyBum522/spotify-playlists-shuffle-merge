using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Quartz;
using Quartz.Impl;
using Quartz.Logging;
using Serilog;
using SpotifyAPI.Web;
using SpotifyPlaylistUtilities;
using SpotifyPlaylistUtilities.Logging;
using SpotifyPlaylistUtilities.Models;
using SpotifyPlaylistUtilities.Playlists;

namespace SpotifyPlaylistUtilitiesGui.Views;

public partial class MainView : UserControl
{
    private readonly ILogger _logger;

    public MainView()
    {
        InitializeComponent();
        
        var loggerConfiguration = LoggerSetup.ConfigureLogger();
        
        LoggerSetup.Logger = loggerConfiguration
            .MinimumLevel.Debug()
            .CreateLogger();

        _logger = LoggerSetup.Logger ?? throw new NullReferenceException();
        
        Task.Run(async () =>
        {
            await SetupQuartzSchedulerAndJobs();
        });
    }

    private async Task SetupQuartzSchedulerAndJobs()
    {
        LogProvider.SetCurrentLogProvider(new CustomSerilogLogProvider(_logger));

        // Grab the Scheduler instance from the Factory
        var factory = new StdSchedulerFactory();
        var scheduler = await factory.GetScheduler();

        // and start it off
        await scheduler.Start();

        // define the job and tie it to our ShufflePlaylistsJob class
        var weebletdaysDailyJob = JobBuilder.Create<ShufflePlaylistsJob>()
            .WithIdentity("playlistSaverJob", "group1")
            .Build();

        // Trigger the job to run now, and then repeat
        var trigger = TriggerBuilder.Create()
            .WithIdentity("playlistSaverJobTrigger", "group1")
            .StartNow()
            .WithSimpleSchedule(x => x
                .WithIntervalInHours(24)
                .RepeatForever())
            .Build();

        // Tell Quartz to schedule the job using our trigger
        await scheduler.ScheduleJob(weebletdaysDailyJob, trigger);    
    }
}

public class ShufflePlaylistsJob : IJob
{
    private List<SerializableWeightedTrack> _savedTrackData = new();
    
    private readonly ILogger _logger = LoggerSetup.Logger ?? throw new NullReferenceException();
    private SpotifyClient? _spotifyClient;
    private PlaylistManager? _playlistManager;
    private PlaylistShuffler? _playlistShuffler;
    
    public async Task Execute(IJobExecutionContext context)
    {
        await EnsureAuthenticatedSpotifyClient();
        
        if (_playlistManager is null || _playlistShuffler is null) throw new NullReferenceException();
        
        // If you need to get a list of playlists, uncomment this:
        await _playlistManager.PrintAllPlaylistData();
        
        
        
        _logger.Information("Finished ShuffleWeebletdays()");
    }
    
    private List<ManagedPlaylistTrack> RemoveDuplicateTracks(List<ManagedPlaylistTrack> mergedTracks)
    {
        var strippedOfDuplicatesTracks = mergedTracks
            .GroupBy(x => new {Id = x.Id})
            .Select(grp => grp.First())
            .ToList();
    
        return strippedOfDuplicatesTracks;
    }
    
    private async Task BackupPlaylist(string playlistIdToShuffle)
    {
        await EnsureAuthenticatedSpotifyClient();
    
        if (_playlistManager is null || _playlistShuffler is null) 
            throw new NullReferenceException();
        
        var playlist = await _playlistManager.GetPlaylistById(playlistIdToShuffle);
        
        _playlistManager.BackupTracksToJsonFile(playlist);
    }
    
    private async Task EnsureAuthenticatedSpotifyClient()
    {
        var spotifyAuthenticationManager = new SpotifyAuthenticationManager(_logger);
        
        _spotifyClient ??= await spotifyAuthenticationManager.GetAuthenticatedSpotifyClient();
        
        if (_spotifyClient is null) throw new NullReferenceException("You may need to call EnsureAuthenticatedSpotifyClient() first");
        
        _playlistManager ??= new PlaylistManager(
            _logger, 
            spotifyClient: _spotifyClient ?? throw new NullReferenceException());

        _playlistShuffler ??= new PlaylistShuffler(_logger, spotifyClient: _spotifyClient ?? throw new NullReferenceException());
    }
}
