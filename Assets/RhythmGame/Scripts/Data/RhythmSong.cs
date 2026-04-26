using UnityEngine;

[CreateAssetMenu(menuName = "RhythmGame/RhythmSong", fileName = "NewRhythmSong")]
public class RhythmSong : ScriptableObject
{
    public string songId;
    public string title;
    public string artist;
    public AudioClip audioClip;
}
