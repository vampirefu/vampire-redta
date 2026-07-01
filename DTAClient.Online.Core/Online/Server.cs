namespace DTAClient.Online
{
    public struct Server
    {
        public Server(string host, string name, int[] ports)
        {
            Host = host;
            Name = name;
            Ports = ports;
        }

        public string Host;
        public string Name;
        public int[] Ports;
    }
}