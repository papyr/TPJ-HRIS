﻿using ProcessLayer.Entities;
using ProcessLayer.Entities.Kiosk;
using ProcessLayer.Processes;
using ReportLayer.Bases;
using ReportLayer.Helpers;
using System;

namespace ReportLayer.Reports
{
    public class PrintAbsence : SpreadSheetReportBase
    {
        public PrintAbsence(string template) : base(template)
        {
        }

        public AbsenceRequest AbsenceRequest { get; set; }
        public override void GenerateReport()
        {
            base.GenerateReport();

            PersonnelDepartment department = PersonnelDepartmentProcess.GetCurrentDepartment(AbsenceRequest._Personnel.ID, AbsenceRequest.CreatedOn ?? AbsenceRequest.RequestDate ?? DateTime.Now);
            PersonnelPosition position = PersonnelPositionProcess.GetCurrentPosition(AbsenceRequest._Personnel.ID, AbsenceRequest.CreatedOn ?? AbsenceRequest.RequestDate ?? DateTime.Now);
            WriteToCell(PrintAbsenceHelper.Instance.Value.NameCell, AbsenceRequest._Personnel.FullName);
            WriteToCell(PrintAbsenceHelper.Instance.Value.DateCell, AbsenceRequest.CreatedOn);
            WriteToCell(PrintAbsenceHelper.Instance.Value.DepartmentCell, department._Department.Description);
            WriteToCell(PrintAbsenceHelper.Instance.Value.PositionCell, position._Position.Description);

            if (AbsenceRequest.IsAbsent ?? false)
            {
                WriteToCell(PrintAbsenceHelper.Instance.Value.IsAbsentCell, "X");
                WriteToCell(PrintAbsenceHelper.Instance.Value.AbsentDateCell, AbsenceRequest.RequestDate?.ToString("MM/dd/yyyy"));
                WriteToCell(PrintAbsenceHelper.Instance.Value.AbsentNoofDaysCell, AbsenceRequest.NoofDays?.ToString("0.##"));
            }

            if (AbsenceRequest.IsHalfDay ?? false)
            {
                WriteToCell(PrintAbsenceHelper.Instance.Value.IsHalfdayCell, "X");
                WriteToCell(PrintAbsenceHelper.Instance.Value.HalfdayDateCell, AbsenceRequest.RequestDate?.ToString("MM/dd/yyyy"));
                WriteToCell(PrintAbsenceHelper.Instance.Value.HalfdayMorningCell, (AbsenceRequest.IsMorning ?? false) ? "X" : "");
                WriteToCell(PrintAbsenceHelper.Instance.Value.HalfdayAfternoonCell, (AbsenceRequest.IsAfternoon ?? false) ? "X" : "");
            }

            if (AbsenceRequest.IsUndertime ?? false)
            {
                WriteToCell(PrintAbsenceHelper.Instance.Value.IsUndertimeCell, "X");
                WriteToCell(PrintAbsenceHelper.Instance.Value.UndertimeDateCell, AbsenceRequest.RequestDate?.ToString("MM/dd/yyyy"));
                WriteToCell(PrintAbsenceHelper.Instance.Value.UndertimeTimeCell, AbsenceRequest.Time?.ToString("hh:mm tt"));
            }

            string reason1 = AbsenceRequest.Reasons.Length > 120 ? AbsenceRequest.Reasons.Substring(0, 120) : AbsenceRequest.Reasons;
            string reason2 = AbsenceRequest.Reasons.Replace(reason1, "");

            WriteToCell(PrintAbsenceHelper.Instance.Value.ReasonsCell, reason1);
            WriteToCell(PrintAbsenceHelper.Instance.Value.Reasons2Cell, reason2);
        }
    }
}
