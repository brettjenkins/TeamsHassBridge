namespace TeamsHassBridge
{
    public class TeamsStatus
    {
        public States? TeamsState { get; set; }
        public bool? TeamsOnCall { get; set; }
        public bool? TeamsUnread { get; set; }
        public bool? TeamsIdle { get; set; }

        public bool Equals(TeamsStatus obj)
        {
            return obj != null && (obj.TeamsState == TeamsState &&
                                   obj.TeamsOnCall == TeamsOnCall && 
                                   obj.TeamsUnread == TeamsUnread && 
                                   obj.TeamsIdle == TeamsIdle);
        }

        public bool IsNull()
        {
            return (!TeamsOnCall.HasValue && !TeamsState.HasValue && !TeamsUnread.HasValue && !TeamsIdle.HasValue);
        }
    }
}
