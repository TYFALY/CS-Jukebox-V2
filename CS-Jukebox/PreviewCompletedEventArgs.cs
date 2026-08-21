using System;

namespace CS_Jukebox
{
    public enum PreviewCompletionReason
    {
        MusicEnded,
        EventEnded
    }

    public sealed class PreviewCompletedEventArgs : EventArgs
    {
        public PreviewCompletedEventArgs(PreviewCompletionReason reason)
        {
            Reason = reason;
        }

        public PreviewCompletionReason Reason { get; }
    }
}
