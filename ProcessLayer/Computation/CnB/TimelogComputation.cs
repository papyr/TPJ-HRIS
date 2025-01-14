﻿using ProcessLayer.Entities;
using ProcessLayer.Entities.CnB;
using ProcessLayer.Entities.CnBs;
using ProcessLayer.Entities.Kiosk;
using ProcessLayer.Helpers;
using ProcessLayer.Helpers.Enumerable;
using ProcessLayer.Helpers.ObjectParameter.Payroll;
using ProcessLayer.Processes;
using ProcessLayer.Processes.CnB;
using ProcessLayer.Processes.HR;
using ProcessLayer.Processes.Kiosk;
using ProcessLayer.Processes.Kiosks;
using ProcessLayer.Processes.Lookups;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProcessLayer.Computation.CnB
{
    public sealed class TimelogComputation
    {
        public static readonly Lazy<TimelogComputation> Instance = new Lazy<TimelogComputation>(() => new TimelogComputation());
        private List<NonWorkingDays> NonWorkingDays { get; set; }
        private List<LateDeduction> LateDeductions { get; set; }
        public PayrollType Monthly { get; set; }
        private TimelogComputation()
        {
            LateDeductions = LateDeductionProcess.Instance.Value.GetList();
        }

        public List<PersonnelTimesheet> GenerateTimesheet(DateTime start, DateTime end, long? personnelID, int? departmentID)
        {
            List<PersonnelTimesheet> timesheets = new List<PersonnelTimesheet>();
            var emp = PersonnelProcess.GetListByDepartment(start, personnelID, departmentID);
            timesheets.AddRange(emp?.Select(x => new PersonnelTimesheet { Personnel = x }));
            if (timesheets?.Any() ?? false)
            {
                NonWorkingDays = NonWorkingDaysProcess.Instance.Value.GetNonWorkingDays(start, end);

                foreach (var timesheet in timesheets)
                {
                    if (timesheet.Personnel.EmploymentStatusId != 1) throw new Exception("Cannot compute timesheet for " + timesheet.Personnel.FullName + ". Inactive employment status.");
                    if (!(timesheet.Personnel._Schedules?.Any() ?? false)) throw new Exception("Cannot compute timesheet for " + timesheet.Personnel.FullName + ". No Schedule found.");
                    if (!(timesheet.Personnel._AssignedLocation?.Any() ?? false)) throw new Exception("Cannot compute timesheet for " + timesheet.Personnel.FullName + ". No Assigned Location found.");
                }
                foreach (var timesheet in timesheets)
                {
                    Compute(timesheet, start, end);
                }
                return timesheets;
            }
            else
                throw new Exception("No personnel found.<br />-Must be employement status is not active");
        }
       
        private void Compute(PersonnelTimesheet timesheet, DateTime periodStart, DateTime periodEnd)
        {
            List<TimeLog> timelogs = TimeLogProcess.Get(timesheet.Personnel.ID, periodStart.AddDays(-5), periodEnd);
            List<OTRequest> approvedotrequests = OTRequestProcess.Instance.Value.GetApprovedOT(timesheet.Personnel.ID, periodStart, periodEnd);
            List<OuterPortRequest> approvedouterportrequests = OuterPortRequestProcess.Instance.Value.GetApprovedOuterPort(timesheet.Personnel.ID, null, periodStart, periodEnd);
            List<HighRiskRequest> approvedhighriskrequests = HighRiskRequestProcess.Instance.Value.GetApproved(timesheet.Personnel.ID, periodStart, periodEnd);
            List<LeaveRequest> approvedleaverequests = LeaveRequestProcess.Instance.Value.GetLeaveForPayroll(timesheet.Personnel.ID, periodStart, periodEnd);
            foreach (LeaveRequest l in approvedleaverequests)
            {
                if (l._ComputedLeaveCredits?.Any() ?? false)
                {
                    l.ComputedLeaveCredits -= l._ComputedLeaveCredits.Sum(x => x.LeaveCreditUsed);
                }
            }
            DateTime start = periodStart.Date;
            DateTime end = periodEnd.Date;
            while (start <= end)
            {
                //Get Schedule
                #region schedule per day
                ScheduleType sched = GlobalHelper.GetSchedule(timesheet.Personnel._Schedules, start) ?? new ScheduleType();
                DateTime starttime = start;
                DateTime breaktime = start;
                DateTime breaktimeend = start;
                DateTime endtime = start;
                DateTime startnight1 = start + PayrollParameters.CNBInstance.Value.NightDiffStartTime1;
                DateTime endnight1 = start + PayrollParameters.CNBInstance.Value.NightDiffEndTime1;
                DateTime startnight2 = start + PayrollParameters.CNBInstance.Value.NightDiffStartTime2;
                DateTime endnight2 = start + PayrollParameters.CNBInstance.Value.NightDiffEndTime2;

                if (endnight1 < startnight1)
                    endnight1 = endnight1.AddDays(1);

                if (endnight2 < startnight1)
                    endnight2 = endnight2.AddDays(1);

                if (sched.BreakTime.HasValue) breaktime = start + sched.BreakTime.Value;

                if (sched.BreakTimeHour.HasValue) breaktimeend = breaktime.AddHours(sched.BreakTimeHour.Value);

                if (sched.TimeIn.HasValue) starttime += sched.TimeIn.Value;

                if (sched.TimeOut.HasValue)
                {
                    endtime += sched.TimeOut.Value;
                    if (endtime < starttime)
                        endtime = endtime.AddDays(1);
                }
                #endregion
                DateTime defbt = start + PayrollParameters.CNBInstance.Value.DefaultBreaktime;
                DateTime defbtend = defbt.AddHours(PayrollParameters.CNBInstance.Value.DefaultBreaktimeHour);
                var loc = PersonnelAssignedLocationProcess.GetCurrent(timesheet.Personnel.ID, start)?._Location ?? new Location();

                ComputedTimelog details = new ComputedTimelog
                {
                    Date = start,
                    Assigned = loc?.Description,
                    Schedule = GlobalHelper.GetSchedule(timesheet.Personnel._Schedules, start)
                };

                //Get Time Log
                #region timelog per day
                details.Login = timelogs.Where(x => (x.LoginDate >= starttime && x.LogoutDate <= endtime)
                                    || (starttime >= x.LoginDate && endtime <= x.LogoutDate)
                                    || (starttime <= x.LoginDate && endtime <= x.LogoutDate && endtime >= x.LoginDate)
                                    || (x.LoginDate <= starttime && x.LogoutDate <= endtime && x.LogoutDate >= endtime)
                                    || (x.LoginDate?.Date == start.Date)).OrderBy(x => x.LoginDate).Select(x => x.LoginDate).FirstOrDefault();

                details.Logout = timelogs.Where(x => (x.LoginDate >= starttime && x.LogoutDate <= endtime)
                                    || (starttime >= x.LoginDate && endtime <= x.LogoutDate)
                                    || (starttime <= x.LoginDate && endtime <= x.LogoutDate && endtime >= x.LoginDate)
                                    || (x.LoginDate <= starttime && x.LogoutDate <= endtime && x.LogoutDate >= endtime)
                                    || (x.LoginDate?.Date == start.Date)).OrderByDescending(x => x.LogoutDate).Select(x => x.LogoutDate).FirstOrDefault();
                
                details.Logout = details.Logout ?? details.Login;

                details.Login = details.Login?.AddSeconds(-(details.Login?.Second ?? 0));
                details.Logout = details.Logout?.AddSeconds(-(details.Logout?.Second ?? 0));
                #endregion
                LeaveRequest leave = approvedleaverequests.Where(x => starttime.Date >= x.RequestedDate && x.ApprovedLeaveCredits.HasValue && x.ApprovedLeaveCredits > 0).FirstOrDefault();
                float leaveCredits = (leave?.ApprovedLeaveCredits ?? 0) - (leave?.ComputedLeaveCredits ?? 0);
                float leaveCreditsUsed = 0;


                OTRequest earlyOT = approvedotrequests.Where(x => x.RequestDate.Date == starttime.Date && x.OTType == OTType.Early).FirstOrDefault();
                OTRequest afterWorkOT = approvedotrequests.Where(x => x.RequestDate.Date == starttime.Date && x.OTType == OTType.After).FirstOrDefault();
                OTRequest wholeDayOT = approvedotrequests.Where(x => x.RequestDate.Date == starttime.Date && x.OTType == OTType.Whole).FirstOrDefault();

                if ((leave?.ID ?? 0) == 0)
                {
                    if ((earlyOT?.ID ?? 0) > 0 && !(earlyOT?.IsOffice ?? false))
                        details.Login = earlyOT.StartDateTime;
                    if ((afterWorkOT?.ID ?? 0) > 0 && !(afterWorkOT?.IsOffice ?? false))
                        details.Logout = afterWorkOT.EndDateTime;
                    if ((wholeDayOT?.ID ?? 0) > 0 && !(wholeDayOT?.IsOffice ?? false))
                    {
                        details.Login = earlyOT.StartDateTime;
                        details.Logout = afterWorkOT.EndDateTime;
                    }
                }

                //get outer port request on specific date
                bool needTimeLog = false;
                OuterPortRequest outerPort = approvedouterportrequests.Where(x => starttime.Date >= x.StartDate && (starttime.Date <= x.EndDate || !x.EndDate.HasValue)).FirstOrDefault();
                HighRiskRequest highRisk = approvedhighriskrequests.Where(x => starttime.Date == x.RequestDate.Date).FirstOrDefault();

                NonWorkingDays holiday = NonWorkingDays.Where(x => (((x.Yearly ?? false) && x.Day?.Month == start.Month && x.Day?.Day == start.Day) || x.Day == start) && (x.LocationID == loc.ID || (x.IsGlobal ?? false))).FirstOrDefault();
                DateTime? prevDate = null;

                if ((outerPort?.ID ?? 0) > 0)
                {
                    details.IsHazard = true;
                    details.HazRate = outerPort._Location?.HazardRate ?? 0;
                    details.Assigned = outerPort._Location?.Prefix;
                    needTimeLog = outerPort._Location?.RequiredTimeLog ?? false;

                    if (outerPort.IsHighRisk ?? false)
                    {
                        if (outerPort.HasQuarantine ?? false)
                        {
                            if (outerPort.QuarantineDateEnd != null && outerPort.QuarantineDateEnd < start) //same day or after?????
                            {
                                details.IsHighRisk = true;
                            }
                        }
                        else
                        {
                            details.IsHighRisk = true;
                        }
                    }

                    if ((outerPort._Location.WithAdditionalForExtension ?? false) && outerPort.StartDate?.AddMonths(PayrollParameters.CNBInstance.Value.ExtendedMonths) <= start)
                    {
                        details.IsExtended = true;
                    }


                    holiday = NonWorkingDays.Where(x => (((x.Yearly ?? false) && x.Day?.Month == start.Month && x.Day?.Day == start.Day) || x.Day == start) && (x.LocationID == outerPort._Location?.ID || (x.IsGlobal ?? false))).FirstOrDefault();
                }

                if ((holiday?.ID ?? 0) > 0)
                {
                    prevDate = GlobalHelper.GetPrevSchedDate(timesheet.Personnel._Schedules, starttime, NonWorkingDays, outerPort?._Location?.ID ?? loc?.ID);
                }
                details.HighRiskRate = PayrollParameters.CNBInstance.Value.HighRiskRate;
                details.ExtensionRate = PayrollParameters.CNBInstance.Value.ExtensionRate;
                if ((highRisk?.ID ?? 0) > 0)
                {
                    details.IsHighRisk = true;
                }
                if (((holiday?.ID ?? 0) == 0 || ((holiday?.ID ?? 0) > 0 && (holiday?.NonWorkingType ?? 0) < 3)) && (sched?.ID ?? 0) > 0 && leaveCredits > 0)
                {
                    if ((leave?.ID ?? 0) > 0)
                    {
                        leaveCreditsUsed = leaveCredits > 1 ? 1 : leaveCredits;
                        leave.ComputedLeaveCredits = (leave.ComputedLeaveCredits ?? 0) + leaveCreditsUsed;
                    }
                }
                if (leaveCreditsUsed == 1)
                {
                    details.NoofDays = (decimal)leaveCreditsUsed;
                }
                else if (((holiday?.ID ?? 0) > 0) || (sched?.ID ?? 0) == 0 || start.DayOfWeek == DayOfWeek.Sunday)
                {
                    SundayAndHolidayComputation(timesheet, outerPort?._Location?.WithHolidayAndSunday ?? false, needTimeLog, approvedouterportrequests, timelogs, start, sched, starttime, endtime, startnight1, endnight1, startnight2, endnight2, defbt, defbtend, details, (wholeDayOT ?? earlyOT) ?? afterWorkOT, holiday, prevDate);
                }
                else if (!needTimeLog && details.IsHazard)
                {
                    details.NoofDays = 1;
                }
                else if (sched.AtHome ?? false)
                {
                    WorkFromHomeComputation(timesheet, details, (wholeDayOT ?? earlyOT) ?? afterWorkOT);
                }
                else
                {
                    if (sched.MustBePresentOnly ?? false)
                    {
                        if (!sched.TimeIn.HasValue && !sched.TimeOut.HasValue)
                        {
                            details.NoofDays = 1;
                        }
                    }
                    else
                    {
                        if (!sched.TimeIn.HasValue && !sched.TimeOut.HasValue) //Flexi
                        {
                            FlexiComputation(timesheet, approvedotrequests, end, sched, starttime, breaktime, breaktimeend, endtime, startnight1, endnight1, startnight2, endnight2, details);
                        }
                        else //Regular Hours
                        {
                            RegularComputation(timesheet, start, sched, starttime, breaktime, breaktimeend, endtime, startnight1, endnight1, startnight2, endnight2, details, earlyOT, afterWorkOT);
                        }
                    }
                }
                if(leaveCreditsUsed > 0)
                {
                    details.LeaveDesc = leave._LeaveType?.Description ?? "";
                    details.NoofDays += (decimal)leaveCreditsUsed;
                    if (details.NoofDays > 1)
                    {
                        details.NoofDays = 1;
                    }
                }
                start = start.AddDays(1);
                timesheet.ComputedTimelogs.Add(details);
            }
        }

        private static void WorkFromHomeComputation(PersonnelTimesheet timesheet, ComputedTimelog details, OTRequest ot)
        {
            details.NoofDays = 1;

            if ((ot?.ID ?? 0) > 0 || timesheet.Personnel.AutoOT)
            {
                DateTime? startot = details.Login;
                DateTime? endot = details.Logout;

                if (endot > startot)
                {
                    details.RegOTHours = GlobalHelper.SubtractDate(endot, startot) / PayrollParameters.CNBInstance.Value.Minutes;
                }
            }

        }

        private void RegularComputation(PersonnelTimesheet timesheet, DateTime start, ScheduleType sched, DateTime starttime, DateTime breaktime, DateTime breaktimeend, DateTime endtime, DateTime startnight1, DateTime endnight1, DateTime startnight2, DateTime endnight2, ComputedTimelog details, OTRequest earlyOT, OTRequest afterWorkOT)
        {
            if (!details.Login.HasValue && !details.Logout.HasValue)
            {
                details.NoofDays = 0;
            }
            else
            {
                int totalRegularMinutes = GlobalHelper.SubtractDate(endtime, starttime) - GlobalHelper.SubtractDate(breaktimeend, breaktime);
                int late = 0;
                int undertime = 0;
                //compute late
                if (starttime < details.Login)
                {
                    late = GlobalHelper.SubtractDate(details.Login, starttime);
                    LateDeduction lateded = LateDeductions.Where(x => (start + x.TimeIn) <= details.Login).OrderByDescending(x => x.TimeIn).FirstOrDefault();
                    if ((lateded?.ID ?? 0) > 0)
                    {
                        if (lateded.DeductedHours.HasValue)
                            late = (lateded.DeductedHours ?? 0) * (int)PayrollParameters.CNBInstance.Value.Minutes;
                    }

                    if (sched.BreakTime.HasValue && sched.BreakTimeHour.HasValue)
                    {
                        if (breaktime < details.Login &&
                            breaktime.AddHours(sched.BreakTimeHour ?? 0) > details.Login &&
                            breaktime.AddHours(sched.BreakTimeHour ?? 0) < details.Logout)
                            late -= GlobalHelper.SubtractDate(breaktime.AddHours(sched.BreakTimeHour ?? 0), details.Login);
                        else if (breaktime.AddHours(sched.BreakTimeHour ?? 0) <= details.Login)
                        {
                            late -= sched.BreakTimeHour ?? 0;
                        }
                    }
                }
                //compute undertime
                if (details.Logout.HasValue && details.Logout < endtime)
                {
                    if (sched.BreakTime.HasValue && sched.BreakTimeHour.HasValue)
                    {
                        if (breaktime.AddHours(sched.BreakTimeHour ?? 0) > details.Logout && breaktime < details.Logout)
                            undertime = GlobalHelper.SubtractDate(endtime, breaktime.AddHours(sched.BreakTimeHour ?? 0));
                        else if (breaktime > details.Logout)
                            undertime = GlobalHelper.SubtractDateWithBreakTime(endtime, details.Logout,sched.BreakTimeHour ?? 0);
                        else if (breaktime.AddHours(sched.BreakTimeHour ?? 0) < details.Logout)
                            undertime = GlobalHelper.SubtractDate(endtime, details.Logout);
                        else
                            undertime = GlobalHelper.SubtractDate(endtime, breaktime.AddHours(sched.BreakTimeHour ?? 0));
                    }
                    else
                        undertime = GlobalHelper.SubtractDate(endtime, details.Logout);
                    totalRegularMinutes -= undertime;
                }

                //compute ot after office
                if ((afterWorkOT?.ID ?? 0) > 0 || timesheet.Personnel.AutoOT)
                {
                    DateTime? startot = endtime;
                    DateTime? endot = details.Logout;

                    if (endot > startot)
                    {
                        int totolotminutes = GlobalHelper.SubtractDate(endot, startot);

                        int totalotminutesremain = totolotminutes - late;
                        if (totalotminutesremain <= 0)
                            late = Math.Abs(totalotminutesremain);
                        else
                        {
                            late = 0;
                            details.RegOTHours = totalotminutesremain / PayrollParameters.CNBInstance.Value.Minutes;
                        }
                    }
                }

                if ((earlyOT?.ID ?? 0) > 0)
                {
                    DateTime? startot = details.Login;
                    DateTime? endot = starttime;

                    if (endot > startot)
                        details.RegOTHours += GlobalHelper.SubtractDate(endot, startot) / PayrollParameters.CNBInstance.Value.Minutes;
                }


                totalRegularMinutes -= late;

                NighDiffComputation(starttime, endtime, startnight1, endnight1, startnight2, endnight2, details);

                details.NoofDays = totalRegularMinutes < 0 ? 0 : (totalRegularMinutes / (decimal)PayrollParameters.CNBInstance.Value.TotalMinutesPerDay);
            }
        }

        private static void FlexiComputation(PersonnelTimesheet timesheet, List<OTRequest> approvedotrequests, DateTime end, ScheduleType sched, DateTime starttime, DateTime breaktime, DateTime breaktimeend, DateTime endtime, DateTime startnight1, DateTime endnight1, DateTime startnight2, DateTime endnight2, ComputedTimelog details)
        {
            if (!details.Login.HasValue && !details.Logout.HasValue)
            {
                details.NoofDays = 0;
            }
            else
            {
                int totalminutes = GlobalHelper.SubtractDate(details.Logout, details.Login);
                if (sched.BreakTime.HasValue && sched.BreakTimeHour.HasValue)
                {
                    if ((details.Login > breaktime && breaktimeend > details.Login) && breaktimeend < details.Logout)
                    {
                        totalminutes = GlobalHelper.SubtractDate(details.Logout, breaktimeend);
                    }
                    else if (details.Login >= breaktimeend || details.Logout <= breaktime)
                    {
                        totalminutes = GlobalHelper.SubtractDate(details.Logout, details.Login);
                    }
                    else if ((details.Logout > breaktime && breaktimeend > details.Logout) && breaktime > details.Login)
                    {
                        totalminutes = GlobalHelper.SubtractDate(breaktime, details.Login);
                    }
                    else if (details.Login < breaktime && details.Logout > breaktimeend)
                    {
                        totalminutes = GlobalHelper.SubtractDate(details.Logout, details.Login) - ((int)PayrollParameters.CNBInstance.Value.Minutes * (sched.BreakTimeHour ?? 0));
                    }
                }
                else if (sched.BreakTimeHour.HasValue)
                {
                    totalminutes = totalminutes > PayrollParameters.CNBInstance.Value.DefaultHalfdayMinutes ?
                            (totalminutes < PayrollParameters.CNBInstance.Value.DefaultHalfdayMinutesWithBreaktime ?
                                PayrollParameters.CNBInstance.Value.DefaultHalfdayMinutes
                                : (totalminutes - ((int)sched.BreakTimeHour * 60)))
                            : totalminutes;
                }
                DateTime? timeotmuststart = details.Login?.AddMinutes((sched.BreakTimeHour.HasValue ? PayrollParameters.CNBInstance.Value.TotalMinutesPerDayWithBreak : PayrollParameters.CNBInstance.Value.TotalMinutesPerDay));
                if (details.Logout > timeotmuststart)
                {
                    totalminutes = PayrollParameters.CNBInstance.Value.TotalMinutesPerDay;
                    //compute ot
                    OTRequest ot = approvedotrequests.Where(x => x.RequestDate.Date == starttime.Date).FirstOrDefault();
                    if ((ot?.ID ?? 0) > 0 || timesheet.Personnel.AutoOT)
                    {
                        DateTime? startot = timeotmuststart;
                        DateTime? endot = details.Logout;
                        if (end > startot)
                            details.RegOTHours = GlobalHelper.SubtractDate(endot, startot) / PayrollParameters.CNBInstance.Value.Minutes;
                    }
                }

                NighDiffComputation(starttime, endtime, startnight1, endnight1, startnight2, endnight2, details);

                details.NoofDays = totalminutes < 0 ? 0 : (totalminutes / (decimal)PayrollParameters.CNBInstance.Value.TotalMinutesPerDay);
            }
        }

        private static void SundayAndHolidayComputation(PersonnelTimesheet timesheet, bool withHolidayAndSundayHazard, bool needTimeLog, List<OuterPortRequest> approvedouterportrequests, List<TimeLog> timelogs, DateTime start, ScheduleType sched, DateTime starttime, DateTime endtime, DateTime startnight1, DateTime endnight1, DateTime startnight2, DateTime endnight2, DateTime defbt, DateTime defbtend, ComputedTimelog details, OTRequest ot, NonWorkingDays holiday, DateTime? prevDate)
        {
            if (((holiday?.ID ?? 0) > 0) && (sched?.ID ?? 0) != 0 && (timelogs.Where(x => x.LoginDate?.Date == prevDate?.Date).Any() || GlobalHelper.IsOuterPortWithoutTimelog(approvedouterportrequests, prevDate) || GlobalHelper.IsWFH(timesheet.Personnel._Schedules, prevDate)))
            {
                details.NoofDays = 1;
                details.HolidayDesc = holiday.Description;
            }

            if (details.Login.HasValue && details.Logout.HasValue || withHolidayAndSundayHazard)
            {
                int mins = 0;
                if ((ot?.ID ?? 0) > 0 || timesheet.Personnel.AutoOT || withHolidayAndSundayHazard)
                {
                    if (needTimeLog || !details.IsHazard)
                    {
                        if ((details.Login > defbt && defbtend > details.Login) && defbtend < details.Logout)
                        {
                            mins = GlobalHelper.SubtractDate(details.Logout, defbtend);
                        }
                        else if (details.Login >= defbtend || details.Logout <= defbt)
                        {
                            mins = GlobalHelper.SubtractDate(details.Logout, details.Login);
                        }
                        else if ((details.Logout > defbt && defbtend > details.Logout) && defbt > details.Login)
                        {
                            mins = GlobalHelper.SubtractDate(defbt, details.Login);
                        }
                        else if (details.Login < defbt && details.Logout > defbtend)
                        {
                            mins = GlobalHelper.SubtractDate(details.Logout, details.Login) - (int)PayrollParameters.CNBInstance.Value.DefaultBreaktimeMinutes;
                        }

                        if (start.DayOfWeek == DayOfWeek.Sunday && (sched?.ID ?? 0) == 0)
                        {
                            int regminutes = mins - PayrollParameters.CNBInstance.Value.SundayTotalMinutes;
                            if (regminutes > 0
                                && (timelogs.Where(x => x.LoginDate?.Date == starttime.AddDays(-1).Date).Any() || GlobalHelper.IsOuterPortWithoutTimelog(approvedouterportrequests, starttime.AddDays(-1)) || GlobalHelper.IsWFH(timesheet.Personnel._Schedules, starttime.AddDays(-1)))
                                && (timelogs.Where(x => x.LoginDate?.Date == starttime.AddDays(1).Date).Any() || GlobalHelper.IsOuterPortWithoutTimelog(approvedouterportrequests, starttime.AddDays(1)) || GlobalHelper.IsWFH(timesheet.Personnel._Schedules, starttime.AddDays(1))))
                            {
                                details.NoofDays = 1;
                                details.SunOTHours = regminutes / PayrollParameters.CNBInstance.Value.Minutes;
                            }
                            else
                                details.NoofDays = mins < 0 ? 0 : mins / (decimal)PayrollParameters.CNBInstance.Value.TotalMinutesPerDay;

                        }
                        else if ((holiday?.ID ?? 0) > 0)
                        {
                            int regminutes = mins - PayrollParameters.CNBInstance.Value.HolidayTotalMinutes;
                            if (regminutes > 0
                                && (timelogs.Where(x => x.LoginDate?.Date == prevDate?.Date).Any() || GlobalHelper.IsOuterPortWithoutTimelog(approvedouterportrequests, prevDate) || GlobalHelper.IsWFH(timesheet.Personnel._Schedules, prevDate)))
                            {
                                details.Holiday = 1;
                                details.HolExcHours = regminutes / PayrollParameters.CNBInstance.Value.Minutes;
                            }
                            else
                                details.Holiday = mins / (decimal)PayrollParameters.CNBInstance.Value.TotalMinutesPerDay;
                        }
                    }
                    else
                    {
                        if (start.DayOfWeek == DayOfWeek.Sunday)
                        {
                            if ((timelogs.Where(x => x.LoginDate?.Date == starttime.AddDays(-1).Date).Any() || GlobalHelper.IsOuterPortWithoutTimelog(approvedouterportrequests, starttime.AddDays(-1)) || GlobalHelper.IsWFH(timesheet.Personnel._Schedules, starttime.AddDays(-1)))
                                && (timelogs.Where(x => x.LoginDate?.Date == starttime.AddDays(1).Date).Any() || GlobalHelper.IsOuterPortWithoutTimelog(approvedouterportrequests, starttime.AddDays(1)) || GlobalHelper.IsWFH(timesheet.Personnel._Schedules, starttime.AddDays(1))))
                            {
                                details.NoofDays = 1;
                            }

                        }
                        else if ((holiday?.ID ?? 0) > 0)
                        {
                            if ((timelogs.Any(x => x.LoginDate?.Date == prevDate?.Date) || GlobalHelper.IsOuterPortWithoutTimelog(approvedouterportrequests, prevDate) || GlobalHelper.IsWFH(timesheet.Personnel._Schedules, prevDate)))
                            {
                                details.Holiday = 1;
                            }
                        }
                    }

                    NighDiffComputation(starttime, endtime, startnight1, endnight1, startnight2, endnight2, details);
                }
            }
        }

        private static void NighDiffComputation(DateTime starttime, DateTime endtime, DateTime startnight1, DateTime endnight1, DateTime startnight2, DateTime endnight2, ComputedTimelog details)
        {
            if (starttime <= startnight1 && endtime >= endnight1)
            {
                DateTime? login = details.Login < startnight1 ? startnight1 : details.Login;
                DateTime? logout = details.Logout > endnight1 ? endnight1 : details.Logout;

                details.NightDiffEarlyHours = GlobalHelper.SubtractDate(logout, login) / PayrollParameters.CNBInstance.Value.Minutes;

            }
            if (starttime <= startnight2 && endtime >= endnight2)
            {
                DateTime? login = details.Login < startnight2 ? startnight2 : details.Login;
                DateTime? logout = details.Logout > endnight2 ? endnight2 : details.Logout;

                details.NightDiffLateHours = GlobalHelper.SubtractDate(logout, login) / PayrollParameters.CNBInstance.Value.Minutes;
            }
        }
    }
}
