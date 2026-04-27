using UnityEngine;

[CreateAssetMenu(menuName = "RhythmGame/RhythmSongDatabase", fileName = "NewRhythmSongDatabase")]
public class RhythmSongDatabase : ScriptableObject
{
    public string instrumentKey;
    public RhythmSong[] songs = System.Array.Empty<RhythmSong>();
}
