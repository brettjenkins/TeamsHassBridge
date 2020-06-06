namespace TeamsHassBridge
{
    public class TeamsStatus
    {
        public States? TeamsState { get; set; }
        public bool? TeamsOnCall { get; set; }
        public bool? TeamsUnread { get; set; }

        public bool Equals(TeamsStatus obj)
        {
            return obj != null && (obj.TeamsState == TeamsState &&
                                    obj.TeamsOnCall == TeamsOnCall && obj.TeamsUnread == TeamsUnread);
        }

        public bool IsNull()
        {
            return (this.TeamsOnCall == null && this.TeamsState == null && this.TeamsUnread == null);
        }
    }
}
