﻿using ProcessLayer.Entities;
using System.Collections.Generic;

namespace WebTemplate.Models.MemoArchives.PolicyAndProcedure
{
    public class Management
    {
        public ProcessLayer.Entities.PolicyAndProcedure PolicyAndProcedure { get; set; } = new ProcessLayer.Entities.PolicyAndProcedure();
        public List<PersonnelGroup> PersonnelGroups { get; set; } = new List<PersonnelGroup>();
        public List<ProcessLayer.Entities.Personnel> Personnels { get; set; } = new List<ProcessLayer.Entities.Personnel>();

        public string PersonnelGroupSearchKey { get; set; }
        public string PersonnelSearchKey { get; set; }
    }
}