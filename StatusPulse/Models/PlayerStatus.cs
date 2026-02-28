using System;

namespace StatusPulse.Models
{
        public class PlayerStatus
{
            public string Name { get; set; } = string.Empty;
            public string Job { get; set; } = string.Empty;
            public string Territory { get; set; } = string.Empty;
            public string World { get; set; } = string.Empty;
            public bool InDuty { get; set; }
            public string DutyName { get; set; } = string.Empty;
            public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        }
    }

