﻿using System;
using System.Collections.Generic;

namespace ProcessLayer.Entities.Kiosk
{
    public class LeaveRequest : KioskBase
    {
        public byte? LeaveTypeID { get; set; }
        public float? ApprovedLeaveCredits { get; set; }
        public float? ComputedLeaveCredits { get; set; }
        public float NoofDays { get; set; }
        public DateTime RequestedDate { get; set; }
        public string File { get; set; }
        public string FilePath { get; set; }
        public bool IsExpired { get { return ((DateTime.Now - CreatedOn).Value.TotalHours >= 48) && Approved != true && Cancelled != true && false /*For Bypassing Expiration*/; } }
        public string Remarks
        {
            get
            {
                if ((Approved ?? false) && (((_LeaveType?.HasDocumentNeeded ?? false) && string.IsNullOrEmpty(File)) || !(_LeaveType?.HasDocumentNeeded ?? false)))
                    return "Partialy approved, Waiting for document to upload";

                if (IsExpired)
                    return "Exceeded 48 hours upon creation date.";
                if (Cancelled != null && Cancelled.Value)
                    return CancellationRemarks;

                return "";
            }
        }

        public LeaveType _LeaveType { get; set; }
        public List<ComputedLeaveCredits> _ComputedLeaveCredits { get; set; }
    }
}
