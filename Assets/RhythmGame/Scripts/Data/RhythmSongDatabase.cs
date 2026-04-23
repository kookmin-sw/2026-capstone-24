using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "RhythmGame/RhythmSongDatabase", fileName = "NewRhythmSongDatabase")]
public class RhythmSongDatabase : ScriptableObject
{
    public string instrumentKey;
    public RhythmSong[] songs = System.Array.Empty<RhythmSong>();

    public List<RhythmSong> GetSongsByDifficulty(RhythmDifficulty difficulty)
    {
        var result = new List<RhythmSong>();
        foreach (var song in songs)
        {
            foreach (var chart in song.charts)
            {
                if (chart != null && chart.difficulty == difficulty)
                {
                    result.Add(song);
                    break;
                }
            }
        }
        return result;
    }
}
