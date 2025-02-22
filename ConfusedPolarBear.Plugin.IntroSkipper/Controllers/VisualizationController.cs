using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ConfusedPolarBear.Plugin.IntroSkipper.Controllers;

/// <summary>
/// Audio fingerprint visualization controller. Allows browsing fingerprints on a per episode basis.
/// </summary>
[Authorize(Policy = "RequiresElevation")]
[ApiController]
[Produces(MediaTypeNames.Application.Json)]
[Route("Intros")]
public class VisualizationController : ControllerBase
{
    private readonly ILogger<VisualizationController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="VisualizationController"/> class.
    /// </summary>
    /// <param name="logger">Logger.</param>
    public VisualizationController(ILogger<VisualizationController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Returns all show names and seasons.
    /// </summary>
    /// <returns>Dictionary of show names to a list of season names.</returns>
    [HttpGet("Shows")]
    public ActionResult<Dictionary<string, HashSet<string>>> GetShowSeasons()
    {
        _logger.LogDebug("Returning season names by series");

        var showSeasons = new Dictionary<string, HashSet<string>>();

        // Loop through all seasons in the analysis queue
        foreach (var kvp in Plugin.Instance!.AnalysisQueue)
        {
            // Check that this season contains at least one episode.
            var episodes = kvp.Value;
            if (episodes is null || episodes.Count == 0)
            {
                _logger.LogDebug("Skipping season {Id} (null or empty)", kvp.Key);
                continue;
            }

            // Peek at the top episode from this season and store the series name and season number.
            var first = episodes[0];
            var series = first.SeriesName;
            var season = GetSeasonName(first);

            // Validate the series and season before attempting to store it.
            if (string.IsNullOrWhiteSpace(series) || string.IsNullOrWhiteSpace(season))
            {
                _logger.LogDebug("Skipping season {Id} (no name or number)", kvp.Key);
                continue;
            }

            // TryAdd is used when adding the HashSet since it is a no-op if one was already created for this series.
            showSeasons.TryAdd(series, new HashSet<string>());
            showSeasons[series].Add(season);
        }

        return showSeasons;
    }

    /// <summary>
    /// Returns the names and unique identifiers of all episodes in the provided season.
    /// </summary>
    /// <param name="series">Show name.</param>
    /// <param name="season">Season name.</param>
    /// <returns>List of episode titles.</returns>
    [HttpGet("Show/{Series}/{Season}")]
    public ActionResult<List<EpisodeVisualization>> GetSeasonEpisodes(
        [FromRoute] string series,
        [FromRoute] string season)
    {
        var visualEpisodes = new List<EpisodeVisualization>();

        if (!LookupSeasonByName(series, season, out var episodes))
        {
            return NotFound();
        }

        foreach (var e in episodes)
        {
            visualEpisodes.Add(new EpisodeVisualization(e.EpisodeId, e.Name));
        }

        return visualEpisodes;
    }

    /// <summary>
    /// Fingerprint the provided episode and returns the uncompressed fingerprint data points.
    /// </summary>
    /// <param name="id">Episode id.</param>
    /// <returns>Read only collection of fingerprint points.</returns>
    [HttpGet("Fingerprint/{Id}")]
    public ActionResult<ReadOnlyCollection<uint>> GetEpisodeFingerprint([FromRoute] Guid id)
    {
        var queue = Plugin.Instance!.AnalysisQueue;

        // Search through all queued episodes to find the requested id
        foreach (var season in queue)
        {
            foreach (var needle in season.Value)
            {
                if (needle.EpisodeId == id)
                {
                    return Chromaprint.Fingerprint(needle);
                }
            }
        }

        return NotFound();
    }

    /// <summary>
    /// Erases all timestamps for the provided season.
    /// </summary>
    /// <param name="series">Show name.</param>
    /// <param name="season">Season name.</param>
    /// <response code="204">Season timestamps erased.</response>
    /// <response code="404">Unable to find season in provided series.</response>
    /// <returns>No content.</returns>
    [HttpDelete("Show/{Series}/{Season}")]
    public ActionResult EraseSeason([FromRoute] string series, [FromRoute] string season)
    {
        if (!LookupSeasonByName(series, season, out var episodes))
        {
            return NotFound();
        }

        _logger.LogInformation("Erasing timestamps for {Series} {Season} at user request", series, season);

        foreach (var e in episodes)
        {
            Plugin.Instance!.Intros.Remove(e.EpisodeId);
        }

        Plugin.Instance!.SaveTimestamps();

        return NoContent();
    }

    /// <summary>
    /// Returns the statistics for the most recent analysis.
    /// </summary>
    /// <response code="200">Analysis statistics.</response>
    /// <returns>AnalysisStatistics.</returns>
    [HttpGet("Statistics")]
    public ActionResult<AnalysisStatistics> GetAnalysisStatistics()
    {
        return Plugin.Instance!.AnalysisStatistics;
    }

    private string GetSeasonName(QueuedEpisode episode)
    {
        return "Season " + episode.SeasonNumber.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Lookup a named season of a series and return all queued episodes.
    /// </summary>
    /// <param name="series">Series name.</param>
    /// <param name="season">Season name.</param>
    /// <param name="episodes">Episodes.</param>
    /// <returns>Boolean indicating if the requested season was found.</returns>
    private bool LookupSeasonByName(string series, string season, out List<QueuedEpisode> episodes)
    {
        foreach (var queuedEpisodes in Plugin.Instance!.AnalysisQueue)
        {
            var first = queuedEpisodes.Value[0];
            var firstSeasonName = GetSeasonName(first);

            // Assert that the queued episode series and season are equal to what was requested
            if (
                !string.Equals(first.SeriesName, series, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(firstSeasonName, season, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            episodes = queuedEpisodes.Value;
            return true;
        }

        episodes = new List<QueuedEpisode>();
        return false;
    }
}
