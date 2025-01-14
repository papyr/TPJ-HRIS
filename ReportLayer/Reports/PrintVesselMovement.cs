﻿using ProcessLayer.Entities;
using ReportLayer.Bases;
using ReportLayer.Helpers;
using System;
using System.Collections.Generic;

namespace ReportLayer.Reports
{
    public class PrintVesselMovement : SpreadSheetReportBase
    {
        public PrintVesselMovement(string template) : base(template)
        {
        }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public Vessel Vessel { get; set; }
        public List<VesselMovement> VesselMovements { get; set; }
        public override void GenerateReport()
        {
            base.GenerateReport();

            WriteToCell(PrintVesselMovementHelper.Instance.Value.DateCell, StartDate.ToString("MMMM dd, yyyy") + " - " + EndDate.ToString("MMMM dd, yyyy"));
            WriteToCell(PrintVesselMovementHelper.Instance.Value.VesselNameCell, Vessel.Description);
            var startRow = PrintVesselMovementHelper.Instance.Value.StartRow;
            foreach(var movement in VesselMovements)
            {
                WriteToCell(startRow, PrintVesselMovementHelper.Instance.Value.DateColumn, movement.MovementDate.ToString("MMMM dd, yyyy"));
                WriteToCell(startRow, PrintVesselMovementHelper.Instance.Value.MovementColumn, movement._VesselMovementType.Description);
                WriteToCell(startRow, PrintVesselMovementHelper.Instance.Value.PlaceColumn, movement.Place);
                startRow++;
            }
        }
    }
}
