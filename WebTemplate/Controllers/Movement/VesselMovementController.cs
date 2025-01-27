﻿using ProcessLayer.Helpers;
using ProcessLayer.Processes;
using System;
using System.Linq;
using System.Web.Mvc;
using ProcessLayer.Entities;
using WebTemplate.Models.VesselMovement;
using VesselMovement = WebTemplate.Models.VesselMovement.VesselMovement;
using ReportLayer.Reports;
using ReportLayer.Helpers;
using System.Web.Script.Serialization;

namespace WebTemplate.Controllers.Movement
{
    public class VesselMovementController : BaseController
    {
        // GET: VesselMovement
        public ActionResult Index(Index model)
        {
            model.Page = model.Page > 1 ? model.Page : 1;
            model.VesselList = VesselProcess.Instance.Value.Filter(null, model.Code, model.Description, model.Page, model.GridCount,
                out int PageCount).ToList();
            model.PageCount = PageCount;

            if (Request.IsAjaxRequest())
            {
                ModelState.Clear();
                return PartialViewCustom("_VesselMovementSearch", model);
            }
            else
            {
                return ViewCustom("_VesselMovementIndex", model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult VesselMovementManagement(VesselMovement model)
        {
            try
            {
                model.Vessel = VesselProcess.Instance.Value.Get(model.VesselID);
                model.VesselMovements =
                    VesselMovementProcess.GetList(model.VesselID, model.StartinDate, model.EndingDate);

                ModelState.Clear();
                return PartialViewCustom("_VesselMovementManagement", model);
            }
            catch (Exception ex)
            {
                return Json(new {msg = false, res = ex.GetActualMessage()});
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditOrNewVesselMovement(long? id, short? vesselid)
        {
            try
            {
                ProcessLayer.Entities.VesselMovement model = VesselMovementProcess.Get(id ?? 0);
                model.VesselID = vesselid ?? 0;

                ModelState.Clear();
                return PartialViewCustom("_VesselMovementNew", model);
            }
            catch (Exception ex)
            {
                return Json(new {msg = false, res = ex.GetActualMessage()});
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CancelVesselMovement(long? id)
        {
            try
            {
                ProcessLayer.Entities.VesselMovement model = VesselMovementProcess.Get(id ?? 0);
                ModelState.Clear();
                return PartialViewCustom("_VesselMovement", model);
            }
            catch (Exception ex)
            {
                return Json(new {msg = false, res = ex.GetActualMessage()});
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateOrUpdateVesselMovement(ProcessLayer.Entities.VesselMovement model)
        {
            try
            {
                model = VesselMovementProcess.CreateOrUpdate(model, User.UserID);

                ModelState.Clear();
                return PartialViewCustom("_VesselMovement", model);
            }
            catch (Exception ex)
            {
                return Json(new {msg = false, res = ex.GetActualMessage()});
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteVesselMovement(int? id = null)
        {
            if (id.HasValue)
            {
                try
                {
                    DeleteVesselSingle(id ?? 0);
                }
                catch
                {
                    return Json(new {msg = false, res = "Vessel Movement not found."});
                }
            }
            else
            {
                return Json(new {msg = false, res = "Vessel Movement not found."});
            }

            return Json(new {msg = true});
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult DeleteMultiple(string ids)
        {
            if (ids.Length > 0)
            {
                try
                {
                    ToApplyAction[] toApplyAction = new JavaScriptSerializer().Deserialize<ToApplyAction[]>(ids);
                    for (int i = 0; i < toApplyAction.Count(); i++)
                    {
                        DeleteVesselSingle(toApplyAction[i].ID.ToInt());
                    }
                }
                catch (Exception e)
                {
                    return Json(new { msg = false, res = e.Message });
                }
            }
            else
            {
                return Json(new { msg = false, res = "Personnel Group not found." });
            }

            return Json(new { msg = true });
        }

        public void DeleteVesselSingle(int? id = null)
        {
            VesselMovementProcess.Delete(id ?? 0, User.UserID);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult GetVesselCrews(CrewList model)
        {
            try
            {   
                model.Vessel = VesselProcess.Instance.Value.Get(model.VesselID);
                model.Crews = VesselMovementProcess.GetCrewDetailList(model.VesselID, model.StartingDate, model.EndingDate);
                ModelState.Clear();
                return PartialViewCustom("_VesselCrewSearch", model);
            }
            catch (Exception ex)
            {
                return Json(new {msg = false, res = ex.GetActualMessage()});
            }
        }

        [HttpPost]
        public ActionResult PrintVesselMovement(VesselMovement model)
        {
            //try
            //{
                using (var report = new PrintVesselMovement(Server.MapPath(PrintVesselMovementHelper.Instance.Value.Template)))
                {
                    report.StartDate = model.StartinDate;
                    report.EndDate = model.EndingDate;
                    report.Vessel = VesselProcess.Instance.Value.Get(model.VesselID);
                    report.VesselMovements = VesselMovementProcess.GetList(model.VesselID, model.StartinDate, model.EndingDate);
                    report.GenerateReport();
                    ViewBag.Content = report.SaveToPDF();
                }
                return View("~/Views/PrintingView.cshtml");
            //}
            //catch (Exception ex)
            //{
            //    //return Json(new { msg = false, res = ex.GetActualMessage() });
            //    return View("~/Views/Security/ServerError.cshtml", ex.GetActualMessage());
            //}
        }
        [HttpPost]
        public ActionResult PrintCrewList(CrewList model)
        {
            //try
            //{
                using (var report = new PrintCrewlist(Server.MapPath(PrintCrewListHelper.Instance.Value.Template)))
                {
                    report.Crews = new System.Collections.Generic.List<CrewDetails>();
                    report.StartingDate = model.StartingDate;
                    report.EndingDate = model.EndingDate;
                    report.Vessel = VesselProcess.Instance.Value.Get(model.VesselID);
                    report.Crews = VesselMovementProcess.GetCrewDetailList(model.VesselID, model.StartingDate, model.EndingDate);
                    report.GenerateReport();
                    ViewBag.Content = report.SaveToPDF();
                }
                return View("~/Views/PrintingView.cshtml");
            //}
            //catch (Exception ex)
            //{
            //    //return Json(new { msg = false, res = ex.GetActualMessage() });
            //    return View("~/Views/Security/ServerError.cshtml", ex.GetActualMessage());
            //}
        }
        
    }
}
