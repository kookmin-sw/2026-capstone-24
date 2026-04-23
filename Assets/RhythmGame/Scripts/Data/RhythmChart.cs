using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "RhythmGame/RhythmChart", fileName = "NewRhythmChart")]
public class RhythmChart : ScriptableObject
{
    public RhythmDifficulty difficulty;
    public float bpm;
    public float offset;
    public List<RhythmNote> notes = new();
}
