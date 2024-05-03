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
            .WithIdentity("weebletdaysDailyJob", "group1")
            .Build();

        // Set up when to run it (at 3am)
        var runAt = new DateTimeOffset(
            DateTimeOffset.Now.Year,
            DateTimeOffset.Now.Month,
            DateTimeOffset.Now.Day,
            3,
            0,
            0,
            new TimeSpan(-5, 0, 0)
        );

        // If we're beyond 3am, then set it to the next 3am (Tomorrow)
        if (DateTimeOffset.Now > runAt)
            runAt += TimeSpan.FromDays(1);

        Console.WriteLine($"Will run next at: {runAt.ToString()}");
        
        // Trigger the job to run now, and then repeat
        var trigger = TriggerBuilder.Create()
            .WithIdentity("weebletdaysDailyJobTrigger", "group1")
            .StartAt(runAt)
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
        // await _playlistManager.PrintAllPlaylistData();
        
        var curated = await _playlistManager.GetPlaylistByName("Curated Weebletdays");
        var selectSelections = await _playlistManager.GetPlaylistByName("Select Selections");
        var selectDaily = await _playlistManager.GetPlaylistByName("Weebletdays Select Daily");
        
        // Backup all playlists
        _playlistManager.BackupTracksToJsonFile(curated);
        _playlistManager.BackupTracksToJsonFile(selectSelections);
        _playlistManager.BackupTracksToJsonFile(selectDaily);
        
        await _playlistManager.DeleteAllPlaylistTracksOnSpotify(selectDaily);

        var curatedRandomTracks = await _playlistShuffler.GetRandomTracksWithPickWeightsFrom(curated, 160);
        var selectSelectionsRandomTracks = await _playlistShuffler.GetRandomTracksWithPickWeightsFrom(selectSelections, 160);

        var mergedTracks = curatedRandomTracks.Concat(selectSelectionsRandomTracks).ToList();

        var strippedOfDuplicatesTracks = RemoveDuplicateTracks(mergedTracks);

        await _playlistManager.AddTracksToPlaylistOnSpotify(selectDaily, strippedOfDuplicatesTracks);
        
        _playlistShuffler.IncrementPickWeightsForTracks(strippedOfDuplicatesTracks);
        
        _playlistShuffler.DecrementPickWeightsForAllSavedTracks();

        _logger.Information("Finished ShuffleWeebletdays()");
        
        
        await ShufflePlaylistById("4M8XUja3GZw92EsD1QF9SI"); // Tally Hall and Stuf
        await ShufflePlaylistById("0ImDFHxZXgJ1m88ogSHU4L"); // Crimbus
        await ShufflePlaylistById("2SK04Gnd7kd92X1NjNSLHr"); // Curated Weebletdays
        await ShufflePlaylistById("26icuIX7tMnCk1KMSqAUsq"); // Art Music
        await ShufflePlaylistById("7pnXJ7jWswV32QGjJwyuFY"); // Pixel Gardner
        await ShufflePlaylistById("6RECxevyNJ1ysJaAjhHSmQ"); // Pixel Gardner - To Check
        await ShufflePlaylistById("1gZqNgs8xccDNTXbBhZphq"); // Beeblet Chill
        await ShufflePlaylistById("1N5tVO8jvxkXeckWCulp4G"); // Weebletdays Reserves
        await ShufflePlaylistById("3SIDzKeTUDDni499NE3tWr"); // Jazz
        await ShufflePlaylistById("5iyF8fuEdbdd1IWkYvycds"); // Muzicalz
        await ShufflePlaylistById("7zBTbIZz2lMy31TQlZvI5m"); // Metal
        await ShufflePlaylistById("6tx5BB9sVWpnbORkYX8Fqn"); // Our Songs <3
    }
    
    private List<ManagedPlaylistTrack> RemoveDuplicateTracks(List<ManagedPlaylistTrack> mergedTracks)
    {
        var strippedOfDuplicatesTracks = mergedTracks
            .GroupBy(x => new {Id = x.Id})
            .Select(grp => grp.First())
            .ToList();
    
        return strippedOfDuplicatesTracks;
    }
    
    private async Task ShufflePlaylistById(string playlistIdToShuffle)
    {
        await EnsureAuthenticatedSpotifyClient();
    
        if (_playlistManager is null || _playlistShuffler is null) 
            throw new NullReferenceException();
        
        var playlist = await _playlistManager.GetPlaylistById(playlistIdToShuffle);
        
        _playlistManager.BackupTracksToJsonFile(playlist);
        
        await _playlistShuffler.ShuffleAllIn(playlist, false);
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
