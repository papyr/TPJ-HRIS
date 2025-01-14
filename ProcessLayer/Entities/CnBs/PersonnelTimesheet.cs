﻿using ProcessLayer.Helpers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcessLayer.Entities.CnBs
{
    public class PersonnelTimesheet
    {
        public Personnel Personnel { get; set; }
        public List<ComputedTimelog> ComputedTimelogs { get; set; } = new List<ComputedTimelog>();
        public decimal TotalNoofDays { get { return ComputedTimelogs?.Sum(x => x.NoofDays.ToDecimalPlaces(3)) ?? 0; } }
        public decimal TotalRegOT { get { return ComputedTimelogs?.Sum(x => x.RegOTHours.ToDecimalPlaces(2)) ?? 0; } }
        public decimal TotalSunOT { get { return ComputedTimelogs?.Sum(x => x.SunOTHours.ToDecimalPlaces(2)) ?? 0; } }
        public decimal TotalHoliday { get { return ComputedTimelogs?.Sum(x => x.Holiday.ToDecimalPlaces(3)) ?? 0; } }
        public decimal TotalHolExc { get { return ComputedTimelogs?.Sum(x => x.HolExcHours.ToDecimalPlaces(2)) ?? 0; } }
        public decimal TotalHazard { get { return ComputedTimelogs?.Sum(x => x.HazardHours.ToDecimalPlaces(2)) ?? 0; } }
        public decimal TotalNightDiffEarly { get { return ComputedTimelogs?.Sum(x => x.NightDiffEarlyHours) ?? 0; } }
        public decimal TotalNightDifflate { get { return ComputedTimelogs?.Sum(x => x.NightDiffLateHours) ?? 0; } }
        public decimal HazardRate { get { return ComputedTimelogs.Where(x => x.IsHazard).Select(x => x.HazRate).FirstOrDefault(); } }
        public decimal HighRiskRate { get { return ComputedTimelogs.Where(x => x.IsHighRisk).Select(x => x.HighRiskRate).FirstOrDefault(); } }
        public decimal TotalHigRisk { get { return ComputedTimelogs?.Sum(x => x.HighRiskHours) ?? 0; } }
    }
}
