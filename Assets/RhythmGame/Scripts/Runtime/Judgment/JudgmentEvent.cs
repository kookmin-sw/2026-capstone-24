public readonly struct JudgmentEvent
{
    public readonly int           channel;
    public readonly byte          midiNote;
    public readonly int           tick;
    public readonly double        scheduledTime;
    public readonly double        inputTime;
    public readonly JudgmentGrade grade;

    public JudgmentEvent(int channel, byte midiNote, int tick,
                         double scheduledTime, double inputTime, JudgmentGrade grade)
    {
        this.channel       = channel;
        this.midiNote      = midiNote;
        this.tick          = tick;
        this.scheduledTime = scheduledTime;
        this.inputTime     = inputTime;
        this.grade         = grade;
    }
}
