using System;

namespace DTAClient.Online
{
    public class QueuedMessage
    {
        private const int DEFAULT_DELAY = -1;
        private const int REPLACE_DELAY = 1;

        public QueuedMessage(string command, QueuedMessageType type, int priority) : 
            this(command, type, priority, DEFAULT_DELAY, false)
        {
        }

        public QueuedMessage(string command, QueuedMessageType type, int priority, bool replace) : 
            this(command, type, priority, replace ? REPLACE_DELAY : DEFAULT_DELAY, replace)
        {
        }

        public QueuedMessage(string command, QueuedMessageType type, int priority, int delay) :
            this(command, type, priority, delay, false)
        {
        }

        private QueuedMessage(string command, QueuedMessageType type, int priority, int delay, bool replace)
        {
            Command = command;
            MessageType = type;
            Priority = priority;
            Delay = delay;
            SendAt = Delay < 0  ? DateTime.Now : DateTime.Now.AddMilliseconds(Delay);
            Replace = replace;
        }

        public int ID { get; set; }

        public string Command { get; set; }

        public QueuedMessageType MessageType { get; set; }

        public int Priority { get; set; }

        public int Delay { get; set; }

        public DateTime SendAt { get; set; }

        public bool Replace { get; set; } = false;
    }
}